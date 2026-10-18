using System.Text.RegularExpressions;
using EducationKeeptrack.Billing.Domain;
using EducationKeeptrack.Billing.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EducationKeeptrack.Billing.Application;

public record RecipientInput(string BankBin, string BankName, string AccountNo, string AccountName, string? StaticQrBase64, long Version);
public record RecipientView(string BankBin, string BankName, string AccountNo, string AccountName, bool HasStaticQr, long Version);
public record TransferInput(Guid StudentId, string Month, long Version, long RecipientVersion);
public record TransferView(string BankBin, string BankName, string AccountNo, string AccountName, string AmountVnd,
    string Content, string QrKind, string? QrDataUrl, string? StaticQrDataUrl, long Version, long RecipientVersion);
public interface IVietQrGenerator
{
    Task<byte[]?> GenerateAsync(RecipientView recipient, string amountVnd, string content, CancellationToken ct);
}

public sealed class PaymentQrService(BillingDatabase db, PaymentService payments, IVietQrGenerator generator)
{
    public async Task<BillingResult<RecipientView>> RecipientAsync(string owner, CancellationToken ct)
    {
        var recipient = await db.Set<PaymentRecipient>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner, ct);
        return recipient is null ? new(null, BillingFailure.NotFound) : new(View(recipient));
    }

    public async Task<BillingResult<RecipientView>> SaveAsync(string owner, RecipientInput input, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Regex.IsMatch(input.BankBin ?? "", "^970[0-9]{3}$")) errors["bankBin"] = ["Mã BIN ngân hàng phải gồm 6 chữ số, bắt đầu bằng 970."];
        if (string.IsNullOrWhiteSpace(input.BankName) || input.BankName.Length > 100) errors["bankName"] = ["Nhập tên ngân hàng (tối đa 100 ký tự)."];
        if (!Regex.IsMatch(input.AccountNo ?? "", "^[0-9]{6,19}$")) errors["accountNo"] = ["Số tài khoản phải gồm 6–19 chữ số."];
        if (!Regex.IsMatch(input.AccountName ?? "", "^[A-Z ]{5,50}$") || string.IsNullOrWhiteSpace(input.AccountName)) errors["accountName"] = ["Tên người nhận gồm 5–50 chữ cái viết hoa không dấu và khoảng trắng."];
        byte[]? image = null;
        if (!string.IsNullOrEmpty(input.StaticQrBase64))
        {
            try { image = Convert.FromBase64String(input.StaticQrBase64); }
            catch (FormatException) { errors["staticQrBase64"] = ["Ảnh QR không hợp lệ."]; }
            if (image is not null && !QrImages.IsPng(image)) errors["staticQrBase64"] = ["Chọn ảnh PNG hợp lệ, tối đa 1 MB."];
        }
        if (errors.Count > 0) return new(null, BillingFailure.Invalid, Errors: errors);
        // Serializes both first creation and replacement for this teacher.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({owner}, 7))", ct);
        var recipient = await db.Set<PaymentRecipient>().SingleOrDefaultAsync(x => x.OwnerId == owner, ct);
        if ((recipient?.Version ?? 0) != input.Version) return new(null, BillingFailure.Conflict, "Tài khoản nhận tiền đã thay đổi. Vui lòng tải lại.");
        if (recipient is null) { recipient = new() { OwnerId = owner }; db.Add(recipient); }
        var sameAccount = recipient.BankBin == input.BankBin && recipient.AccountNo == input.AccountNo && recipient.AccountName == input.AccountName!.Trim();
        recipient.BankBin = input.BankBin!; recipient.BankName = input.BankName.Trim();
        recipient.AccountNo = input.AccountNo!; recipient.AccountName = input.AccountName!.Trim();
        // null preserves the current image only for an unchanged recipient; empty removes it.
        if (input.StaticQrBase64 is null && sameAccount) image = recipient.StaticQr;
        recipient.StaticQr = image;
        recipient.Version++;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(View(recipient));
    }

    public async Task<BillingResult<TransferView>> CreateAsync(string owner, Guid classId, TransferInput input, CancellationToken ct)
    {
        var before = await payments.ReportAsync(owner, classId, input.Month, ct);
        if (before.Failure is not null) return new(null, before.Failure, before.Message, before.Errors);
        var student = before.Value!.Students.SingleOrDefault(x => x.StudentId == input.StudentId);
        if (student is null) return new(null, BillingFailure.NotFound);
        var recipient = await db.Set<PaymentRecipient>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner, ct);
        if (recipient is null) return new(null, BillingFailure.Invalid, Errors: new() { ["recipient"] = ["Chưa cấu hình tài khoản nhận tiền."] });
        if (before.Value.Status != "ready" || before.Value.Version != input.Version || recipient.Version != input.RecipientVersion)
            return Stale();
        if (student.TotalDebtVnd == "0") return new(null, BillingFailure.Invalid, Errors: new() { ["amount"] = ["Không còn dư nợ; không tạo yêu cầu chuyển khoản."] });
        var content = $"HP{input.Month.Replace("-", "")}{input.StudentId:N}"[..24];
        byte[]? qr = null;
        try { qr = await generator.GenerateAsync(View(recipient), student.TotalDebtVnd, content, ct); }
        catch (HttpRequestException) { }
        catch (IOException) { }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
        var kind = QrImages.IsPng(qr) ? "vietqr" : QrImages.IsPng(recipient.StaticQr) ? "static" : "manual";
        qr = kind == "vietqr" ? qr : kind == "static" ? recipient.StaticQr : null;
        var after = await payments.ReportAsync(owner, classId, input.Month, ct);
        var current = await db.Set<PaymentRecipient>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner, ct);
        if (after.Value?.Status != "ready" || after.Value.Version != before.Value.Version || current?.Version != recipient.Version) return Stale();
        return new(new TransferView(recipient.BankBin, recipient.BankName, recipient.AccountNo, recipient.AccountName,
            student.TotalDebtVnd, content, kind, DataUrl(qr), QrImages.IsPng(recipient.StaticQr) ? DataUrl(recipient.StaticQr) : null, before.Value.Version, recipient.Version));
    }
    private static string? DataUrl(byte[]? image) => image is null ? null : "data:image/png;base64," + Convert.ToBase64String(image);
    private static BillingResult<TransferView> Stale() => new(null, BillingFailure.Conflict, "Công nợ hoặc tài khoản đã thay đổi. Mã cũ lỗi thời; vui lòng tải lại và tạo mã mới.");
    private static RecipientView View(PaymentRecipient r) => new(r.BankBin, r.BankName, r.AccountNo, r.AccountName, r.StaticQr is not null, r.Version);
}

public static class QrImages
{
    public static bool IsPng(byte[]? data)
    {
        if (data is not { Length: >= 45 and <= 1048576 }
            || !data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            || !data.AsSpan(12, 4).SequenceEqual("IHDR"u8)
            || !data.AsSpan(data.Length - 8, 4).SequenceEqual("IEND"u8)) return false;
        // Bound decoded allocation before allowing the image decoder to read pixels.
        var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20, 4));
        if (width is 0 or > 2048 || height is 0 or > 2048) return false;
        try
        {
            var image = StbImageSharp.ImageResult.FromMemory(data, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            return image.Width == width && image.Height == height;
        }
        // Invalid compressed pixel data is an external input failure.
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }
}

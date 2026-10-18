using EducationKeeptrack.Billing.Application;

namespace EducationKeeptrack.Billing.Infrastructure;

public sealed class VietQrGenerator(HttpClient client) : IVietQrGenerator
{
    public async Task<byte[]?> GenerateAsync(RecipientView recipient, string amountVnd, string content, CancellationToken ct)
    {
        // VietQR Quick Link supports at most thirteen digits for the amount.
        if (amountVnd.Length > 13) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        ct = timeout.Token;
        var url = $"https://img.vietqr.io/image/{recipient.BankBin}-{recipient.AccountNo}-compact2.png?amount={amountVnd}&addInfo={Uri.EscapeDataString(content)}&accountName={Uri.EscapeDataString(recipient.AccountName)}";
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 1048576) return null;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(block, ct)) > 0)
        {
            if (buffer.Length + count > 1048576) return null;
            buffer.Write(block, 0, count);
        }
        var bytes = buffer.ToArray();
        return QrImages.IsPng(bytes) ? bytes : null;
    }
}

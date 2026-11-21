using System.Text;
using EducationKeeptrack.Billing.Application;
using QRCoder;

namespace EducationKeeptrack.Billing.Infrastructure;

/// <summary>
/// The text a VietQR code carries: NAPAS 247 "transfer to account" (EMVCo merchant-presented QR, dynamic, with an amount
/// and a transfer note). Banking apps read this string; the bank and account number are the whole recipient.
/// </summary>
public static class VietQrPayload
{
    public static string Build(string bankBin, string accountNo, string amountVnd, string content)
    {
        var beneficiary = Tlv("00", "A000000727")
            + Tlv("01", Tlv("00", bankBin) + Tlv("01", accountNo))
            + Tlv("02", "QRIBFTTA");
        var body = Tlv("00", "01") + Tlv("01", "12") + Tlv("38", beneficiary) + Tlv("53", "704") + Tlv("54", amountVnd)
            + Tlv("58", "VN") + Tlv("62", Tlv("08", content)) + "6304";
        return body + Crc16(body);
    }

    private static string Tlv(string tag, string value) => tag + value.Length.ToString("00") + value;

    // CRC-16/CCITT-FALSE over the whole text including the "6304" that introduces the checksum.
    private static string Crc16(string text)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(text))
        {
            crc ^= (ushort)(b << 8);
            for (var bit = 0; bit < 8; bit++) crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc.ToString("X4");
    }
}

/// <summary>
/// Draws the VietQR code here instead of asking an image service for it: the QR no longer depends on a third party being
/// reachable or keeping its response format, and nothing but our own PNG is ever shown or put in a PDF.
/// </summary>
public sealed class LocalVietQrGenerator : IVietQrGenerator
{
    public Task<byte[]?> GenerateAsync(RecipientView recipient, string amountVnd, string content, CancellationToken ct)
    {
        // The amount field holds at most thirteen digits and the transfer note at most twenty-five ASCII characters.
        if (amountVnd.Length is 0 or > 13 || content.Length is 0 or > 25 || content.Any(c => c is < ' ' or > '~'))
            return Task.FromResult<byte[]?>(null);
        var payload = VietQrPayload.Build(recipient.BankBin, recipient.AccountNo, amountVnd, content);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        return Task.FromResult<byte[]?>(new PngByteQRCode(data).GetGraphic(10));
    }
}

using EducationKeeptrack.Billing.Application;
using EducationKeeptrack.Billing.Infrastructure;

namespace ApiTests;

// The VietQR code is drawn here. The expected payload below is what the public VietQR service put in its own code for
// the same bank, account, amount and note (read back from its image), so a banking app reads ours the same way.
public sealed class VietQrPayloadTests
{
    private const string Reference = "00020101021238540010A00000072701240006970415011001234567890208QRIBFTTA530370454064500005802VN62160812HP202611BINH63047DC3";

    [Fact]
    public void The_payload_matches_the_one_the_vietqr_service_produces() =>
        Assert.Equal(Reference, VietQrPayload.Build("970415", "0123456789", "450000", "HP202611BINH"));

    [Fact]
    public void The_checksum_covers_every_character_before_it()
    {
        var payload = VietQrPayload.Build("970436", "1234567890123", "1200000", "HP202610AN");
        Assert.EndsWith(VietQrPayload.Build("970436", "1234567890123", "1200000", "HP202610AN")[^4..], payload);
        Assert.NotEqual(payload[^4..], VietQrPayload.Build("970436", "1234567890124", "1200000", "HP202610AN")[^4..]);
    }

    private static readonly RecipientView Recipient = new("970415", "VietinBank", "0123456789", "NGUYEN VAN A", false, 1);

    [Fact]
    public async Task The_generator_draws_a_png_the_app_accepts()
    {
        var png = await new LocalVietQrGenerator().GenerateAsync(Recipient, "450000", "HP202611BINH", default);
        Assert.True(QrImages.IsPng(png));
    }

    [Theory]
    [InlineData("12345678901234", "HP1")]      // amount longer than thirteen digits
    [InlineData("450000", "")]                 // no note
    [InlineData("450000", "ABCDEFGHIJKLMNOPQRSTUVWXYZ")] // note longer than twenty-five characters
    [InlineData("450000", "Học phí")]          // not ASCII
    public async Task Values_the_code_cannot_carry_give_no_image(string amount, string content) =>
        Assert.Null(await new LocalVietQrGenerator().GenerateAsync(Recipient, amount, content, default));
}

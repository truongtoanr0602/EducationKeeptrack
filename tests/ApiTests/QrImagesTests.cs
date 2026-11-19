using System.Text;
using EducationKeeptrack.Billing.Application;

namespace ApiTests;

// The VietQR service may reply with a PNG or with an SVG wrapper that embeds the PNG as a data URL.
public sealed class QrImagesTests
{
    private static readonly byte[] Png = Convert.FromBase64String(ServiceFeeQrTests.Png(9));

    private static byte[] Svg(string href) =>
        Encoding.UTF8.GetBytes($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"540\" height=\"640\"><rect width=\"540\" height=\"640\"/><image href=\"{href}\"/><text>Số tiền</text></svg>");

    [Fact]
    public void A_plain_png_is_returned_unchanged() => Assert.Equal(Png, QrImages.FromResponse(Png));

    [Fact]
    public void The_png_embedded_in_an_svg_wrapper_is_extracted() =>
        Assert.Equal(Png, QrImages.FromResponse(Svg("data:image/png;base64," + Convert.ToBase64String(Png))));

    [Theory]
    [InlineData("data:image/png;base64,!!!not-base64!!!")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("https://example.test/qr.png")]
    public void An_svg_without_a_valid_embedded_png_is_rejected(string href) => Assert.Null(QrImages.FromResponse(Svg(href)));

    [Fact]
    public void Other_content_is_rejected()
    {
        Assert.Null(QrImages.FromResponse([]));
        Assert.Null(QrImages.FromResponse(Encoding.UTF8.GetBytes("<html>data:image/png;base64," + Convert.ToBase64String(Png) + "\"</html>")));
    }
}

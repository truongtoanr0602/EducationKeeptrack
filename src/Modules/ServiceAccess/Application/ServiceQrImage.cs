namespace EducationKeeptrack.ServiceAccess.Application;

// Admin's static bank QR for the service fee. The whole JSON request is capped at 128 KiB by nginx and Kestrel and
// base64 adds a third, so the image itself stays small enough to leave room for the text fields.
public static class ServiceQrImage
{
    public const int MaxBytes = 80 * 1024;
    // 4 * ceil(MaxBytes / 3): lets an oversized upload be refused before it is decoded.
    public const int MaxBase64Length = (MaxBytes + 2) / 3 * 4;
    public const string Message = "Chọn ảnh QR dạng PNG hợp lệ, tối đa 80 KB.";
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static bool IsPng(byte[]? data)
    {
        if (data is not { Length: >= 45 and <= MaxBytes }
            || !data.AsSpan(0, 8).SequenceEqual(Signature)
            || !data.AsSpan(12, 4).SequenceEqual("IHDR"u8)
            || !data.AsSpan(data.Length - 8, 4).SequenceEqual("IEND"u8)) return false;
        var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20, 4));
        // Bound the decoded allocation before the decoder reads any pixel.
        if (width is 0 or > 2048 || height is 0 or > 2048) return false;
        try
        {
            var image = StbImageSharp.ImageResult.FromMemory(data, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            return image.Width == width && image.Height == height;
        }
        // Invalid compressed pixel data is an external input failure, not a program error.
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    public static string? DataUrl(byte[]? image) => image is null ? null : "data:image/png;base64," + Convert.ToBase64String(image);
}

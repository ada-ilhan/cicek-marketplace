namespace Cicek.Domain.Media;

public enum ImageType
{
    Jpeg,
    Png,
    WebP,
}

/// <summary>
/// Detects the real image type from the file's leading bytes. The client-supplied file name and MIME type are never
/// trusted. Only JPEG, PNG and WebP are accepted (no SVG: it can carry scripts; no GIF/animated formats).
/// </summary>
public static class ImageSignature
{
    public const int HeaderLength = 12;

    public static ImageType? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ImageType.Jpeg;
        }

        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (header.Length >= png.Length && header[..png.Length].SequenceEqual(png))
        {
            return ImageType.Png;
        }

        // RIFF....WEBP
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return ImageType.WebP;
        }

        return null;
    }

    public static string ContentType(ImageType type) => type switch
    {
        ImageType.Jpeg => "image/jpeg",
        ImageType.Png => "image/png",
        ImageType.WebP => "image/webp",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string Extension(ImageType type) => type switch
    {
        ImageType.Jpeg => "jpg",
        ImageType.Png => "png",
        ImageType.WebP => "webp",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}

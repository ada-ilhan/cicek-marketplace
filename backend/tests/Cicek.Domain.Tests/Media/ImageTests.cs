using Cicek.Domain.Media;

namespace Cicek.Domain.Tests.Media;

public sealed class ImageTests
{
    [Fact]
    public void Signatures_are_detected_from_bytes()
    {
        Assert.Equal(ImageType.Jpeg, ImageSignature.Detect([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal(ImageType.Png, ImageSignature.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]));
        Assert.Equal(ImageType.WebP, ImageSignature.Detect("RIFF\0\0\0\0WEBPVP8 "u8));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<svg/>")]
    [InlineData("GIF89a")]
    [InlineData("%PDF-1.7")]
    [InlineData("RIFF\0\0\0\0WAVEfmt ")]
    public void Other_content_is_not_an_image(string text)
    {
        Assert.Null(ImageSignature.Detect(System.Text.Encoding.Latin1.GetBytes(text)));
    }

    [Fact]
    public void Platform_references_are_normalized_to_a_canonical_form()
    {
        var id = Guid.NewGuid();
        Assert.Equal($"/media/{id:D}", ImageReference.Normalize($"/media/{id:D}"));
        Assert.True(ImageReference.TryGetMediaId($"/media/{id:D}", out var parsed));
        Assert.Equal(id, parsed);
    }

    [Theory]
    [InlineData("/media/abc")]
    [InlineData("/media/../etc/passwd")]
    [InlineData("/other/00000000-0000-0000-0000-000000000001")]
    [InlineData("http://example.com/a.jpg")]
    [InlineData("https://u:p@example.com/a.jpg")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void Unsafe_or_malformed_references_are_refused(string? value)
    {
        Assert.Null(ImageReference.Normalize(value));
    }

    [Fact]
    public void An_https_url_is_accepted()
    {
        Assert.Equal("https://cdn.example.com/a.jpg", ImageReference.Normalize("https://cdn.example.com/a.jpg"));
    }
}

using Ecosologic.Api.Media;

namespace Ecosologic.Api.Tests;

public sealed class MediaFormatDetectorTests
{
    [Fact]
    public void Detect_recognizes_jpeg_from_magic_bytes()
    {
        byte[] bytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

        Assert.Equal(MediaFormat.Jpeg, MediaFormatDetector.Detect(bytes));
    }

    [Fact]
    public void Detect_recognizes_png_from_signature()
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        Assert.Equal(MediaFormat.Png, MediaFormatDetector.Detect(bytes));
    }

    [Fact]
    public void Detect_recognizes_webp_from_signature()
    {
        byte[] bytes = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

        Assert.Equal(MediaFormat.WebP, MediaFormatDetector.Detect(bytes));
    }

    [Fact]
    public void Detect_returns_null_for_empty_input()
    {
        Assert.Null(MediaFormatDetector.Detect([]));
    }

    [Fact]
    public void Detect_returns_null_for_unknown_format()
    {
        byte[] bytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61];

        Assert.Null(MediaFormatDetector.Detect(bytes));
    }

    [Fact]
    public void Detect_returns_null_for_truncated_signature()
    {
        byte[] bytes = [0x89, 0x50];

        Assert.Null(MediaFormatDetector.Detect(bytes));
    }
}

using Ecosologic.Api.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace Ecosologic.Api.Tests;

public sealed class ImageSanitizerTests
{
    private readonly ImageSanitizer _sanitizer = new();

    [Fact]
    public async Task Sanitize_returns_png_for_valid_png()
    {
        var result = await _sanitizer.SanitizeAsync(new MemoryStream(TestImageFactory.Png()), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(MediaFormat.Png, result.Media!.Format);
        Assert.Equal("image/png", result.Media.ContentType);
        Assert.Equal(".png", result.Media.Extension);
        Assert.NotEmpty(result.Media.Content);
    }

    [Fact]
    public async Task Sanitize_returns_jpeg_for_valid_jpeg()
    {
        var result = await _sanitizer.SanitizeAsync(new MemoryStream(TestImageFactory.Jpeg()), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(MediaFormat.Jpeg, result.Media!.Format);
        Assert.Equal("image/jpeg", result.Media.ContentType);
        Assert.Equal(".jpg", result.Media.Extension);
    }

    [Fact]
    public async Task Sanitize_returns_webp_for_valid_webp()
    {
        var result = await _sanitizer.SanitizeAsync(new MemoryStream(TestImageFactory.WebP()), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(MediaFormat.WebP, result.Media!.Format);
        Assert.Equal("image/webp", result.Media.ContentType);
        Assert.Equal(".webp", result.Media.Extension);
    }

    [Fact]
    public async Task Sanitize_rejects_truncated_image()
    {
        var truncated = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00 };

        var result = await _sanitizer.SanitizeAsync(new MemoryStream(truncated), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Sanitize_rejects_unknown_format()
    {
        var result = await _sanitizer.SanitizeAsync(new MemoryStream([0x47, 0x49, 0x46, 0x38]), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Sanitize_reencodes_to_strip_metadata()
    {
        var original = TestImageFactory.JpegWithExif("malicious-payload");

        var result = await _sanitizer.SanitizeAsync(new MemoryStream(original), CancellationToken.None);

        Assert.True(result.Success);
        using var sanitized = Image.Load(result.Media!.Content);
        Assert.Null(sanitized.Metadata.ExifProfile);
    }

    [Fact]
    public async Task Sanitize_produces_decodable_png()
    {
        var result = await _sanitizer.SanitizeAsync(new MemoryStream(TestImageFactory.Png()), CancellationToken.None);

        Assert.True(result.Success);
        using var decoded = Image.Load(result.Media!.Content);
        Assert.IsType<PngFormat>(decoded.Metadata.DecodedImageFormat);
    }

    [Fact]
    public async Task Sanitize_rejects_image_exceeding_max_pixel_count()
    {
        var header = TestImageFactory.PngHeader(6000, 5000);

        var result = await _sanitizer.SanitizeAsync(new MemoryStream(header), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("pixel", result.Error);
    }

    [Fact]
    public async Task Sanitize_rejects_image_exceeding_max_dimension()
    {
        var image = TestImageFactory.Png(MediaConstraints.MaxDimension + 1, 1);

        var result = await _sanitizer.SanitizeAsync(new MemoryStream(image), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("dimens", result.Error);
    }

    [Fact]
    public async Task Sanitize_accepts_image_within_pixel_limits()
    {
        var result = await _sanitizer.SanitizeAsync(new MemoryStream(TestImageFactory.Png(100, 100)), CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Sanitize_rejects_animated_image()
    {
        var result = await _sanitizer.SanitizeAsync(new MemoryStream(TestImageFactory.AnimatedWebP()), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("frame", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sanitize_releases_decode_gate_after_success()
    {
        var sanitizer = new ImageSanitizer();
        var before = sanitizer.DecodeGate.CurrentCount;

        var result = await sanitizer.SanitizeAsync(new MemoryStream(TestImageFactory.Png()), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(before, sanitizer.DecodeGate.CurrentCount);
    }

    [Fact]
    public async Task Sanitize_releases_decode_gate_after_failure()
    {
        var sanitizer = new ImageSanitizer();
        var before = sanitizer.DecodeGate.CurrentCount;
        var truncated = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00 };

        var result = await sanitizer.SanitizeAsync(new MemoryStream(truncated), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(before, sanitizer.DecodeGate.CurrentCount);
    }

    [Fact]
    public async Task Sanitize_waits_cancelably_when_decode_gate_is_exhausted()
    {
        var sanitizer = new ImageSanitizer();
        await sanitizer.DecodeGate.WaitAsync();
        await sanitizer.DecodeGate.WaitAsync();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sanitizer.SanitizeAsync(new MemoryStream(TestImageFactory.Png()), cts.Token));
        }
        finally
        {
            sanitizer.DecodeGate.Release();
            sanitizer.DecodeGate.Release();
        }
    }

    [Fact]
    public async Task Sanitize_cancels_during_decode()
    {
        var sanitizer = new ImageSanitizer();
        var before = sanitizer.DecodeGate.CurrentCount;

        var large = TestImageFactory.Jpeg(2500, 2500);
        using var cts = new CancellationTokenSource();

        var task = Task.Run(() => sanitizer.SanitizeAsync(new MemoryStream(large), cts.Token));

        // Espera o semáforo ser adquirido (passado a cópia e a espera no gate) para que o
        // cancelamento caia dentro do Identify/Load, e não na espera do próprio semáforo.
        var spin = new SpinWait();
        while (sanitizer.DecodeGate.CurrentCount == before && !task.IsCompleted)
            spin.SpinOnce();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        Assert.Equal(before, sanitizer.DecodeGate.CurrentCount);
    }

    [Fact]
    public async Task Decode_gate_limits_concurrency_to_two()
    {
        var sanitizer = new ImageSanitizer();

        Assert.Equal(ImageSanitizer.MaxConcurrentImageDecodes, sanitizer.DecodeGate.CurrentCount);

        await sanitizer.DecodeGate.WaitAsync();
        await sanitizer.DecodeGate.WaitAsync();
        Assert.Equal(0, sanitizer.DecodeGate.CurrentCount);

        sanitizer.DecodeGate.Release();
        Assert.Equal(1, sanitizer.DecodeGate.CurrentCount);

        sanitizer.DecodeGate.Release();
        Assert.Equal(ImageSanitizer.MaxConcurrentImageDecodes, sanitizer.DecodeGate.CurrentCount);
    }
}

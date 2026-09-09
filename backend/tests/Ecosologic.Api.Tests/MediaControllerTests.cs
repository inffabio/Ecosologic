using Ecosologic.Api.Controllers;
using Ecosologic.Api.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SixLabors.ImageSharp;

namespace Ecosologic.Api.Tests;

public sealed class MediaControllerTests
{
    [Fact]
    public void Upload_requires_admin_role()
    {
        var method = typeof(MediaController).GetMethod(nameof(MediaController.UploadMedia));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Equal("Admin", ((AuthorizeAttribute)attribute).Roles);
    }

    [Fact]
    public void Upload_limits_multipart_body_to_five_megabytes()
    {
        var method = typeof(MediaController).GetMethod(nameof(MediaController.UploadMedia));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(RequestFormLimitsAttribute), true));
        Assert.Equal((long)MediaConstraints.MaxBytes, ((RequestFormLimitsAttribute)attribute).MultipartBodyLengthLimit);
    }

    [Fact]
    public void Upload_has_rate_limiting_policy_named_upload()
    {
        var method = typeof(MediaController).GetMethod(nameof(MediaController.UploadMedia));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true));
        Assert.Equal("upload", ((EnableRateLimitingAttribute)attribute).PolicyName);
    }

    [Fact]
    public async Task Upload_saves_valid_image_and_returns_url()
    {
        var tempDir = NewTempDir();
        try
        {
            var controller = NewController(tempDir);
            var file = NewFile("photo.png", "image/png", TestImageFactory.Png());

            var result = await controller.UploadMedia(file, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<MediaUploadResponse>(ok.Value);
            Assert.StartsWith("/uploads/", response.Url);
            Assert.EndsWith(".png", response.Url);
            var saved = Assert.Single(Directory.GetFiles(tempDir));
            Assert.Equal(response.Url["/uploads/".Length..], Path.GetFileName(saved));

            using var decoded = Image.Load(File.ReadAllBytes(saved));
            Assert.Equal("image/png", decoded.Metadata.DecodedImageFormat!.DefaultMimeType);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task Upload_rejects_empty_file()
    {
        var controller = NewController(NewTempDir());

        var result = await controller.UploadMedia(NewFile("photo.png", "image/png", []), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_rejects_unsupported_content_type()
    {
        var controller = NewController(NewTempDir());

        var result = await controller.UploadMedia(NewFile("photo.gif", "image/gif", [0x47, 0x49, 0x46]), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_rejects_invalid_extension()
    {
        var controller = NewController(NewTempDir());

        var result = await controller.UploadMedia(NewFile("malware.exe", "image/png", TestImageFactory.Png()), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_rejects_file_over_five_megabytes()
    {
        var controller = NewController(NewTempDir());

        var result = await controller.UploadMedia(NewFile("big.png", "image/png", new byte[MediaConstraints.MaxBytes + 1]), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_rejects_mismatched_signature()
    {
        var controller = NewController(NewTempDir());

        var result = await controller.UploadMedia(NewFile("fake.png", "image/png", TestImageFactory.Jpeg()), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_rejects_extension_content_type_mismatch()
    {
        var controller = NewController(NewTempDir());

        var result = await controller.UploadMedia(NewFile("photo.png", "image/jpeg", TestImageFactory.Jpeg()), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_rejects_truncated_image_without_500()
    {
        var controller = NewController(NewTempDir());
        var truncated = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00 };

        var result = await controller.UploadMedia(NewFile("broken.png", "image/png", truncated), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_rejects_non_image_content()
    {
        var controller = NewController(NewTempDir());
        var text = System.Text.Encoding.UTF8.GetBytes("hello, this is not an image");

        var result = await controller.UploadMedia(NewFile("photo.png", "image/png", text), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_saves_reencoded_content_not_original()
    {
        var tempDir = NewTempDir();
        try
        {
            var storage = new LocalMediaStorage(tempDir);
            var controller = new MediaController(storage, new ImageSanitizer());
            var original = TestImageFactory.JpegWithExif("malicious-payload");

            var result = await controller.UploadMedia(NewFile("photo.jpg", "image/jpeg", original), CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<MediaUploadResponse>(ok.Value);
            var savedPath = Path.Combine(tempDir, response.Url["/uploads/".Length..]);
            var savedBytes = File.ReadAllBytes(savedPath);

            Assert.NotEqual(original, savedBytes);
            using var decoded = Image.Load(savedBytes);
            Assert.Null(decoded.Metadata.ExifProfile);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    private static MediaController NewController(string tempDir) =>
        new(new LocalMediaStorage(tempDir), new ImageSanitizer());

    private static string NewTempDir() =>
        Path.Combine(Path.GetTempPath(), "ecosologic-media-" + Guid.NewGuid().ToString("N"));

    private static void DeleteTempDir(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    private static IFormFile NewFile(string fileName, string contentType, byte[] bytes) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
}

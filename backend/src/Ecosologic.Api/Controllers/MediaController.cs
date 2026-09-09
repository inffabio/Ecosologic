using Ecosologic.Api.Media;
using Ecosologic.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecosologic.Api.Controllers;

[ApiController]
[Route("api/content")]
public sealed class MediaController(IMediaStorage storage, IImageSanitizer sanitizer) : ControllerBase
{
    [HttpPost("media")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting(MediaRateLimiter.PolicyName)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaConstraints.MaxBytes)]
    public async Task<IActionResult> UploadMedia(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Arquivo vazio." });

        if (file.Length > MediaConstraints.MaxBytes)
            return BadRequest(new { error = "Arquivo excede o limite de 5 MB." });

        var declaredContentType = MediaConstraints.NormalizeContentType(file.ContentType);
        if (!MediaConstraints.IsAllowedContentType(declaredContentType))
            return BadRequest(new { error = "Tipo de imagem não permitido. Use JPG, PNG ou WebP." });

        if (!MediaConstraints.IsAllowedExtension(file.FileName))
            return BadRequest(new { error = "Extensão de arquivo não permitida." });

        await using var input = file.OpenReadStream();
        var result = await sanitizer.SanitizeAsync(input, cancellationToken);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        var media = result.Media!;
        var declaredFormat = MediaConstraints.FormatForContentType(declaredContentType);
        var extensionFormat = MediaConstraints.FormatForExtension(file.FileName);

        if (media.Format != declaredFormat || media.Format != extensionFormat)
            return BadRequest(new { error = "O arquivo não corresponde ao tipo ou extensão declarados." });

        await using var content = new MemoryStream(media.Content);
        var fileName = await storage.SaveAsync(content, media.Extension, cancellationToken);
        return Ok(new MediaUploadResponse($"/uploads/{fileName}"));
    }
}

public sealed record MediaUploadResponse(string Url);

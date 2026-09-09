namespace Ecosologic.Api.Media;

public interface IImageSanitizer
{
    Task<SanitizeResult> SanitizeAsync(Stream input, CancellationToken cancellationToken);
}

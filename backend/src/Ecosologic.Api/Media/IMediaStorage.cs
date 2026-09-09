namespace Ecosologic.Api.Media;

public interface IMediaStorage
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);
}

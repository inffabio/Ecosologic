namespace Ecosologic.Api.Media;

public sealed class LocalMediaStorage : IMediaStorage
{
    private readonly string _mediaRootPath;
    private readonly Func<string, string> _fileNameFactory;

    public LocalMediaStorage(string mediaRootPath)
        : this(mediaRootPath, extension => Guid.NewGuid().ToString("N") + extension)
    {
    }

    public LocalMediaStorage(string mediaRootPath, Func<string, string> fileNameFactory)
    {
        _mediaRootPath = mediaRootPath;
        _fileNameFactory = fileNameFactory;
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_mediaRootPath);
        var fileName = _fileNameFactory(extension);
        var fullPath = Path.Combine(_mediaRootPath, fileName);
        var tempPath = Path.Combine(_mediaRootPath, fileName + ".tmp");
        var moved = false;

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(stream, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(tempPath, fullPath, overwrite: false);
            moved = true;

            cancellationToken.ThrowIfCancellationRequested();
            return fileName;
        }
        catch
        {
            if (moved && File.Exists(fullPath))
            {
                try { File.Delete(fullPath); } catch { }
            }
            else if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }

            throw;
        }
    }
}

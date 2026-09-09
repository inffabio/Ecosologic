namespace Ecosologic.Api.Media;

public sealed record SanitizedMedia(MediaFormat Format, string ContentType, string Extension, byte[] Content);

public sealed record SanitizeResult(bool Success, string? Error = null, SanitizedMedia? Media = null)
{
    public static SanitizeResult Ok(SanitizedMedia media) => new(true, Media: media);

    public static SanitizeResult Fail(string error) => new(false, Error: error);
}

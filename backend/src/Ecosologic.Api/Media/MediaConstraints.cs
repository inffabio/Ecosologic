namespace Ecosologic.Api.Media;

public static class MediaConstraints
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public const int MaxPixels = 25_000_000;

    public const int MaxDimension = 16384;

    // Uploads do site sao fotos; apenas imagens de frame unico sao aceitas.
    // Imagens animadas (APNG/WebP/GIF) sao rejeitadas antes do decode completo.
    public const int MaxFrames = 1;

    private static readonly IReadOnlyDictionary<MediaFormat, string> ContentTypeByFormat =
        new Dictionary<MediaFormat, string>
        {
            [MediaFormat.Jpeg] = "image/jpeg",
            [MediaFormat.Png] = "image/png",
            [MediaFormat.WebP] = "image/webp"
        };

    private static readonly IReadOnlyDictionary<MediaFormat, string> ExtensionByFormat =
        new Dictionary<MediaFormat, string>
        {
            [MediaFormat.Jpeg] = ".jpg",
            [MediaFormat.Png] = ".png",
            [MediaFormat.WebP] = ".webp"
        };

    private static readonly IReadOnlyDictionary<string, MediaFormat> FormatByContentType =
        new Dictionary<string, MediaFormat>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = MediaFormat.Jpeg,
            ["image/png"] = MediaFormat.Png,
            ["image/webp"] = MediaFormat.WebP
        };

    private static readonly IReadOnlyDictionary<string, MediaFormat> FormatByExtension =
        new Dictionary<string, MediaFormat>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = MediaFormat.Jpeg,
            [".jpeg"] = MediaFormat.Jpeg,
            [".png"] = MediaFormat.Png,
            [".webp"] = MediaFormat.WebP
        };

    public static string? ValidatePixelLimits(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return "Imagem com dimensões inválidas.";

        if (width > MaxDimension || height > MaxDimension)
            return "A imagem excede as dimensões máximas permitidas.";

        if ((long)width * height > MaxPixels)
            return "A imagem excede o limite de 25 megapixels.";

        return null;
    }

    public static string? ValidateFrameCount(int frameCount)
    {
        if (frameCount > MaxFrames)
            return "Imagens animadas (múltiplos frames) não são permitidas.";

        return null;
    }

    public static string NormalizeContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ? string.Empty : contentType.Split(';')[0].Trim();

    public static bool IsAllowedContentType(string contentType) => FormatByContentType.ContainsKey(contentType);

    public static bool IsAllowedExtension(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) && FormatByExtension.ContainsKey(Path.GetExtension(fileName));

    public static MediaFormat? FormatForContentType(string contentType) =>
        FormatByContentType.TryGetValue(contentType, out var format) ? format : null;

    public static MediaFormat? FormatForExtension(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName) ? null
        : FormatByExtension.TryGetValue(Path.GetExtension(fileName), out var format) ? format : null;

    public static string ContentTypeFor(MediaFormat format) => ContentTypeByFormat[format];

    public static string ExtensionFor(MediaFormat format) => ExtensionByFormat[format];
}

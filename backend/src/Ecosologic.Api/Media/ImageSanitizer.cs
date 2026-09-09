using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace Ecosologic.Api.Media;

public sealed class ImageSanitizer : IImageSanitizer
{
    // Limita decodes/reencodes simultâneos no processo para evitar picos de memória.
    // Uploads são fotos administrativas; dois decodes concorrentes bastam para a demanda
    // sem permitir que requests ilimitados consumam memória durante Identify/Load/encode.
    internal const int MaxConcurrentImageDecodes = 2;

    private static readonly bool WebPSupported = IsWebPSupported();

    private readonly SemaphoreSlim _decodeGate = new(MaxConcurrentImageDecodes, MaxConcurrentImageDecodes);

    internal SemaphoreSlim DecodeGate => _decodeGate;

    public async Task<SanitizeResult> SanitizeAsync(Stream input, CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return SanitizeResult.Fail("Não foi possível ler o arquivo.");
        }

        var detected = MediaFormatDetector.Detect(bytes);
        if (detected is null)
            return SanitizeResult.Fail("Formato de imagem não reconhecido. Use JPG, PNG ou WebP.");

        if (detected == MediaFormat.WebP && !WebPSupported)
            return SanitizeResult.Fail("Formato WebP não suportado neste servidor.");

        await _decodeGate.WaitAsync(cancellationToken);
        try
        {
            var options = new DecoderOptions { SkipMetadata = true };
            using var source = new MemoryStream(bytes, writable: false);

            var info = await Image.IdentifyAsync(options, source, cancellationToken);
            var frameLimitError = MediaConstraints.ValidateFrameCount(info.FrameMetadataCollection.Count);
            if (frameLimitError is not null)
                return SanitizeResult.Fail(frameLimitError);

            var limitError = MediaConstraints.ValidatePixelLimits(info.Width, info.Height);
            if (limitError is not null)
                return SanitizeResult.Fail(limitError);

            source.Position = 0;
            using var image = await Image.LoadAsync(options, source, cancellationToken);
            if (!Matches(image.Metadata.DecodedImageFormat, detected.Value))
                return SanitizeResult.Fail("O conteúdo da imagem não corresponde à assinatura declarada.");

            StripMetadata(image);

            using var output = new MemoryStream();
            await image.SaveAsync(output, EncoderFor(detected.Value), cancellationToken);

            return SanitizeResult.Ok(new SanitizedMedia(
                detected.Value,
                MediaConstraints.ContentTypeFor(detected.Value),
                MediaConstraints.ExtensionFor(detected.Value),
                output.ToArray()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return SanitizeResult.Fail("Imagem inválida, truncada ou corrompida.");
        }
        finally
        {
            _decodeGate.Release();
        }
    }

    private static void StripMetadata(Image image)
    {
        image.Metadata.ExifProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IccProfile = null;

        foreach (var frame in image.Frames)
        {
            frame.Metadata.ExifProfile = null;
            frame.Metadata.IptcProfile = null;
            frame.Metadata.XmpProfile = null;
            frame.Metadata.IccProfile = null;
        }
    }

    private static IImageEncoder EncoderFor(MediaFormat format) => format switch
    {
        MediaFormat.Jpeg => new JpegEncoder { Quality = 90 },
        MediaFormat.Png => new PngEncoder(),
        MediaFormat.WebP => new WebpEncoder { Quality = 90 },
        _ => throw new NotSupportedException($"Formato não suportado: {format}.")
    };

    private static bool Matches(IImageFormat? decoded, MediaFormat expected)
    {
        MediaFormat? actual = decoded switch
        {
            JpegFormat => MediaFormat.Jpeg,
            PngFormat => MediaFormat.Png,
            WebpFormat => MediaFormat.WebP,
            _ => null
        };
        return actual == expected;
    }

    private static bool IsWebPSupported()
    {
        try
        {
            return SixLabors.ImageSharp.Configuration.Default.ImageFormatsManager.TryFindFormatByFileExtension("webp", out _);
        }
        catch
        {
            return false;
        }
    }
}

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Ecosologic.Api.Tests;

internal static class TestImageFactory
{
    public static byte[] Png(int width = 2, int height = 2)
    {
        using var image = new Image<Rgba32>(width, height, Color.Red);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    public static byte[] Jpeg(int width = 2, int height = 2)
    {
        using var image = new Image<Rgba32>(width, height, Color.Blue);
        using var stream = new MemoryStream();
        image.Save(stream, new JpegEncoder());
        return stream.ToArray();
    }

    public static byte[] WebP()
    {
        using var image = new Image<Rgba32>(2, 2, Color.Green);
        using var stream = new MemoryStream();
        image.Save(stream, new WebpEncoder());
        return stream.ToArray();
    }

    public static byte[] AnimatedWebP()
    {
        using var image = new Image<Rgba32>(2, 2, Color.Green);
        image.Frames.AddFrame(image.Frames.RootFrame);
        using var stream = new MemoryStream();
        image.Save(stream, new WebpEncoder());
        return stream.ToArray();
    }

    public static byte[] JpegWithExif(string softwareTag)
    {
        using var image = new Image<Rgba32>(2, 2, Color.Blue);
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Software, softwareTag);
        using var stream = new MemoryStream();
        image.Save(stream, new JpegEncoder());
        return stream.ToArray();
    }

    public static byte[] PngHeader(int width, int height)
    {
        var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        void WriteChunk(string type, byte[] data)
        {
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            stream.Write(BitConverter.GetBytes(data.Length).Reverse().ToArray());
            stream.Write(typeBytes);
            stream.Write(data);
            stream.Write(BitConverter.GetBytes(Crc32(typeBytes.Concat(data).ToArray())).Reverse().ToArray());
        }

        var ihdr = new byte[13];
        BitConverter.GetBytes(width).Reverse().ToArray().CopyTo(ihdr, 0);
        BitConverter.GetBytes(height).Reverse().ToArray().CopyTo(ihdr, 4);
        ihdr[8] = 8;
        ihdr[9] = 2;
        WriteChunk("IHDR", ihdr);

        return stream.ToArray();
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
        }

        return ~crc;
    }
}

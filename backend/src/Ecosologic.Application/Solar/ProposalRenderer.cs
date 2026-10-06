using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ecosologic.Application.Solar;

public sealed record ProposalRenderResult(byte[] Content, string Sha256, string TemplateVersion);

public sealed class ProposalRenderer
{
    public ProposalRenderResult Render(string payloadJson, string templateVersion)
    {
        payloadJson = RequireJson(payloadJson);
        if (string.IsNullOrWhiteSpace(templateVersion))
            throw new ArgumentException("Versão do template é obrigatória.", nameof(templateVersion));

        var title = ExtractTitle(payloadJson);
        var content = BuildPdf(title, templateVersion);
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new ProposalRenderResult(content, hash, templateVersion.Trim());
    }

    private static byte[] BuildPdf(string title, string templateVersion)
    {
        var streamText = $"BT /F1 20 Tf 72 720 Td ({Escape(title)}) Tj /F1 10 Tf 0 -28 Td (Ecosologic - proposta comercial) Tj 0 -18 Td (Template: {Escape(templateVersion.Trim())}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(streamText)} >>\nstream\n{streamText}\nendstream"
        };

        using var output = new MemoryStream();
        Write(output, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(output.Position);
            Write(output, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = output.Position;
        Write(output, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            Write(output, $"{offset:D10} 00000 n \n");
        Write(output, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        return output.ToArray();
    }

    private static string RequireJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Payload é obrigatório.", nameof(value));
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                throw new ArgumentException("Payload deve ser um objeto ou array JSON.", nameof(value));
            return value;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Payload não é um JSON válido.", nameof(value), exception);
        }
    }

    private static string ExtractTitle(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("title", out var title)
            && title.ValueKind == JsonValueKind.String
            ? title.GetString() ?? "Proposta Ecosologic"
            : "Proposta Ecosologic";
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static void Write(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}

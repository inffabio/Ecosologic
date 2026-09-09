using System.Text.Json;

namespace Ecosologic.Domain.Solar;

internal static class SolarValidation
{
    public static void RequireGuid(Guid value, string name, string message)
    {
        if (value == Guid.Empty)
            throw new ArgumentException(message, name);
    }

    public static string RequireText(string? value, string name, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message, name);
        return value.Trim();
    }

    public static void RequireMoney(decimal value, string name)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(name, "Valor monetário não pode ser negativo.");
        if (GetScale(value) > 2)
            throw new ArgumentOutOfRangeException(name, "Valor monetário deve ter no máximo 2 casas decimais.");
    }

    public static void RequirePercent(decimal value, string name)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(name, "Percentual não pode ser negativo.");
        if (GetScale(value) > 4)
            throw new ArgumentOutOfRangeException(name, "Percentual deve ter no máximo 4 casas decimais.");
    }

    public static string RequireJson(string? value, string name, string message)
    {
        var text = RequireText(value, name, message);

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                throw new ArgumentException($"{name} deve ser um objeto ou array JSON.", name);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"{name} não é um JSON válido.", name, ex);
        }

        return text;
    }

    private static int GetScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;
}

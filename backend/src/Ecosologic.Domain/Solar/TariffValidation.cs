namespace Ecosologic.Domain.Solar;

internal static class TariffValidation
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

    public static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static decimal RequireTariffValue(decimal value, string name)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(name, "Valor tarifário não pode ser negativo.");
        if (GetScale(value) > 8)
            throw new ArgumentOutOfRangeException(name, "Valor tarifário deve ter no máximo 8 casas decimais.");
        return value;
    }

    public static decimal RequirePercent(decimal value, string name)
    {
        if (value < 0 || value > 100)
            throw new ArgumentOutOfRangeException(name, "Percentual deve estar entre 0 e 100.");
        if (GetScale(value) > 4)
            throw new ArgumentOutOfRangeException(name, "Percentual deve ter no máximo 4 casas decimais.");
        return value;
    }

    public static void RequireValidity(DateOnly start, DateOnly? end)
    {
        if (end.HasValue && end.Value <= start)
            throw new ArgumentOutOfRangeException(nameof(end), "Vigência final deve ser posterior à vigência inicial.");
    }

    public static int RequireReferenceYear(int year)
    {
        if (year < 2000 || year > 2200)
            throw new ArgumentOutOfRangeException(nameof(year), "Ano de referência fora do intervalo esperado.");
        return year;
    }

    private static int GetScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;
}

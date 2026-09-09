namespace Ecosologic.Domain.Solar;

/// <summary>
/// Validações do motor técnico: dimensões (12 meses), unidades e domínio dos valores.
/// Rejeita NaN/Infinity, negativos e intervalos inválidos antes do cálculo.
/// </summary>
internal static class SolarSizingValidation
{
    /// <summary>
    /// Helper finito compartilhado: rejeita NaN/Infinity antes de qualquer validação de domínio.
    /// Todas as demais validações numéricas do motor delegam aqui para manter uma única fonte
    /// de verdade sobre finitude (evita overflow silencioso e comparações `Infinity > Infinity`).
    /// </summary>
    public static double RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, "Valor deve ser finito (não pode ser NaN ou Infinity).");
        return value;
    }

    public static double RequireFiniteNonNegative(double value, string name)
    {
        RequireFinite(value, name);
        if (value < 0)
            throw new ArgumentOutOfRangeException(name, "Valor não pode ser negativo.");
        return value;
    }

    public static double RequireFinitePositive(double value, string name)
    {
        RequireFinite(value, name);
        if (value <= 0)
            throw new ArgumentOutOfRangeException(name, "Valor deve ser positivo.");
        return value;
    }

    public static double RequireLoss(double value, string name)
    {
        RequireFinite(value, name);
        if (value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name, "Perda deve estar entre 0 e 1 (inclusive).");
        return value;
    }

    public static double RequireDerating(double value, string name)
    {
        RequireFinite(value, name);
        if (value <= 0 || value > 1)
            throw new ArgumentOutOfRangeException(name, "Derating deve estar no intervalo (0, 1].");
        return value;
    }

    public static IReadOnlyList<double> RequireMonthlySeries(IReadOnlyList<double>? values, string name)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count != 12)
            throw new ArgumentException($"{name} deve conter exatamente 12 valores (um por mês).", name);

        var copy = new double[12];
        for (var i = 0; i < 12; i++)
            copy[i] = RequireFiniteNonNegative(values[i], $"{name}[{i}]");

        return Array.AsReadOnly(copy);
    }

    public static string RequireText(string? value, string name, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message, name);
        return value.Trim();
    }
}

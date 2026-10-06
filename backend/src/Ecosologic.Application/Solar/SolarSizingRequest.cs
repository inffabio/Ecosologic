using System.Text.Json;

namespace Ecosologic.Application.Solar;

public sealed class SolarSizingRequest
{
    public Guid LeadId { get; set; }
    public string Distributor { get; set; } = "";
    public string Group { get; set; } = "";
    public string Modality { get; set; } = "";
    public string Connection { get; set; } = "";
    public IReadOnlyList<decimal>? MonthlyConsumptionKWh { get; set; }
    public IReadOnlyList<decimal>? MonthlyBillAmount { get; set; }
    public Guid? ModuleMaterialId { get; set; }
    public Guid? InverterMaterialId { get; set; }
    public DateOnly? ProtocolDate { get; set; }
    public string Address { get; set; } = "";
    public string AssumptionsJson { get; set; } = "{}";

    public void Validate()
    {
        if (LeadId == Guid.Empty)
            throw new ArgumentException("LeadId é obrigatório.", nameof(LeadId));

        Distributor = RequireText(Distributor, nameof(Distributor));
        Group = RequireText(Group, nameof(Group));
        Modality = RequireText(Modality, nameof(Modality));
        Connection = RequireText(Connection, nameof(Connection));
        Address = RequireText(Address, nameof(Address));

        if (ProtocolDate is null)
            throw new ArgumentException("Data de protocolo é obrigatória.", nameof(ProtocolDate));

        ValidateMonthlySeries(MonthlyConsumptionKWh, nameof(MonthlyConsumptionKWh), requireMoney: false);
        ValidateMonthlySeries(MonthlyBillAmount, nameof(MonthlyBillAmount), requireMoney: true);
        AssumptionsJson = RequireJson(AssumptionsJson, nameof(AssumptionsJson));
    }

    private static string RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Campo obrigatório.", name);
        return value.Trim();
    }

    private static void ValidateMonthlySeries(IReadOnlyList<decimal>? values, string name, bool requireMoney)
    {
        if (values is null || values.Count != 12)
            throw new ArgumentException("A série deve conter exatamente 12 valores.", name);

        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (value < 0)
                throw new ArgumentOutOfRangeException(name, $"{name}[{index}] não pode ser negativo.");
            if (requireMoney && decimal.Round(value, 2) != value)
                throw new ArgumentOutOfRangeException(name, $"{name}[{index}] deve ter no máximo 2 casas decimais.");
        }
    }

    private static string RequireJson(string? value, string name)
    {
        var text = RequireText(value, name);
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
}

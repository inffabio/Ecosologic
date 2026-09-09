namespace Ecosologic.Domain.Solar;

public enum SolarSizingStatus
{
    Draft,
    Calculated,
    Approved,
    Cancelled
}

public sealed class SolarSizing
{
    private SolarSizing(
        Guid leadId,
        string concessionaria,
        string grupo,
        string modalidade,
        string engineVersion,
        string inputsJson)
    {
        Id = Guid.NewGuid();
        LeadId = leadId;
        Concessionaria = concessionaria;
        Grupo = grupo;
        Modalidade = modalidade;
        EngineVersion = engineVersion;
        InputsJson = inputsJson;
        Status = SolarSizingStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; }
    public Guid LeadId { get; }
    public string Concessionaria { get; }
    public string Grupo { get; }
    public string Modalidade { get; }
    public string EngineVersion { get; }
    public string InputsJson { get; }
    public string? ResultsJson { get; private set; }
    public SolarSizingStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static SolarSizing CreateDraft(
        Guid leadId,
        string concessionaria,
        string grupo,
        string modalidade,
        string engineVersion,
        string inputsJson)
    {
        SolarValidation.RequireGuid(leadId, nameof(leadId), "LeadId é obrigatório.");
        concessionaria = SolarValidation.RequireText(concessionaria, nameof(concessionaria), "Concessionária é obrigatória.");
        grupo = SolarValidation.RequireText(grupo, nameof(grupo), "Grupo é obrigatório.");
        modalidade = SolarValidation.RequireText(modalidade, nameof(modalidade), "Modalidade é obrigatória.");
        engineVersion = SolarValidation.RequireText(engineVersion, nameof(engineVersion), "Versão do motor é obrigatória.");
        inputsJson = SolarValidation.RequireJson(inputsJson, nameof(inputsJson), "Snapshot de entradas é obrigatório.");

        return new SolarSizing(leadId, concessionaria, grupo, modalidade, engineVersion, inputsJson);
    }

    public void Calculate(string resultsJson)
    {
        resultsJson = SolarValidation.RequireJson(resultsJson, nameof(resultsJson), "Snapshot de resultados é obrigatório.");

        TransitionTo(SolarSizingStatus.Calculated);
        ResultsJson = resultsJson;
        Touch();
    }

    public void Approve()
    {
        TransitionTo(SolarSizingStatus.Approved);
        Touch();
    }

    public void Cancel()
    {
        TransitionTo(SolarSizingStatus.Cancelled);
        Touch();
    }

    public bool CanTransitionTo(SolarSizingStatus target) =>
        SolarTransitions.CanSizingTransition(Status, target);

    private void TransitionTo(SolarSizingStatus target)
    {
        if (!CanTransitionTo(target))
            throw new InvalidOperationException($"Transição inválida de SolarSizing {Status} para {target}.");
        Status = target;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}

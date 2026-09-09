namespace Ecosologic.Domain.Crm;

public enum LeadStage
{
    New,
    Contacted,
    DataReceived,
    Dimensioning,
    ProposalSent,
    Negotiation,
    Won,
    Lost
}

public sealed class Lead
{
    private Lead(string name, string phone, string email, string message)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
        Phone = phone.Trim();
        Email = email.Trim();
        Message = message.Trim();
        Stage = LeadStage.New;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string Phone { get; }
    public string Email { get; }
    public string Message { get; }
    public LeadStage Stage { get; }
    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyList<string> Errors { get; private set; } = [];
    public bool IsValid => Errors.Count == 0;

    public static Lead Create(string name, string phone, string email, string message)
    {
        var lead = new Lead(name, phone, email, message);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name)) errors.Add("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(phone)) errors.Add("WhatsApp é obrigatório.");
        lead.Errors = errors;
        return lead;
    }

    public static bool TryParseStage(string? value, out LeadStage stage)
    {
        stage = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var candidate = value.Trim();
        foreach (var name in Enum.GetNames<LeadStage>())
        {
            if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase))
            {
                stage = Enum.Parse<LeadStage>(name);
                return true;
            }
        }

        return false;
    }
}

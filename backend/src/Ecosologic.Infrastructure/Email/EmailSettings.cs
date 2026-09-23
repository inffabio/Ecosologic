namespace Ecosologic.Infrastructure.Email;

public sealed class EmailSettings
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Ecosologic";
    public string LeadRecipient { get; set; } = "";

    public bool IsConfigured => Enabled
        && !string.IsNullOrWhiteSpace(SmtpHost)
        && SmtpPort > 0
        && !string.IsNullOrWhiteSpace(SmtpUsername)
        && !string.IsNullOrWhiteSpace(SmtpPassword)
        && !string.IsNullOrWhiteSpace(FromAddress)
        && !string.IsNullOrWhiteSpace(LeadRecipient);
}

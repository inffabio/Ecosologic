using Ecosologic.Domain.Crm;

namespace Ecosologic.Domain.Tests;

public class LeadTests
{
    [Fact]
    public void Create_requires_name_and_phone()
    {
        var lead = Lead.Create("", "", "", "Contato");

        Assert.False(lead.IsValid);
        Assert.Contains(lead.Errors, error => error.StartsWith("Nome"));
        Assert.Contains(lead.Errors, error => error.StartsWith("WhatsApp"));
    }

    [Fact]
    public void Create_starts_in_new_stage()
    {
        var lead = Lead.Create("Fabio", "+5521995424027", "fabio@ecosologic.com.br", "Quero um orçamento");

        Assert.True(lead.IsValid);
        Assert.Equal(LeadStage.New, lead.Stage);
    }
}

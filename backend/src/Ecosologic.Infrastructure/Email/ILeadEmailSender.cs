using Ecosologic.Infrastructure.Persistence;

namespace Ecosologic.Infrastructure.Email;

public interface ILeadEmailSender
{
    Task SendAsync(LeadRecord lead, CancellationToken cancellationToken);
}

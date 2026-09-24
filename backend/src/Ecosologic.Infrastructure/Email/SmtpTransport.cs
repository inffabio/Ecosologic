using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Ecosologic.Infrastructure.Email;

public interface ISmtpTransport : IAsyncDisposable
{
    Task ConnectAsync(string host, int port, SecureSocketOptions security, CancellationToken cancellationToken);
    Task AuthenticateAsync(string username, string password, CancellationToken cancellationToken);
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken);
    Task DisconnectAsync(bool quit, CancellationToken cancellationToken);
}

public interface ISmtpTransportFactory
{
    ISmtpTransport Create();
}

public sealed class MailKitSmtpTransportFactory : ISmtpTransportFactory
{
    public ISmtpTransport Create() => new MailKitSmtpTransport(new SmtpClient());
}

public sealed class MailKitSmtpTransport(SmtpClient client) : ISmtpTransport
{
    public Task ConnectAsync(string host, int port, SecureSocketOptions security, CancellationToken cancellationToken) =>
        client.ConnectAsync(host, port, security, cancellationToken);

    public Task AuthenticateAsync(string username, string password, CancellationToken cancellationToken) =>
        client.AuthenticateAsync(username, password, cancellationToken);

    public Task SendAsync(MimeMessage message, CancellationToken cancellationToken) =>
        client.SendAsync(message, cancellationToken);

    public Task DisconnectAsync(bool quit, CancellationToken cancellationToken) =>
        client.DisconnectAsync(quit, cancellationToken);

    public ValueTask DisposeAsync()
    {
        client.Dispose();
        return ValueTask.CompletedTask;
    }
}

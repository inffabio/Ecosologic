using Ecosologic.Infrastructure.Email;
using Ecosologic.Infrastructure.Persistence;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Ecosologic.Api.Tests;

public sealed class LeadEmailSenderTests
{
    [Fact]
    public async Task Sends_internal_notification_over_start_tls_after_authentication()
    {
        var transport = new RecordingSmtpTransport();
        var sender = new LeadEmailSender(
            Options.Create(new EmailSettings
            {
                Enabled = true,
                SmtpHost = "smtp.example.com",
                SmtpPort = 587,
                SmtpUsername = "smtp-user",
                SmtpPassword = "smtp-token",
                FromAddress = "contato@example.com",
                FromName = "Example",
                LeadRecipient = "owner@example.com"
            }),
            NullLogger<LeadEmailSender>.Instance,
            new RecordingSmtpTransportFactory(transport));

        await sender.SendAsync(new LeadRecord
        {
            Id = Guid.NewGuid(),
            Name = "Ana",
            Phone = "21999999999",
            Message = "Quero um orçamento."
        }, CancellationToken.None);

        Assert.Equal(("smtp.example.com", 587, SecureSocketOptions.StartTls), transport.Connection);
        Assert.Equal(("smtp-user", "smtp-token"), transport.Authentication);
        var message = Assert.Single(transport.Messages);
        Assert.Equal("owner@example.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("contato@example.com", message.From.Mailboxes.Single().Address);
    }

    private sealed class RecordingSmtpTransportFactory(RecordingSmtpTransport transport) : ISmtpTransportFactory
    {
        public ISmtpTransport Create() => transport;
    }

    private sealed class RecordingSmtpTransport : ISmtpTransport
    {
        public (string Host, int Port, SecureSocketOptions Security)? Connection { get; private set; }
        public (string Username, string Password)? Authentication { get; private set; }
        public List<MimeMessage> Messages { get; } = [];

        public Task ConnectAsync(string host, int port, SecureSocketOptions security, CancellationToken cancellationToken)
        {
            Connection = (host, port, security);
            return Task.CompletedTask;
        }

        public Task AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
        {
            Authentication = (username, password);
            return Task.CompletedTask;
        }

        public Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(bool quit, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

using System.Net.Mail;
using System.Text.Encodings.Web;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

using Ecosologic.Infrastructure.Persistence;

namespace Ecosologic.Infrastructure.Email;

public sealed class LeadEmailSender(
    IOptions<EmailSettings> options,
    ILogger<LeadEmailSender> logger,
    ISmtpTransportFactory transportFactory) : ILeadEmailSender
{
    private const string Logo = "<img src='https://www.ecosologic.com.br/assets/brand/email-logo.png' width='220' height='58' alt='Ecosologic' style='display:block;width:220px;max-width:100%;height:auto;border:0' />";

    public async Task SendAsync(LeadRecord lead, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            logger.LogWarning("Lead email is not configured; lead {LeadId} was saved without email delivery.", lead.Id);
            return;
        }

        await using var transport = transportFactory.Create();
        await transport.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.StartTls, cancellationToken);
        try
        {
            await transport.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword, cancellationToken);
            await transport.SendAsync(BuildInternalMessage(settings, lead), cancellationToken);

            if (IsValidEmail(lead.Email))
                await transport.SendAsync(BuildCustomerMessage(settings, lead), cancellationToken);

            logger.LogInformation("Lead email notification sent for lead {LeadId}.", lead.Id);
        }
        finally
        {
            await transport.DisconnectAsync(true, cancellationToken);
        }
    }

    private static MimeMessage BuildInternalMessage(EmailSettings settings, LeadRecord lead)
    {
        var message = NewMessage(settings, settings.LeadRecipient, "Novo contato pelo site Ecosologic");
        if (IsValidEmail(lead.Email))
            message.ReplyTo.Add(MailboxAddress.Parse(lead.Email));

        message.Body = new BodyBuilder
        {
            HtmlBody = $"""
                <!doctype html>
                <html><body style="margin:0;background:#eef6ed;font-family:Arial,sans-serif;color:#102522">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="padding:32px 12px;background:#eef6ed">
                    <tr><td align="center"><table role="presentation" width="620" cellpadding="0" cellspacing="0" style="max-width:620px;background:#fff;border-radius:18px;overflow:hidden">
                      <tr><td style="padding:24px;background:#06211f">{Logo}</td></tr>
                      <tr><td style="padding:32px"><p style="margin:0 0 8px;color:#0b8f67;font-size:12px;font-weight:bold;letter-spacing:1px;text-transform:uppercase">Novo lead</p>
                        <h1 style="margin:0 0 24px;font-size:28px;color:#06211f">Solicitação de orçamento</h1>
                        <p style="margin:8px 0"><strong>Nome:</strong> {Encode(lead.Name)}</p>
                        <p style="margin:8px 0"><strong>WhatsApp:</strong> {Encode(lead.Phone)}</p>
                        <p style="margin:8px 0"><strong>E-mail:</strong> {Encode(lead.Email)}</p>
                        <div style="margin-top:24px;padding:18px;background:#f3f8ef;border-radius:12px;white-space:pre-line">{Encode(lead.Message)}</div>
                      </td></tr>
                    </table></td></tr>
                  </table>
                </body></html>
                """
        }.ToMessageBody();
        return message;
    }

    private static MimeMessage BuildCustomerMessage(EmailSettings settings, LeadRecord lead)
    {
        var message = NewMessage(settings, lead.Email, "Recebemos sua solicitação | Ecosologic");
        message.Body = new BodyBuilder
        {
            HtmlBody = $"""
                <!doctype html>
                <html><body style="margin:0;background:#eef6ed;font-family:Arial,sans-serif;color:#102522">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="padding:32px 12px;background:#eef6ed">
                    <tr><td align="center"><table role="presentation" width="620" cellpadding="0" cellspacing="0" style="max-width:620px;background:#fff;border-radius:18px;overflow:hidden">
                      <tr><td style="padding:24px;background:#06211f">{Logo}</td></tr>
                      <tr><td style="padding:40px 32px"><p style="margin:0 0 8px;color:#0b8f67;font-size:12px;font-weight:bold;letter-spacing:1px;text-transform:uppercase">Solicitação recebida</p>
                        <h1 style="margin:0 0 20px;font-size:30px;color:#06211f">Olá, {Encode(lead.Name)}.</h1>
                        <p style="font-size:16px;line-height:1.6">Recebemos seus dados e já vamos analisar o melhor caminho para o seu projeto de energia solar.</p>
                        <div style="margin:28px 0;padding:22px;border-radius:14px;background:#d8ff3e;color:#06211f"><strong style="font-size:19px">Em breve estaremos respondendo.</strong><br><span style="display:inline-block;margin-top:8px;font-size:14px">Nossa equipe entrará em contato para continuar a conversa.</span></div>
                        <p style="font-size:14px;line-height:1.6;color:#5c6b67">Se preferir, fale diretamente conosco pelo WhatsApp e mencione que você preencheu o formulário no site.</p>
                      </td></tr>
                      <tr><td style="padding:18px 32px;background:#f3f8ef;color:#5c6b67;font-size:12px">Ecosologic · Energia distribuída, solar fotovoltaica e soluções híbridas.</td></tr>
                    </table></td></tr>
                  </table>
                </body></html>
                """
        }.ToMessageBody();
        return message;
    }

    private static MimeMessage NewMessage(EmailSettings settings, string recipient, string subject)
    {
        var message = new MimeMessage
        {
            Subject = subject
        };
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        return message;
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try { _ = new MailAddress(email); return true; }
        catch (FormatException) { return false; }
    }

    private static string Encode(string? value) => HtmlEncoder.Default.Encode(value ?? "");
}

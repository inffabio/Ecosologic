# MailKit SMTP Transport Design

## Context

The public lead endpoint saves submissions, but OCI Email Delivery rejects the
current `System.Net.Mail.SmtpClient` transport with `Authentication required`.
The form and database flow are otherwise working.

## Design

Keep `ILeadEmailSender` as the application-facing boundary. Replace the native
SMTP implementation with MailKit using `StartTls` on the configured SMTP port.
Wrap the MailKit client behind a small transport interface and factory so the
sender can be tested without a live SMTP server.

The sender will connect, authenticate with the configured SMTP username and
auth token, send the internal notification (and optional customer reply), then
disconnect in a `finally` path. Existing lead persistence behavior remains:
SMTP failures are logged after the lead has been saved and do not roll back the
lead.

## Verification

- Unit tests verify StartTLS selection, authentication, recipients, and send.
- Existing API tests and solution build must remain green.
- Production verification submits one controlled lead and checks the API log
  for a successful MailKit delivery message.

## Scope

No changes to the frontend form, database schema, SMTP secrets, or lead data
model are required.

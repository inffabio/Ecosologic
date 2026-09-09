using System.Globalization;
using System.Text;
using Ecosologic.Infrastructure.Persistence;

namespace Ecosologic.Infrastructure.Crm;

/// <summary>
/// Cursor opaco e estável para paginação por chave (DueAt, Id). Não depende do estado
/// de leitura, então marcar notificações como lidas não invalida a paginação.
/// </summary>
public static class CrmNotificationCursor
{
    private const char Separator = '|';

    public static string? Create(CrmNotificationRecord? last)
    {
        if (last is null)
            return null;

        var raw = $"{last.DueAt.ToString("O", CultureInfo.InvariantCulture)}{Separator}{last.Id:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static (DateTimeOffset DueAt, Guid Id) Parse(string cursor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cursor);

        var base64 = cursor.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');

        string raw;
        try
        {
            raw = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Cursor inválido.", nameof(cursor), exception);
        }

        var separator = raw.LastIndexOf(Separator);
        if (separator <= 0)
            throw new ArgumentException("Cursor inválido.", nameof(cursor));

        try
        {
            var dueAt = DateTimeOffset.Parse(raw[..separator], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var id = Guid.ParseExact(raw[(separator + 1)..], "N");
            return (dueAt, id);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Cursor inválido.", nameof(cursor), exception);
        }
    }
}

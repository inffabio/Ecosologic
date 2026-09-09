using System.Security.Cryptography;
using System.Text;

namespace Ecosologic.Domain.Auth;

public sealed class AdminCredentialValidator
{
    private readonly string _email;
    private readonly byte[] _password;

    public AdminCredentialValidator(string email, string password)
    {
        _email = (email ?? "").Trim();
        _password = Encoding.UTF8.GetBytes(password ?? "");
    }

    public bool Validate(string? email, string? password)
    {
        if (email is null || password is null)
            return false;

        if (!string.Equals(_email, email.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        var actual = Encoding.UTF8.GetBytes(password);
        return _password.Length == actual.Length
            && CryptographicOperations.FixedTimeEquals(_password, actual);
    }
}

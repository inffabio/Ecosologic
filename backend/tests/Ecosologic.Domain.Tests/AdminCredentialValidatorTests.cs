using Ecosologic.Domain.Auth;

namespace Ecosologic.Domain.Tests;

public class AdminCredentialValidatorTests
{
    private const string Email = "admin@ecosologic.com.br";
    private const string Password = "s3nh4-f0rte";

    [Fact]
    public void Validate_accepts_correct_credentials()
    {
        var validator = new AdminCredentialValidator(Email, Password);

        Assert.True(validator.Validate(Email, Password));
    }

    [Fact]
    public void Validate_rejects_wrong_password()
    {
        var validator = new AdminCredentialValidator(Email, Password);

        Assert.False(validator.Validate(Email, "senha-errada"));
    }

    [Fact]
    public void Validate_rejects_unknown_email()
    {
        var validator = new AdminCredentialValidator(Email, Password);

        Assert.False(validator.Validate("outro@ecosologic.com.br", Password));
    }

    [Fact]
    public void Validate_rejects_missing_values()
    {
        var validator = new AdminCredentialValidator(Email, Password);

        Assert.False(validator.Validate(null, Password));
        Assert.False(validator.Validate(Email, null));
        Assert.False(validator.Validate("", ""));
    }
}

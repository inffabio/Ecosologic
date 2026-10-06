using Oci.Common.Auth;
using Oci.SecretsService;
using Oci.SecretsService.Models;
using Oci.SecretsService.Requests;
using Microsoft.Extensions.Configuration;

namespace Ecosologic.Infrastructure.Secrets;

public sealed class OciSecretReader(
    IConfiguration configuration,
    InternalSecretsOptions options) : IOciSecretReader, IDisposable
{
    private readonly SecretsClient client = CreateClient(configuration);

    public async Task<string> ReadAsync(string secretName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.VaultOcid))
            throw new InvalidOperationException("InternalSecrets:VaultOcid é obrigatório.");

        var response = await client.GetSecretBundleByName(
            new GetSecretBundleByNameRequest
            {
                VaultId = options.VaultOcid,
                SecretName = secretName,
                Stage = GetSecretBundleByNameRequest.StageEnum.Current
            },
            cancellationToken: cancellationToken);

        var content = (response.SecretBundle?.SecretBundleContent as Base64SecretBundleContentDetails)?.Content;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("OCI retornou um segredo vazio.");

        return DecodeContent(content);
    }

    public void Dispose() => client.Dispose();

    private static SecretsClient CreateClient(IConfiguration configuration)
    {
        var configPath = configuration["OCI:ConfigFile"] ?? configuration["OCI_CONFIG_FILE"];
        if (string.IsNullOrWhiteSpace(configPath))
            throw new InvalidOperationException("OCI:ConfigFile é obrigatório.");

        var profile = configuration["OCI:Profile"] ?? "DEFAULT";
        var provider = new ConfigFileAuthenticationDetailsProvider(configPath, profile);
        return new SecretsClient(provider);
    }

    private static string DecodeContent(string content)
    {
        try
        {
            return Convert.ToBase64String(Convert.FromBase64String(content)) == content
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(content))
                : content;
        }
        catch (FormatException)
        {
            return content;
        }
    }
}

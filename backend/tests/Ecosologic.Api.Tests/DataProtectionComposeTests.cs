namespace Ecosologic.Api.Tests;

public class DataProtectionComposeTests
{
    private const string ComposeFileName = "docker-compose.server.yml";
    private const string ServiceName = "api";
    private const string VolumeKey = "api-data-protection-keys";
    private const string ExpectedTarget = "/root/.aspnet/DataProtection-Keys";
    private const string ExpectedVolumeName = "ecosologic_api_data_protection_keys";
    private static readonly string ProgramRelativePath = Path.Combine("backend", "src", "Ecosologic.Api", "Program.cs");

    private static string LocateComposeFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ComposeFileName);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Nao foi possivel localizar {ComposeFileName} a partir de {AppContext.BaseDirectory}.");
    }

    private static string LocateProgramFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ProgramRelativePath);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Nao foi possivel localizar {ProgramRelativePath} a partir de {AppContext.BaseDirectory}.");
    }

    [Fact]
    public void Api_service_mounts_the_data_protection_keys_volume_at_the_expected_target()
    {
        var compose = File.ReadAllText(LocateComposeFile());
        var volumes = ParseServiceVolumes(compose, ServiceName);

        var entry = volumes.SingleOrDefault(v => v.Target == ExpectedTarget);

        Assert.NotNull(entry);
        Assert.Equal(VolumeKey, entry!.Source);
    }

    [Fact]
    public void The_data_protection_keys_volume_has_a_fixed_docker_name()
    {
        var compose = File.ReadAllText(LocateComposeFile());
        var volumes = ParseTopLevelVolumes(compose);

        Assert.True(volumes.ContainsKey(VolumeKey), $"Volume '{VolumeKey}' nao declarado na secao 'volumes'.");
        Assert.Equal(ExpectedVolumeName, volumes[VolumeKey].Name);
    }

    [Fact]
    public void Program_explicitly_configures_data_protection_to_persist_keys_to_the_volume_target()
    {
        var source = File.ReadAllText(LocateProgramFile());

        Assert.Contains("AddDataProtection", source);
        Assert.Contains("PersistKeysToFileSystem", source);
        Assert.Contains("DataProtectionKeys.Resolve", source);
    }

    private sealed record VolumeMount(string Source, string Target);

    private sealed record VolumeDefinition(string? Name);

    private static IReadOnlyList<string> NormalizeLines(string compose) =>
        compose.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static IReadOnlyList<VolumeMount> ParseServiceVolumes(string compose, string serviceName)
    {
        var lines = NormalizeLines(compose);

        var serviceStart = lines.ToList().IndexOf($"  {serviceName}:");
        Assert.True(serviceStart >= 0, $"Servico '{serviceName}' nao encontrado no compose.");

        var volumesIndex = -1;
        for (var i = serviceStart + 1; i < lines.Count; i++)
        {
            var line = lines[i];

            if (line.Length > 0 && line.StartsWith("  ", StringComparison.Ordinal) &&
                !line.StartsWith("    ", StringComparison.Ordinal))
            {
                break;
            }

            if (line == "    volumes:")
            {
                volumesIndex = i;
                break;
            }
        }

        Assert.True(volumesIndex >= 0, $"Servico '{serviceName}' nao declara a secao 'volumes'.");

        var result = new List<VolumeMount>();
        for (var i = volumesIndex + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.StartsWith("      - ", StringComparison.Ordinal))
                break;

            var raw = line["      - ".Length..].Trim();
            result.Add(ParseVolumeMount(raw));
        }

        return result;
    }

    private static VolumeMount ParseVolumeMount(string raw)
    {
        var separator = raw.IndexOf(':');
        Assert.True(separator > 0, $"Mount invalido (esperado 'source:target'): '{raw}'.");

        return new VolumeMount(raw[..separator].Trim(), raw[(separator + 1)..].Trim());
    }

    private static Dictionary<string, VolumeDefinition> ParseTopLevelVolumes(string compose)
    {
        var lines = NormalizeLines(compose);

        var volumesIndex = lines.ToList().IndexOf("volumes:");
        Assert.True(volumesIndex >= 0, "Secao 'volumes' de topo nao encontrada no compose.");

        var result = new Dictionary<string, VolumeDefinition>();
        string? currentKey = null;
        string? name = null;

        for (var i = volumesIndex + 1; i < lines.Count; i++)
        {
            var line = lines[i];

            if (line.Length == 0)
                continue;

            if (line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("    ", StringComparison.Ordinal))
            {
                if (currentKey is not null)
                    result[currentKey] = new VolumeDefinition(name);

                currentKey = line.Trim().TrimEnd(':');
                name = null;
            }
            else if (line.StartsWith("    ", StringComparison.Ordinal))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("name:", StringComparison.Ordinal))
                    name = trimmed["name:".Length..].Trim();
            }
            else
            {
                break;
            }
        }

        if (currentKey is not null)
            result[currentKey] = new VolumeDefinition(name);

        return result;
    }
}

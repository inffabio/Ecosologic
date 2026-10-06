using Ecosologic.Api.Contracts;

namespace Ecosologic.Api.Suppliers;

public static class SolarSupplierImportDeduplicator
{
    public static IReadOnlyList<SolarSupplierImportItem> KeepFirstByName(
        IEnumerable<SolarSupplierImportItem> items,
        IEnumerable<string>? existingNames = null)
    {
        var seen = new HashSet<string>(existingNames ?? [], StringComparer.OrdinalIgnoreCase);
        var result = new List<SolarSupplierImportItem>();

        foreach (var item in items)
        {
            var name = item.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                continue;

            result.Add(item);
        }

        return result;
    }
}

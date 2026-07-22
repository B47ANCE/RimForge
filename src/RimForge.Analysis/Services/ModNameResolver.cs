using RimForge.Core.Models;

namespace RimForge.Analysis.Services;

public interface IModNameResolver
{
    string Resolve(string? packageId);
}

public sealed class ModNameResolver : IModNameResolver
{
    private readonly IReadOnlyDictionary<string, string> _names;

    public ModNameResolver(IEnumerable<ModRecord> mods)
    {
        _names = mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
            .GroupBy(mod => Normalize(mod.PackageId!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().DisplayName,
                StringComparer.OrdinalIgnoreCase);
    }

    public string Resolve(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return "Unknown mod";
        var normalized = Normalize(packageId);
        return _names.TryGetValue(normalized, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : HumanizeFallback(packageId);
    }

    public static string Normalize(string packageId) => packageId.Trim().ToLowerInvariant();

    private static string HumanizeFallback(string packageId)
    {
        var value = packageId.Trim();
        var last = value.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(last) ? value : last;
    }
}

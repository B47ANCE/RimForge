using System.Xml.Linq;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class ExternalProfileReconciliationService : IExternalProfileReconciliationService
{
    public async Task<ExternalProfileSnapshot> ReadAsync(string modsConfigPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modsConfigPath);
        await using var stream = new FileStream(modsConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, true);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);
        var root = document.Root ?? throw new InvalidDataException("ModsConfig.xml has no root element.");
        var active = Distinct(root.Element("activeMods")?.Elements("li").Select(x => x.Value) ?? []);
        var expansions = Distinct(root.Element("knownExpansions")?.Elements("li").Select(x => x.Value) ?? []);
        var version = root.Element("version")?.Value.Trim();
        return new ExternalProfileSnapshot(modsConfigPath, string.IsNullOrWhiteSpace(version) ? "unknown" : version, active, expansions, DateTimeOffset.UtcNow);
    }

    public ExternalProfileReconciliation Compare(RimForgeProfile profile, ExternalProfileSnapshot external)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(external);
        var comparer = StringComparer.OrdinalIgnoreCase;
        var left = Distinct(profile.ActiveMods);
        var right = Distinct(external.ActiveMods);
        var leftSet = left.ToHashSet(comparer);
        var rightSet = right.ToHashSet(comparer);
        var added = right.Where(x => !leftSet.Contains(x)).ToArray();
        var removed = left.Where(x => !rightSet.Contains(x)).ToArray();
        var rightIndexes = right.Select((id, index) => (id,index)).ToDictionary(x => x.id, x => x.index, comparer);
        var order = left.Select((id,index)=>(id,index))
            .Where(x => rightIndexes.TryGetValue(x.id, out var i) && i != x.index)
            .Select(x => new ProfileOrderChange(x.id, x.index, rightIndexes[x.id])).ToArray();
        return new ExternalProfileReconciliation(profile, external, added, removed, order);
    }

    private static IReadOnlyList<string> Distinct(IEnumerable<string> values) => values
        .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}

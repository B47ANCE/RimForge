using System.Security.Cryptography;
using System.Text;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class LibraryProfileProjectionService : ILibraryProfileProjectionService
{
    private static readonly StringComparer PackageComparer = StringComparer.OrdinalIgnoreCase;

    public LibraryProfileWorkspaceSnapshot Create(
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyList<RimForgeProfile> profiles,
        DateTimeOffset? generatedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(installedMods);
        ArgumentNullException.ThrowIfNull(profiles);

        var orderedMods = installedMods
            .OrderBy(mod => Normalize(mod.PackageId), PackageComparer)
            .ThenBy(mod => mod.RootPath, PackageComparer)
            .ToArray();
        var byPackageId = orderedMods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
            .GroupBy(mod => Normalize(mod.PackageId), PackageComparer)
            .ToDictionary(group => group.Key, group => group.ToArray(), PackageComparer);
        var duplicates = byPackageId
            .Where(pair => pair.Value.Length > 1)
            .Select(pair => pair.Key)
            .OrderBy(packageId => packageId, PackageComparer)
            .ToArray();

        var projections = profiles
            .OrderBy(profile => profile.Name, PackageComparer)
            .ThenBy(profile => profile.WorkspacePath, PackageComparer)
            .Select(profile => Project(profile, orderedMods, byPackageId))
            .ToArray();

        return new LibraryProfileWorkspaceSnapshot(
            orderedMods,
            projections,
            duplicates,
            CreateFingerprint(orderedMods, projections),
            generatedUtc ?? DateTimeOffset.UtcNow);
    }

    private static ProfileLibraryProjection Project(
        RimForgeProfile profile,
        IReadOnlyList<ModRecord> installedMods,
        IReadOnlyDictionary<string, ModRecord[]> byPackageId)
    {
        var activeIds = new HashSet<string>(PackageComparer);
        var references = new List<ProfileModReference>(profile.ActiveMods.Count);
        for (var position = 0; position < profile.ActiveMods.Count; position++)
        {
            var packageId = Normalize(profile.ActiveMods[position]);
            activeIds.Add(packageId);
            if (!byPackageId.TryGetValue(packageId, out var matches))
                references.Add(new ProfileModReference(packageId, position, ProfileModResolution.Missing));
            else if (matches.Length > 1)
                references.Add(new ProfileModReference(packageId, position, ProfileModResolution.Ambiguous));
            else
                references.Add(new ProfileModReference(packageId, position, ProfileModResolution.Installed, matches[0]));
        }

        var inactive = installedMods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId) && !activeIds.Contains(Normalize(mod.PackageId)))
            .ToArray();
        return new ProfileLibraryProjection(profile, references, inactive, AssessReadiness(profile, references));
    }

    private static ProfileReadinessSummary AssessReadiness(
        RimForgeProfile profile,
        IReadOnlyList<ProfileModReference> references)
    {
        var missing = references.Count(item => item.Resolution == ProfileModResolution.Missing);
        var ambiguous = references.Count(item => item.Resolution == ProfileModResolution.Ambiguous);
        var duplicateActive = references.GroupBy(item => item.PackageId, PackageComparer)
            .Sum(group => Math.Max(0, group.Count() - 1));
        var incompatible = references.Count(item =>
            item.Mod is { SupportedVersions.Count: > 0 } mod &&
            !mod.SupportedVersions.Contains(profile.Version, PackageComparer));
        var hasCore = references.Any(item =>
            item.Resolution == ProfileModResolution.Installed &&
            item.PackageId.Equals("ludeon.rimworld", StringComparison.OrdinalIgnoreCase));
        var reasons = new List<string>();
        if (!hasCore) reasons.Add("Core is missing or unresolved.");
        if (missing > 0) reasons.Add($"{missing} active package(s) are not installed.");
        if (ambiguous > 0) reasons.Add($"{ambiguous} active package(s) resolve to multiple installations.");
        if (duplicateActive > 0) reasons.Add($"{duplicateActive} duplicate active package entr{(duplicateActive == 1 ? "y" : "ies")} must be removed.");
        if (incompatible > 0) reasons.Add($"{incompatible} active mod(s) do not declare support for RimWorld {profile.Version}.");
        var status = !hasCore || missing > 0 || ambiguous > 0 || duplicateActive > 0
            ? ProfileReadinessStatus.Blocked
            : incompatible > 0 ? ProfileReadinessStatus.Warning : ProfileReadinessStatus.Ready;
        return new(status, missing, ambiguous, duplicateActive, incompatible, hasCore, reasons);
    }

    private static string CreateFingerprint(
        IReadOnlyList<ModRecord> mods,
        IReadOnlyList<ProfileLibraryProjection> profiles)
    {
        var canonical = new StringBuilder();
        foreach (var mod in mods)
            canonical.Append("mod|").Append(Normalize(mod.PackageId)).Append('|').Append(mod.RootPath).AppendLine();
        foreach (var projection in profiles)
        {
            canonical.Append("profile|").Append(projection.Profile.Name).Append('|').Append(projection.Profile.Version).AppendLine();
            foreach (var item in projection.ActiveMods)
                canonical.Append(item.Position).Append('|').Append(item.PackageId).Append('|').Append(item.Resolution).AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static string Normalize(string? packageId) => packageId?.Trim().ToLowerInvariant() ?? string.Empty;
}

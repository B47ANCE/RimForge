namespace RimForge.Core.Models;

public sealed class ModRecord
{
    public required string Id { get; init; }
    public required string RootPath { get; init; }
    public required string FolderName { get; init; }
    public required string AboutPath { get; init; }
    public string? Name { get; init; }
    public string? PackageId { get; init; }
    public string? Author { get; init; }
    public string? Description { get; init; }
    public string? WorkshopId { get; init; }
    public string? WorkshopUrl { get; init; }
    public string? PreviewImagePath { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public IReadOnlyList<string> SupportedVersions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ModDependency> Dependencies { get; init; } = Array.Empty<ModDependency>();
    public IReadOnlyList<string> LoadBefore { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> LoadAfter { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> IncompatibleWith { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public ModEvidence Evidence { get; set; } = ModEvidence.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? FolderName : Name;
    public string DisplayPackageId => string.IsNullOrWhiteSpace(PackageId) ? "Missing package ID" : PackageId;
    public string SupportedVersionsText => SupportedVersions.Count == 0 ? "Unknown" : string.Join(", ", SupportedVersions);
    public int DependencyCount => Dependencies.Count;
    public string HealthText => Errors.Count == 0 ? "Healthy" : $"{Errors.Count} issue(s)";
    public bool HasWorkshop => !string.IsNullOrWhiteSpace(WorkshopId) && !string.IsNullOrWhiteSpace(WorkshopUrl);
    public bool IsOfficialContent => IsOfficialRimWorldPackageId(PackageId);
    public ModSource Source => IsOfficialContent
        ? ModSource.Official
        : HasWorkshop ? ModSource.SteamWorkshop : ModSource.Local;

    public static bool IsOfficialRimWorldPackageId(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return false;
        }

        return packageId.Equals("ludeon.rimworld", StringComparison.OrdinalIgnoreCase)
            || packageId.StartsWith("ludeon.rimworld.", StringComparison.OrdinalIgnoreCase);
    }
}

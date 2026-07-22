using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimForge.Core.Models;

public sealed class UseThisInsteadDatabase
{
    public int SchemaVersion { get; init; } = 1;
    public string ContentVersion { get; init; } = "1.0.0";
    public IReadOnlyList<UseThisInsteadRule> Rules { get; init; } = Array.Empty<UseThisInsteadRule>();

    public static UseThisInsteadDatabase LoadDefault()
    {
        foreach (var path in CandidatePaths())
        {
            if (!File.Exists(path)) continue;
            try
            {
                return JsonSerializer.Deserialize<UseThisInsteadDatabase>(File.ReadAllText(path), JsonOptions) ?? new();
            }
            catch (JsonException) { }
            catch (IOException) { }
        }
        return new();
    }

    public IReadOnlyList<UseThisInsteadRule> GetApplicable(string packageId, string? targetVersion) => Rules
        .Where(rule => rule.SubjectPackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase) && rule.Scope.AppliesTo(targetVersion))
        .Where(rule => !string.IsNullOrWhiteSpace(rule.RuleId) && !string.IsNullOrWhiteSpace(rule.ReplacementPackageId))
        .ToArray();

    private static IEnumerable<string> CandidatePaths()
    {
        var paths = RimForgePathLayout.Create(RimForgePathLayout.ResolveRepositoryRoot());
        foreach (var path in paths.CuratedDatabaseCandidates("UseThisInstead.json")) yield return path;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

public sealed class UseThisInsteadRule
{
    public string RuleId { get; init; } = string.Empty;
    public string SubjectPackageId { get; init; } = string.Empty;
    public string ReplacementPackageId { get; init; } = string.Empty;
    public string Reason { get; init; } = "A maintained replacement is available.";
    public string Source { get; init; } = "RimForge bundled baseline";
    public string? LastReviewed { get; init; }
    public LoadOrderRuleConfidence Confidence { get; init; } = LoadOrderRuleConfidence.Recommended;
    public CuratedRuleScope Scope { get; init; } = new();
}

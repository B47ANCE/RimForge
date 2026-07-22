using System.Text.Json;
using System.Text.Json.Serialization;

namespace RimForge.Core.Models;

public sealed class LoadOrderPolicyPack
{
    public int SchemaVersion { get; init; } = 4;
    public string ContentVersion { get; init; } = "1.0.0";
    public string Id { get; init; } = "rimforge.category-first.v1";
    public string DisplayName { get; init; } = "RimForge Category-First";
    public string Policy { get; init; } = "CategoryFirst";
    public string PrecedenceStatement { get; init; } = "Later bands override earlier bands";
    public bool LaterCategoryOverridesEarlier { get; init; } = true;
    public bool DependencyReconciliationDownwardOnly { get; init; } = true;
    public bool CuratedAnchorsBidirectional { get; init; } = true;
    public IReadOnlyList<string> TopAnchors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CategoryBands { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BottomAnchors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LoadOrderPackageRule> PackageRules { get; init; } = Array.Empty<LoadOrderPackageRule>();
    public IReadOnlyList<LoadOrderRelativeRule> RelativeRules { get; init; } = Array.Empty<LoadOrderRelativeRule>();
    public IReadOnlyList<CuratedRuleDiagnostic> Diagnostics { get; private init; } = Array.Empty<CuratedRuleDiagnostic>();
    public bool IsValid => Diagnostics.All(diagnostic => !diagnostic.IsBlocking);

    public static LoadOrderPolicyPack LoadDefault()
    {
        foreach (var path in CandidatePaths())
        {
            if (!File.Exists(path)) continue;
            try
            {
                var pack = JsonSerializer.Deserialize<LoadOrderPolicyPack>(
                    File.ReadAllText(path),
                    JsonOptions);
                if (pack is not null && pack.Policy.Equals("CategoryFirst", StringComparison.OrdinalIgnoreCase))
                    return pack.WithValidation();
            }
            catch (JsonException)
            {
                // Fall through to the embedded policy so a damaged optional rule pack cannot break startup.
            }
            catch (IOException)
            {
                // Fall through to the embedded policy when the external file is temporarily unavailable.
            }
        }

        return CreateBuiltIn().WithValidation();
    }


    public IReadOnlyList<LoadOrderRelativeRule> GetApplicableRelativeRules(string? targetVersion) =>
        IsValid
            ? RelativeRules.Where(rule => rule.Scope.AppliesTo(targetVersion)).ToArray()
            : Array.Empty<LoadOrderRelativeRule>();

    public IReadOnlyList<LoadOrderPackageRule> GetApplicablePackageRules(string? targetVersion) =>
        IsValid
            ? PackageRules.Where(rule => rule.Scope.AppliesTo(targetVersion)).ToArray()
            : Array.Empty<LoadOrderPackageRule>();

    private LoadOrderPolicyPack WithValidation()
    {
        var diagnostics = new List<CuratedRuleDiagnostic>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in PackageRules)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleId))
                diagnostics.Add(new("MissingRuleId", "<package-rule>", "Package rule is missing a stable RuleId.", rule.Confidence == LoadOrderRuleConfidence.Hard));
            else if (!ids.Add(rule.RuleId))
                diagnostics.Add(new("DuplicateRuleId", rule.RuleId, "RuleId is duplicated in the load-order policy pack.", true));
            if (string.IsNullOrWhiteSpace(rule.Source))
                diagnostics.Add(new("MissingProvenance", rule.RuleId, "Package rule is missing provenance.", rule.Confidence == LoadOrderRuleConfidence.Hard));
        }
        foreach (var rule in RelativeRules)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleId))
                diagnostics.Add(new("MissingRuleId", "<relative-rule>", "Relative rule is missing a stable RuleId.", rule.Confidence == LoadOrderRuleConfidence.Hard));
            else if (!ids.Add(rule.RuleId))
                diagnostics.Add(new("DuplicateRuleId", rule.RuleId, "RuleId is duplicated in the load-order policy pack.", true));
            if (string.IsNullOrWhiteSpace(rule.Source))
                diagnostics.Add(new("MissingProvenance", rule.RuleId, "Relative rule is missing provenance.", rule.Confidence == LoadOrderRuleConfidence.Hard));
            if (rule.BeforePackageId.Equals(rule.AfterPackageId, StringComparison.OrdinalIgnoreCase))
                diagnostics.Add(new("SelfReference", rule.RuleId, "Relative rule points a package at itself.", true));
        }
        return new LoadOrderPolicyPack
        {
            SchemaVersion = SchemaVersion, ContentVersion = ContentVersion, Id = Id, DisplayName = DisplayName,
            Policy = Policy, PrecedenceStatement = PrecedenceStatement, LaterCategoryOverridesEarlier = LaterCategoryOverridesEarlier,
            DependencyReconciliationDownwardOnly = DependencyReconciliationDownwardOnly, CuratedAnchorsBidirectional = CuratedAnchorsBidirectional,
            TopAnchors = TopAnchors, CategoryBands = CategoryBands, BottomAnchors = BottomAnchors,
            PackageRules = PackageRules, RelativeRules = RelativeRules, Diagnostics = diagnostics
        };
    }

    private static IEnumerable<string> CandidatePaths()
    {
        var paths = RimForgePathLayout.Create(RimForgePathLayout.ResolveRepositoryRoot());
        foreach (var path in paths.CuratedDatabaseCandidates("LoadOrderRules.json")) yield return path;
    }

    private static LoadOrderPolicyPack CreateBuiltIn() => new()
    {
        TopAnchors = LoadOrderRules.TopAnchors,
        BottomAnchors = LoadOrderRules.BottomAnchors,
        CategoryBands = Enum.GetValues<LoadOrderCategory>()
            .Where(value => value is not LoadOrderCategory.TopAnchor and not LoadOrderCategory.BottomAnchor and not LoadOrderCategory.Uncategorized)
            .OrderBy(value => (int)value)
            .Select(value => value.ToString())
            .ToArray(),
        PackageRules =
        [
            new() { RuleId = "rf.anchor.rocketman", PackageId = LoadOrderRules.RocketManPackageId, Category = LoadOrderCategory.BottomAnchor, Confidence = LoadOrderRuleConfidence.Hard, Reason = "Canonical bottom anchor" },
            new() { RuleId = "rf.anchor.missilegirl", PackageId = LoadOrderRules.MissileGirlPackageId, Category = LoadOrderCategory.BottomAnchor, Confidence = LoadOrderRuleConfidence.Hard, Reason = "Canonical bottom anchor" }
        ]
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

public sealed class LoadOrderPackageRule
{
    public string RuleId { get; init; } = string.Empty;
    public string Source { get; init; } = "RimForge bundled baseline";
    public string? LastReviewed { get; init; }
    public CuratedRuleScope Scope { get; init; } = new();
    public string? PackageId { get; init; }
    public string? NameContains { get; init; }
    public LoadOrderCategory Category { get; init; } = LoadOrderCategory.Uncategorized;
    public LoadOrderRuleConfidence Confidence { get; init; } = LoadOrderRuleConfidence.Recommended;
    public string Reason { get; init; } = "Curated package rule";
}

public sealed class LoadOrderRelativeRule
{
    public string RuleId { get; init; } = string.Empty;
    public string Source { get; init; } = "RimForge bundled baseline";
    public string? LastReviewed { get; init; }
    public CuratedRuleScope Scope { get; init; } = new();
    public string BeforePackageId { get; init; } = string.Empty;
    public string AfterPackageId { get; init; } = string.Empty;
    public LoadOrderRuleConfidence Confidence { get; init; } = LoadOrderRuleConfidence.Recommended;
    public string Reason { get; init; } = "Curated relative-order rule";
}

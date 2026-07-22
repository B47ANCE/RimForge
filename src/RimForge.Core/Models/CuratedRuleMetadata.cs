namespace RimForge.Core.Models;

public sealed class CuratedRuleScope
{
    public IReadOnlyList<string> RimWorldVersions { get; init; } = Array.Empty<string>();

    public bool AppliesTo(string? targetVersion)
    {
        if (RimWorldVersions.Count == 0 || string.IsNullOrWhiteSpace(targetVersion)) return true;
        return RimWorldVersions.Contains(targetVersion.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record CuratedRuleDiagnostic(
    string Code,
    string RuleId,
    string Message,
    bool IsBlocking);

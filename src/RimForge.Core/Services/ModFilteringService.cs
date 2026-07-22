using System.Text.RegularExpressions;
using RimForge.Core.Models;

namespace RimForge.Core.Services;

public sealed class ModFilteringService : IModFilteringService
{
    public bool Matches(ModRecord mod, ModFilterCriteria criteria)
    {
        if (!criteria.ShowFullLibrary && criteria.ActivePackageIds is not null)
        {
            if (string.IsNullOrWhiteSpace(mod.PackageId) || !criteria.ActivePackageIds.Contains(mod.PackageId)) return false;
        }
        if (criteria.IssuesOnly && mod.Errors.Count == 0) return false;

        var query = criteria.Query ?? StructuredSearchQuery.Parse(criteria.SearchText);
        return query.Evaluate(clause => MatchesClause(mod, clause, criteria));
    }

    private static bool MatchesClause(ModRecord mod, SearchClause clause, ModFilterCriteria criteria) => clause.Field switch
    {
        "identity" => MatchesIdentity(mod, clause.Value),
        "mod" => MatchText(mod.DisplayName, clause),
        "package" => MatchText(mod.PackageId, clause),
        "author" => MatchText(mod.Author, clause),
        "workshop" => MatchText(mod.WorkshopId, clause) || MatchText(mod.WorkshopUrl, clause),
        "source" => MatchesSource(mod, clause),
        "badge" => mod.Evidence.Badges.Any(badge => MatchText(badge.Kind.ToString(), clause) || MatchText(badge.Label, clause)),
        "requires" => mod.Dependencies.Any(dependency => MatchText(dependency.PackageId, clause) || MatchText(dependency.DisplayName, clause)),
        "required-by" => IsRequiredBy(mod, clause, criteria.Library),
        "incompatible" => mod.Errors.Any(error => MatchText(error, clause)) || mod.Evidence.NotableFindings.Any(finding => MatchText(finding, clause)),
        "supported-version" => mod.SupportedVersions.Any(version => MatchVersion(version, clause)),
        "active" => MatchesBoolean(criteria.ActivePackageIds?.Contains(mod.PackageId ?? string.Empty) == true, clause.Value),
        "issue" => MatchesIssue(mod, clause),
        "favorite" => false, // feature registration point; profile metadata will supply this later
        "profile" => false,  // feature registration point; profile index will supply this later
        _ => false
    };

    private static bool MatchesIdentity(ModRecord mod, string value) =>
        MatchPattern(mod.DisplayName, value) || MatchPattern(mod.PackageId, value) || MatchPattern(mod.Author, value) || MatchPattern(mod.WorkshopId, value);

    private static bool MatchesSource(ModRecord mod, SearchClause clause) =>
        MatchText(mod.Source.ToString(), clause)
        || (mod.HasWorkshop && (MatchText("Steam", clause) || MatchText("Workshop", clause) || MatchText("Steam Workshop", clause)))
        || (!mod.HasWorkshop && MatchText("Local", clause));

    private static bool IsRequiredBy(ModRecord candidate, SearchClause clause, IReadOnlyList<ModRecord>? library)
    {
        if (library is null) return false;
        return library.Where(mod => MatchesIdentity(mod, clause.Value)).Any(dependent =>
            dependent.Dependencies.Any(dependency =>
                MatchPattern(candidate.PackageId, dependency.PackageId) || MatchPattern(candidate.DisplayName, dependency.DisplayName ?? dependency.PackageId)));
    }

    private static bool MatchesIssue(ModRecord mod, SearchClause clause)
    {
        if (int.TryParse(clause.Value, out var expectedCount) && clause.Operator is ">" or ">=" or "<" or "<=" or "=" or "!=")
            return Compare(mod.Errors.Count, expectedCount, clause.Operator);
        if (MatchesBoolean(mod.Errors.Count > 0, clause.Value)) return true;
        return mod.Errors.Any(error => MatchText(error, clause));
    }

    private static bool MatchVersion(string version, SearchClause clause)
    {
        if (clause.Operator == ":" || clause.Operator == "=") return MatchPattern(version, clause.Value);
        if (!Version.TryParse(NormalizeVersion(version), out var actual) || !Version.TryParse(NormalizeVersion(clause.Value), out var expected)) return false;
        var comparison = actual.CompareTo(expected);
        return clause.Operator switch { ">" => comparison > 0, ">=" => comparison >= 0, "<" => comparison < 0, "<=" => comparison <= 0, "!=" => comparison != 0, _ => comparison == 0 };
    }

    private static string NormalizeVersion(string value)
    {
        var match = Regex.Match(value, @"\d+(?:\.\d+){0,3}");
        return match.Success ? match.Value : value;
    }

    private static bool MatchesBoolean(bool actual, string value)
    {
        if (bool.TryParse(value, out var expected)) return actual == expected;
        if (value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("on", StringComparison.OrdinalIgnoreCase)) return actual;
        if (value.Equals("no", StringComparison.OrdinalIgnoreCase) || value == "0" || value.Equals("off", StringComparison.OrdinalIgnoreCase)) return !actual;
        return false;
    }

    private static bool MatchText(string? value, SearchClause clause) => clause.Operator == "!=" ? !MatchPattern(value, clause.Value) : MatchPattern(value, clause.Value);

    private static bool MatchPattern(string? value, string? query)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(query)) return false;
        if (!query.Contains('*') && !query.Contains('?')) return value.Contains(query, StringComparison.OrdinalIgnoreCase);
        var pattern = "^" + Regex.Escape(query).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool Compare(int actual, int expected, string op) => op switch
    {
        ">" => actual > expected, ">=" => actual >= expected, "<" => actual < expected, "<=" => actual <= expected, "!=" => actual != expected, _ => actual == expected
    };
}

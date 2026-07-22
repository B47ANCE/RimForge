namespace RimForge.Core.Models;

public enum LoadOrderCategory
{
    TopAnchor = 0,
    Libraries = 100,
    SpecialFrameworkExceptions = 200,
    EarlyOverhauls = 300,
    MapGeneration = 400,
    Factions = 500,
    Traits = 600,
    AndroidsAndRobots = 700,
    Medical = 800,
    GameplayMechanicsAndUi = 900,
    Uncategorized = 950,
    Items = 1000,
    RacesAnimalsAndPawns = 1100,
    BottomRequired = 1200,
    BottomAnchor = 1300
}

public enum LoadOrderRuleConfidence
{
    Experimental = 0,
    Recommended = 1,
    Hard = 2
}

public sealed record LoadOrderClassificationEvidence(
    LoadOrderCategory Category,
    string Reason,
    string Source,
    LoadOrderRuleConfidence Confidence);

public sealed record LoadOrderClassification(
    LoadOrderCategory Category,
    string Reason,
    bool IsCurated = false,
    LoadOrderRuleConfidence Confidence = LoadOrderRuleConfidence.Recommended,
    IReadOnlyList<LoadOrderCategory>? CandidateCategories = null,
    IReadOnlyList<LoadOrderClassificationEvidence>? Evidence = null,
    string RuleSource = "RimForge built-in policy")
{
    public IReadOnlyList<LoadOrderCategory> MatchedCategories =>
        CandidateCategories ?? Array.Empty<LoadOrderCategory>();

    public IReadOnlyList<LoadOrderClassificationEvidence> ClassificationEvidence =>
        Evidence ?? Array.Empty<LoadOrderClassificationEvidence>();

    public string Explanation =>
        $"{Category}: {Reason} (confidence: {Confidence}; source: {RuleSource})";
}

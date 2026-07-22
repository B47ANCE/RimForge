namespace RimForge.Core.Models;

/// <summary>
/// Category-first RimWorld load-order knowledge engine.
/// Later bands override earlier bands when a mod matches more than one category.
/// Required dependencies and declared ordering edges are still preserved by the analysis engine.
/// </summary>
public static class LoadOrderPolicy
{
    private static readonly Dictionary<string, LoadOrderClassification> ClassificationCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<LoadOrderPolicyPack> PolicyPack = new(LoadOrderPolicyPack.LoadDefault);

    public static LoadOrderPolicyPack Current => PolicyPack.Value;

    public static LoadOrderClassification Classify(ModRecord mod, string? targetVersion = null)
    {
        ArgumentNullException.ThrowIfNull(mod);
        var packageId = mod.PackageId?.Trim() ?? string.Empty;
        var cacheKey = $"{targetVersion}|{packageId}|{mod.DisplayName}|{mod.Description}|{mod.Evidence.DnaText}";
        if (ClassificationCache.TryGetValue(cacheKey, out var cached)) return cached;

        if (LoadOrderRules.IsTopAnchor(packageId))
            return Curated(LoadOrderCategory.TopAnchor, "RimForge canonical top anchor", LoadOrderRuleConfidence.Hard);
        if (LoadOrderRules.IsBottomAnchor(packageId))
            return Curated(LoadOrderCategory.BottomAnchor, "RimForge canonical bottom anchor", LoadOrderRuleConfidence.Hard);

        var matchingRule = Current.GetApplicablePackageRules(targetVersion)
            .Where(rule => Matches(rule, mod, packageId))
            .OrderByDescending(rule => rule.Confidence)
            .ThenByDescending(rule => (int)rule.Category)
            .FirstOrDefault();
        if (matchingRule is not null)
            return Curated(matchingRule.Category, matchingRule.Reason, matchingRule.Confidence, $"{matchingRule.RuleId} · {matchingRule.Source}");

        var badges = mod.Evidence.Badges.Select(static badge => badge.Kind).ToHashSet();
        var text = $"{mod.DisplayName} {mod.Description} {packageId}".ToLowerInvariant();
        var evidence = new List<LoadOrderClassificationEvidence>();

        AddIf(LooksLikeLibrary(mod, text), LoadOrderCategory.Libraries, "framework/library evidence", "metadata + Forge evidence");
        AddIf(ContainsAny(text, "rimworld of magic", "medieval times", "rimwar", "rimhammer", "tiberium rim", "combat extended"),
            LoadOrderCategory.EarlyOverhauls, "large or foundational overhaul signature", "curated signature");
        AddIf(badges.Contains(ModEvidenceKind.WorldGeneration) || badges.Contains(ModEvidenceKind.Biomes),
            LoadOrderCategory.MapGeneration, "world-generation or biome evidence", "Forge evidence");
        AddIf(badges.Contains(ModEvidenceKind.Factions), LoadOrderCategory.Factions, "faction evidence", "Forge evidence");
        AddIf(ContainsAny(text, "trait", "traits"), LoadOrderCategory.Traits, "trait-focused metadata", "metadata");
        AddIf(ContainsAny(text, "android", "robot", "mechanoid colonist", "mechanical pawn"),
            LoadOrderCategory.AndroidsAndRobots, "android/robot pawn metadata", "metadata");
        AddIf(badges.Contains(ModEvidenceKind.Hediffs) || ContainsAny(text, "medical", "surgery", "bionic", "prosthetic", "health"),
            LoadOrderCategory.Medical, "medical or health-system evidence", "metadata + Forge evidence");
        AddIf(badges.Contains(ModEvidenceKind.ArtificialIntelligence) || badges.Contains(ModEvidenceKind.Jobs) ||
              badges.Contains(ModEvidenceKind.Incidents) || badges.Contains(ModEvidenceKind.Harmony) ||
              ContainsAny(text, "ui", "interface", "storyteller", "mechanic", "behavior", "behaviour"),
            LoadOrderCategory.GameplayMechanicsAndUi, "gameplay, behavior, UI, or runtime-patch evidence", "metadata + Forge evidence");
        AddIf(badges.Overlaps(new[] { ModEvidenceKind.Weapons, ModEvidenceKind.Apparel, ModEvidenceKind.Buildings,
                ModEvidenceKind.Plants, ModEvidenceKind.Recipes, ModEvidenceKind.Research }),
            LoadOrderCategory.Items, "buildable, craftable, wearable, weapon, plant, recipe, or research evidence", "Forge evidence");
        AddIf(badges.Contains(ModEvidenceKind.PawnKinds) || badges.Contains(ModEvidenceKind.Animals) ||
              ContainsAny(text, "race", "races", "animal", "pawn race", "alien race"),
            LoadOrderCategory.RacesAnimalsAndPawns, "race, animal, or pawn-kind evidence", "metadata + Forge evidence");

        if (evidence.Count == 0)
            return new(LoadOrderCategory.Uncategorized, "no high-confidence category evidence",
                Confidence: LoadOrderRuleConfidence.Experimental,
                RuleSource: "RimForge classifier");

        // Category bands are the primary organizing structure. Later bands override earlier bands.
        var winner = Current.LaterCategoryOverridesEarlier
            ? evidence.OrderByDescending(static item => (int)item.Category).First()
            : evidence.OrderBy(static item => (int)item.Category).First();
        var classification = new LoadOrderClassification(
            winner.Category,
            winner.Reason,
            Confidence: winner.Confidence,
            CandidateCategories: evidence.Select(item => item.Category).Distinct().OrderBy(item => (int)item).ToArray(),
            Evidence: evidence,
            RuleSource: winner.Source);
        ClassificationCache[cacheKey] = classification;
        return classification;

        void AddIf(bool condition, LoadOrderCategory category, string reason, string source)
        {
            if (condition)
                evidence.Add(new(category, reason, source, LoadOrderRuleConfidence.Recommended));
        }
    }

    public static int GetSortRank(ModRecord mod, string? targetVersion = null) => (int)Classify(mod, targetVersion).Category;

    public static IReadOnlyList<LoadOrderRelativeRule> GetApplicableRelativeRules(
        IReadOnlyDictionary<string, ModRecord> mods,
        string? targetVersion = null) =>
        Current.GetApplicableRelativeRules(targetVersion)
            .Where(rule => !string.IsNullOrWhiteSpace(rule.BeforePackageId) && !string.IsNullOrWhiteSpace(rule.AfterPackageId))
            .Where(rule => mods.ContainsKey(rule.BeforePackageId) && mods.ContainsKey(rule.AfterPackageId))
            .ToArray();

    public static string Explain(ModRecord mod) => Classify(mod).Explanation;

    private static LoadOrderClassification Curated(
        LoadOrderCategory category,
        string reason,
        LoadOrderRuleConfidence confidence,
        string source = "RimForge canonical policy") =>
        new(category, reason, true, confidence, [category],
            [new(category, reason, source, confidence)], source);

    private static bool Matches(LoadOrderPackageRule rule, ModRecord mod, string packageId)
    {
        if (!string.IsNullOrWhiteSpace(rule.PackageId) &&
            packageId.Equals(rule.PackageId, StringComparison.OrdinalIgnoreCase)) return true;
        return !string.IsNullOrWhiteSpace(rule.NameContains) &&
               mod.DisplayName.Contains(rule.NameContains, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeLibrary(ModRecord mod, string text)
    {
        if (ContainsAny(text, "library", "framework", "core mod", "dependency only", "api for", "required by other mods"))
            return true;
        return mod.Evidence.AssemblyFiles > 0 && mod.Evidence.DefinitionCount == 0 &&
               mod.Evidence.TextureFiles == 0 && mod.Evidence.AudioFiles == 0;
    }

    private static bool ContainsAny(string text, params string[] values) => values.Any(text.Contains);
}

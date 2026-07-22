using RimForge.Analysis.Models;
using RimForge.Core.Models;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace RimForge.Analysis.Services;

public sealed class ModAnalysisEngine : IModAnalysisEngine
{
    private const int CacheCapacity = 8;
    private readonly object _cacheGate = new();
    private readonly Dictionary<string, CachedAnalysis> _cache = new(StringComparer.Ordinal);
    private long _cacheAccessSequence;

    public Task<ModAnalysisResult> AnalyzeAsync(
        ModAnalysisRequest request,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.InstalledMods);
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        var orderedMods = request.InstalledMods
            .OrderBy(mod => mod.PackageId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.RootPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fingerprint = CreateInputFingerprint(
            orderedMods,
            request.ActiveLoadOrder,
            request.TargetRimWorldVersion,
            request.LockedPositions,
            request.Evidence);
        var telemetry = new AnalysisRunTelemetry(progress, orderedMods.Length, cancellationToken);
        telemetry.Advance(AnalysisStage.CacheLookup, 0, "Resolving the deterministic analysis input identity.");
        if (request.CachePolicy == AnalysisCachePolicy.Use && TryGetCached(fingerprint, out var cached))
        {
            stopwatch.Stop();
            telemetry.Complete($"Reused analysis for {orderedMods.Length} unchanged installed mod(s).");
            return Task.FromResult(CreateResult(
                cached.Snapshot,
                cached.Diagnostics,
                cached.Explainability,
                telemetry.Stages,
                orderedMods.Length,
                request.ActiveLoadOrder,
                stopwatch.Elapsed,
                fingerprint,
                new AnalysisCacheInfo(AnalysisCacheDisposition.Hit, fingerprint, cached.GeneratedAtUtc)));
        }

        telemetry.Advance(AnalysisStage.Indexing, 0, "Indexing the complete installed mod library.");
        var snapshot = AnalyzeCore(
            orderedMods,
            request.ActiveLoadOrder,
            request.TargetRimWorldVersion,
            request.LockedPositions,
            request.Evidence,
            cancellationToken,
            telemetry);
        cancellationToken.ThrowIfCancellationRequested();
        stopwatch.Stop();
        var diagnostics = snapshot.Issues
            .Select(issue => new AnalysisDiagnostic(
                issue.Code.ToString(),
                issue.Severity,
                issue.Explanation,
                issue.PackageId,
                issue.RelatedPackageIds))
            .ToArray();
        var explainability = AnalysisExplanationBuilder.Build(
            snapshot,
            orderedMods,
            request.ActiveLoadOrder,
            diagnostics);
        telemetry.Advance(AnalysisStage.Finalizing, orderedMods.Length, "Finalizing stable analysis output.");
        telemetry.Complete($"Analyzed {orderedMods.Length} installed mod(s).");
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var disposition = request.CachePolicy switch
        {
            AnalysisCachePolicy.Refresh => AnalysisCacheDisposition.Refreshed,
            AnalysisCachePolicy.Bypass => AnalysisCacheDisposition.Bypassed,
            _ => AnalysisCacheDisposition.Miss
        };
        if (request.CachePolicy != AnalysisCachePolicy.Bypass)
            StoreCached(fingerprint, snapshot, diagnostics, explainability, generatedAtUtc);
        return Task.FromResult(CreateResult(
            snapshot,
            diagnostics,
            explainability,
            telemetry.Stages,
            orderedMods.Length,
            request.ActiveLoadOrder,
            stopwatch.Elapsed,
            fingerprint,
            new AnalysisCacheInfo(disposition, fingerprint, generatedAtUtc)));
    }

    public void InvalidateCache(string? inputFingerprint = null)
    {
        lock (_cacheGate)
        {
            if (string.IsNullOrWhiteSpace(inputFingerprint)) _cache.Clear();
            else _cache.Remove(inputFingerprint);
        }
    }

    private ModAnalysisResult CreateResult(
        ModAnalysisSnapshot snapshot,
        IReadOnlyList<AnalysisDiagnostic> diagnostics,
        AnalysisExplanationCatalog explainability,
        IReadOnlyList<AnalysisStageMetrics> stages,
        int installedCount,
        IReadOnlyList<string>? activeLoadOrder,
        TimeSpan elapsed,
        string fingerprint,
        AnalysisCacheInfo cache) => new(
            snapshot,
            new AnalysisExecutionMetrics(
                installedCount,
                activeLoadOrder?.Distinct(StringComparer.OrdinalIgnoreCase).Count() ?? 0,
                snapshot.Relationships.Count,
                snapshot.Issues.Count,
                snapshot.Cycles.Count,
                elapsed,
                fingerprint),
            diagnostics,
            stages,
            cache,
            explainability);

    private bool TryGetCached(string fingerprint, out CachedAnalysis cached)
    {
        lock (_cacheGate)
        {
            if (!_cache.TryGetValue(fingerprint, out cached!)) return false;
            cached = cached with { LastAccessSequence = ++_cacheAccessSequence };
            _cache[fingerprint] = cached;
            return true;
        }
    }

    private void StoreCached(
        string fingerprint,
        ModAnalysisSnapshot snapshot,
        IReadOnlyList<AnalysisDiagnostic> diagnostics,
        AnalysisExplanationCatalog explainability,
        DateTimeOffset generatedAtUtc)
    {
        lock (_cacheGate)
        {
            _cache[fingerprint] = new CachedAnalysis(
                snapshot,
                diagnostics,
                explainability,
                generatedAtUtc,
                ++_cacheAccessSequence);
            if (_cache.Count <= CacheCapacity) return;
            var oldestKey = _cache.MinBy(pair => pair.Value.LastAccessSequence).Key;
            _cache.Remove(oldestKey);
        }
    }

    private sealed record CachedAnalysis(
        ModAnalysisSnapshot Snapshot,
        IReadOnlyList<AnalysisDiagnostic> Diagnostics,
        AnalysisExplanationCatalog Explainability,
        DateTimeOffset GeneratedAtUtc,
        long LastAccessSequence);

    private sealed record OrderingPreference(
        string BeforePackageId,
        string AfterPackageId,
        LoadOrderRuleConfidence Confidence,
        string Reason,
        string Source,
        bool IsMandatory);
    public ModAnalysisSnapshot Analyze(
        IReadOnlyList<ModRecord> mods,
        IReadOnlyList<string>? currentLoadOrder = null,
        string? targetRimWorldVersion = null,
        IReadOnlyList<UserLoadOrderLock>? lockedPositions = null)
        => AnalyzeCore(mods, currentLoadOrder, targetRimWorldVersion, lockedPositions, null, CancellationToken.None, null);

    private ModAnalysisSnapshot AnalyzeCore(
        IReadOnlyList<ModRecord> mods,
        IReadOnlyList<string>? currentLoadOrder,
        string? targetRimWorldVersion,
        IReadOnlyList<UserLoadOrderLock>? lockedPositions,
        IReadOnlyList<ForgeEvidenceContribution>? evidence,
        CancellationToken cancellationToken,
        AnalysisRunTelemetry? telemetry)
    {
        ArgumentNullException.ThrowIfNull(mods);
        cancellationToken.ThrowIfCancellationRequested();

        var issues = new List<ModAnalysisIssue>();
        var relationships = new List<AnalysisRelationship>();
        var packageGroups = mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
            .GroupBy(mod => ModNameResolver.Normalize(mod.PackageId!), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var byPackageId = packageGroups.ToDictionary(
            group => group.Key,
            group => group.First(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods.Where(mod => string.IsNullOrWhiteSpace(mod.PackageId)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            issues.Add(new ModAnalysisIssue(
                AnalysisIssueCode.MissingPackageId,
                AnalysisIssueSeverity.Error,
                mod.Id,
                "Missing package ID",
                $"{mod.DisplayName} does not declare a package ID and cannot participate reliably in dependency analysis.",
                Array.Empty<string>()));
        }

        foreach (var group in packageGroups.Where(group => group.Count() > 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var paths = group.Select(mod => mod.RootPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var mod in group)
            {
                issues.Add(new ModAnalysisIssue(
                    AnalysisIssueCode.DuplicatePackageId,
                    AnalysisIssueSeverity.Error,
                    group.Key,
                    "Duplicate package ID",
                    $"{group.Key} is declared by {group.Count()} installed folders: {string.Join("; ", paths)}",
                    new[] { group.Key }));
            }
        }

        foreach (var diagnostic in LoadOrderPolicy.Current.Diagnostics)
        {
            issues.Add(new ModAnalysisIssue(
                AnalysisIssueCode.CuratedRuleConflict,
                diagnostic.IsBlocking ? AnalysisIssueSeverity.Error : AnalysisIssueSeverity.Warning,
                "rimforge.curated-rules",
                "Curated database validation failed",
                $"{diagnostic.Code}: {diagnostic.Message} Rule: {diagnostic.RuleId}. " +
                (diagnostic.IsBlocking ? "Curated load-order rules were disabled for this analysis." : "The affected advisory rule was ignored."),
                Array.Empty<string>()));
        }

        var dependencies = CreateMap(byPackageId.Keys);
        var dependents = CreateMap(byPackageId.Keys);
        var hardOrdering = CreateMap(byPackageId.Keys);
        var orderingPreferences = new List<OrderingPreference>();

        telemetry?.Advance(AnalysisStage.Relationships, 0, "Resolving dependency and metadata relationships.");
        foreach (var mod in byPackageId.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = ModNameResolver.Normalize(mod.PackageId!);
            foreach (var dependency in mod.Dependencies
                         .Where(item => !string.IsNullOrWhiteSpace(item.PackageId))
                         .DistinctBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                var dependencyId = ModNameResolver.Normalize(dependency.PackageId);
                if (!byPackageId.ContainsKey(dependencyId))
                {
                    issues.Add(new ModAnalysisIssue(
                        AnalysisIssueCode.MissingRequiredDependency,
                        AnalysisIssueSeverity.Error,
                        source,
                        "Missing required dependency",
                        $"{mod.DisplayName} requires {dependency.DisplayName ?? dependency.PackageId}, but {dependency.PackageId} is not installed.",
                        new[] { dependencyId }));
                    continue;
                }

                AddUnique(dependencies[source], dependencyId);
                AddUnique(dependents[dependencyId], source);
                AddUnique(hardOrdering[dependencyId], source);
                orderingPreferences.Add(new OrderingPreference(
                    dependencyId, source, LoadOrderRuleConfidence.Hard,
                    $"{mod.DisplayName} requires {byPackageId[dependencyId].DisplayName}.",
                    "Required dependency metadata", true));
                relationships.Add(new AnalysisRelationship(
                    source,
                    dependencyId,
                    AnalysisRelationshipKind.RequiredDependency,
                    $"{mod.DisplayName} requires {byPackageId[dependencyId].DisplayName}.",
                    LoadOrderRuleConfidence.Hard,
                    "Required dependency metadata",
                    true));
            }

            foreach (var target in mod.LoadBefore.Select(ModNameResolver.Normalize).Where(byPackageId.ContainsKey)
                         .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                AddUnique(hardOrdering[source], target);
                orderingPreferences.Add(new OrderingPreference(
                    source, target, LoadOrderRuleConfidence.Hard,
                    $"{mod.DisplayName} declares that it must load before {byPackageId[target].DisplayName}.",
                    "Mod loadBefore metadata", true));
                relationships.Add(new AnalysisRelationship(source, target, AnalysisRelationshipKind.LoadBefore,
                    $"{mod.DisplayName} declares that it must load before {byPackageId[target].DisplayName}.",
                    LoadOrderRuleConfidence.Hard,
                    "Mod loadBefore metadata",
                    true));
            }

            foreach (var target in mod.LoadAfter.Select(ModNameResolver.Normalize).Where(byPackageId.ContainsKey)
                         .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                AddUnique(hardOrdering[target], source);
                orderingPreferences.Add(new OrderingPreference(
                    target, source, LoadOrderRuleConfidence.Hard,
                    $"{mod.DisplayName} declares that it must load after {byPackageId[target].DisplayName}.",
                    "Mod loadAfter metadata", true));
                relationships.Add(new AnalysisRelationship(source, target, AnalysisRelationshipKind.LoadAfter,
                    $"{mod.DisplayName} declares that it must load after {byPackageId[target].DisplayName}.",
                    LoadOrderRuleConfidence.Hard,
                    "Mod loadAfter metadata",
                    true));
            }
        }

        telemetry?.Advance(AnalysisStage.Rules, 0, "Evaluating curated ordering and replacement rules.");
        var applicableCuratedRules = LoadOrderPolicy.GetApplicableRelativeRules(byPackageId, targetRimWorldVersion);
        var quarantinedRuleIds = FindContradictoryCuratedRules(applicableCuratedRules, issues);
        foreach (var rule in applicableCuratedRules.Where(rule => !quarantinedRuleIds.Contains(rule.RuleId)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = ModNameResolver.Normalize(rule.BeforePackageId);
            var after = ModNameResolver.Normalize(rule.AfterPackageId);
            var mandatory = rule.Confidence == LoadOrderRuleConfidence.Hard;
            if (mandatory) AddUnique(hardOrdering[before], after);
            orderingPreferences.Add(new OrderingPreference(
                before, after, rule.Confidence, rule.Reason,
                $"{rule.RuleId} · {rule.Source}", mandatory));
            relationships.Add(new AnalysisRelationship(
                before,
                after,
                AnalysisRelationshipKind.LoadBefore,
                $"RimForge curated {rule.Confidence.ToString().ToLowerInvariant()} rule: {rule.Reason}",
                rule.Confidence,
                $"{rule.RuleId} · {rule.Source}",
                mandatory));
        }

        AddUseThisInsteadIssues(byPackageId, targetRimWorldVersion, issues);
        AddEvidenceFindings(byPackageId, evidence, issues, relationships, cancellationToken);

        telemetry?.Advance(AnalysisStage.GraphValidation, 0, "Validating dependency graph cycles.");
        var cycles = FindCycles(hardOrdering, cancellationToken);
        foreach (var cycle in cycles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var explanation = $"Dependency or load-order cycle detected: {string.Join(" → ", cycle.Select(id => byPackageId.TryGetValue(id, out var cycleMod) ? cycleMod.DisplayName : id))}.";
            foreach (var packageId in cycle.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new ModAnalysisIssue(
                    AnalysisIssueCode.DependencyCycle,
                    AnalysisIssueSeverity.Error,
                    packageId,
                    "Dependency cycle",
                    explanation,
                    cycle));
            }
        }

        telemetry?.Advance(AnalysisStage.ProfileValidation, 0, "Validating the active profile against the installed library.");
        var normalizedCurrentOrder = currentLoadOrder?
            .Select(ModNameResolver.Normalize)
            .Where(byPackageId.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sortScope = normalizedCurrentOrder is { Length: > 0 }
            ? normalizedCurrentOrder.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : byPackageId.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scopedHardOrdering = FilterAdjacency(hardOrdering, sortScope);
        var scopedPreferences = orderingPreferences
            .Where(item => sortScope.Contains(item.BeforePackageId) && sortScope.Contains(item.AfterPackageId))
            .ToArray();
        if (normalizedCurrentOrder is { Length: > 0 })
        {
            AddInactiveDependencyIssues(sortScope, byPackageId, issues);
            AddLoadOrderIssues(normalizedCurrentOrder, scopedHardOrdering, byPackageId, issues);
        }
        telemetry?.Advance(AnalysisStage.LoadOrderPlanning, 0, "Building the deterministic load-order plan.");
        var proposedOrder = BuildTopologicalOrder(
            scopedHardOrdering,
            scopedPreferences,
            normalizedCurrentOrder ?? byPackageId.Keys.ToArray(),
            byPackageId,
            targetRimWorldVersion,
            cancellationToken);
        if (proposedOrder.IsComplete && lockedPositions is { Count: > 0 })
        {
            var lockResult = LockedPositionSolver.Apply(proposedOrder.OrderedPackageIds, scopedHardOrdering, lockedPositions);
            foreach (var conflict in lockResult.Conflicts)
            {
                issues.Add(new ModAnalysisIssue(
                    AnalysisIssueCode.UserLockConflict,
                    AnalysisIssueSeverity.Warning,
                    conflict.PackageId,
                    "User load-order lock conflict",
                    conflict.Explanation + (conflict.SuggestedPositions.Count > 0
                        ? $" Legal positions include: {string.Join(", ", conflict.SuggestedPositions.Select(position => position + 1))}."
                        : " No legal position is currently available."),
                    new[] { conflict.BlockingPackageId }));
            }

            if (lockResult.OrderedPackageIds.Count == proposedOrder.OrderedPackageIds.Count)
            {
                var lockedRank = lockResult.OrderedPackageIds.Select((packageId, index) => (packageId, index))
                    .ToDictionary(item => item.packageId, item => item.index, StringComparer.OrdinalIgnoreCase);
                proposedOrder = proposedOrder with
                {
                    OrderedPackageIds = lockResult.OrderedPackageIds,
                    Explanation = lockResult.Success
                        ? proposedOrder.Explanation + " User-locked positions were honored."
                        : proposedOrder.Explanation + " Conflicting user locks were left unapplied."
                };
                proposedOrder = proposedOrder with
                {
                    Decisions = proposedOrder.Decisions.Select(decision => decision with
                    {
                        ProposedIndex = lockedRank.GetValueOrDefault(decision.PackageId, decision.ProposedIndex),
                        PrimaryReason = lockedPositions.Any(item => item.PackageId.Equals(decision.PackageId, StringComparison.OrdinalIgnoreCase))
                            ? "User-locked load-order position"
                            : decision.PrimaryReason,
                        RuleSource = lockedPositions.Any(item => item.PackageId.Equals(decision.PackageId, StringComparison.OrdinalIgnoreCase))
                            ? "Profile workspace lock"
                            : decision.RuleSource
                    }).ToArray()
                };
            }
        }
        var scopedCycles = FindCycles(scopedHardOrdering, cancellationToken);
        var cycleMembers = scopedCycles.SelectMany(cycle => cycle).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var namedCycleMembers = scopedCycles
            .SelectMany(cycle => cycle)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => byPackageId.TryGetValue(id, out var cycleMod) ? cycleMod.DisplayName : id)
            .ToArray();
        foreach (var blockedPackageId in proposedOrder.BlockedPackageIds.Where(id => !cycleMembers.Contains(id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var blockedName = byPackageId.TryGetValue(blockedPackageId, out var blockedMod) ? blockedMod.DisplayName : blockedPackageId;
            issues.Add(new ModAnalysisIssue(
                AnalysisIssueCode.LoadOrderBlockedByCycle,
                AnalysisIssueSeverity.Error,
                blockedPackageId,
                "Load order blocked by dependency cycle",
                $"{blockedName} cannot be placed in a complete load order until the cycle involving {string.Join(", ", namedCycleMembers)} is resolved.",
                cycleMembers.ToArray()));
        }

        var loadOrderPlan = new LoadOrderPlan(
            proposedOrder.IsComplete,
            proposedOrder.OrderedPackageIds.Select(id => new LoadOrderEntry(byPackageId.TryGetValue(id, out var mod) ? mod.DisplayName : id, id)).ToArray(),
            proposedOrder.BlockedPackageIds.Select(id => new LoadOrderEntry(byPackageId.TryGetValue(id, out var mod) ? mod.DisplayName : id, id)).ToArray(),
            scopedCycles.Select(cycle => (IReadOnlyList<LoadOrderEntry>)cycle
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => new LoadOrderEntry(byPackageId.TryGetValue(id, out var mod) ? mod.DisplayName : id, id))
                .ToArray()).ToArray(),
            proposedOrder.Explanation);
        return new ModAnalysisSnapshot(
            byPackageId,
            relationships
                .DistinctBy(item => (item.SourcePackageId.ToUpperInvariant(), item.TargetPackageId.ToUpperInvariant(), item.Kind))
                .OrderBy(item => item.SourcePackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.TargetPackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Kind)
                .ToArray(),
            issues
                .DistinctBy(item => (item.Code, item.PackageId.ToUpperInvariant(), item.Explanation))
                .OrderByDescending(item => item.Severity)
                .ThenBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            cycles.OrderBy(cycle => string.Join("|", cycle), StringComparer.OrdinalIgnoreCase).ToArray(),
            dependencies.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase),
            dependents.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase),
            proposedOrder,
            loadOrderPlan);
    }

    private static string CreateInputFingerprint(
        IReadOnlyList<ModRecord> mods,
        IReadOnlyList<string>? activeLoadOrder,
        string? targetVersion,
        IReadOnlyList<UserLoadOrderLock>? lockedPositions,
        IReadOnlyList<ForgeEvidenceContribution>? evidence)
    {
        static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
        static string Ordered(IEnumerable<string> values) => string.Join(",", values.Select(Normalize).OrderBy(value => value, StringComparer.Ordinal));

        var modInputs = mods.Select(mod => string.Join("|",
            Normalize(mod.Id),
            Normalize(mod.PackageId),
            Normalize(Path.GetFullPath(mod.RootPath)),
            mod.LastModified.UtcTicks,
            Ordered(mod.Dependencies.Select(item => item.PackageId)),
            Ordered(mod.LoadBefore),
            Ordered(mod.LoadAfter),
            Ordered(mod.IncompatibleWith),
            Ordered(mod.SupportedVersions),
            Ordered(mod.Evidence.Badges.Select(badge => $"{badge.Kind}:{badge.Count}:{badge.Label}")),
            Ordered(mod.Evidence.Capabilities),
            Ordered(mod.Evidence.NotableFindings)));
        var locks = (lockedPositions ?? Array.Empty<UserLoadOrderLock>())
            .OrderBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Position)
            .Select(item => $"{Normalize(item.PackageId)}|{item.Position}");
        var evidenceInputs = (evidence ?? Array.Empty<ForgeEvidenceContribution>())
            .OrderBy(item => item.EvidenceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SubjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RelatedSubjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.EvidenceType, StringComparer.OrdinalIgnoreCase)
            .Select(item => string.Join("|",
                Normalize(item.EvidenceId),
                Normalize(item.SubjectId),
                Normalize(item.RelatedSubjectId),
                Normalize(item.EvidenceType),
                Normalize(item.Summary),
                item.Confidence.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                item.ConfidenceBand,
                item.ObservationCount,
                item.Provenance.SourceKind,
                Normalize(item.Provenance.SourceId),
                Normalize(item.Provenance.SourceVersion),
                Ordered(item.EffectiveAttributes.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => $"{pair.Key}={pair.Value}"))));
        var input = string.Join("\n", modInputs) +
            "\n--active--\n" + string.Join("\n", (activeLoadOrder ?? Array.Empty<string>()).Select(Normalize)) +
            "\n--target--\n" + Normalize(targetVersion) +
            "\n--locks--\n" + string.Join("\n", locks) +
            "\n--evidence--\n" + string.Join("\n", evidenceInputs);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static Dictionary<string, List<string>> CreateMap(IEnumerable<string> keys) =>
        keys.ToDictionary(key => key, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, List<string>> FilterAdjacency(
        IReadOnlyDictionary<string, List<string>> adjacency,
        IReadOnlySet<string> scope)
    {
        var filtered = scope.ToDictionary(
            packageId => packageId,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var packageId in scope)
        {
            if (!adjacency.TryGetValue(packageId, out var targets)) continue;
            foreach (var target in targets.Where(scope.Contains))
                AddUnique(filtered[packageId], target);
        }
        return filtered;
    }

    private static void AddUnique(ICollection<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase)) values.Add(value);
    }

    private static void AddInactiveDependencyIssues(
        IReadOnlySet<string> activePackageIds,
        IReadOnlyDictionary<string, ModRecord> mods,
        ICollection<ModAnalysisIssue> issues)
    {
        foreach (var packageId in activePackageIds)
        {
            if (!mods.TryGetValue(packageId, out var mod)) continue;
            foreach (var dependency in mod.Dependencies
                         .Where(item => !string.IsNullOrWhiteSpace(item.PackageId))
                         .DistinctBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                var dependencyId = ModNameResolver.Normalize(dependency.PackageId);
                if (!mods.ContainsKey(dependencyId) || activePackageIds.Contains(dependencyId)) continue;
                issues.Add(new ModAnalysisIssue(
                    AnalysisIssueCode.InactiveRequiredDependency,
                    AnalysisIssueSeverity.Error,
                    packageId,
                    "Required dependency is inactive",
                    $"{mod.DisplayName} requires {dependency.DisplayName ?? dependency.PackageId}, which is installed but not active in this profile.",
                    new[] { dependencyId }));
            }
        }
    }

    private static void AddLoadOrderIssues(
        IReadOnlyList<string> currentLoadOrder,
        IReadOnlyDictionary<string, List<string>> ordering,
        IReadOnlyDictionary<string, ModRecord> mods,
        ICollection<ModAnalysisIssue> issues)
    {
        var position = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < currentLoadOrder.Count; index++)
        {
            var packageId = currentLoadOrder[index];
            if (!string.IsNullOrWhiteSpace(packageId)) position.TryAdd(packageId, index);
        }

        foreach (var (before, afterValues) in ordering)
        {
            if (!position.TryGetValue(before, out var beforeIndex)) continue;
            foreach (var after in afterValues)
            {
                if (!position.TryGetValue(after, out var afterIndex) || beforeIndex < afterIndex) continue;
                var beforeName = mods.TryGetValue(before, out var beforeMod) ? beforeMod.DisplayName : before;
                var afterName = mods.TryGetValue(after, out var afterMod) ? afterMod.DisplayName : after;
                issues.Add(new ModAnalysisIssue(
                    AnalysisIssueCode.LoadOrderViolation,
                    AnalysisIssueSeverity.Warning,
                    after,
                    "Load-order violation",
                    $"{beforeName} must load before {afterName}, but it currently appears later in the active profile.",
                    new[] { before, after }));
            }
        }
    }

    private static HashSet<string> FindContradictoryCuratedRules(
        IReadOnlyList<LoadOrderRelativeRule> rules,
        ICollection<ModAnalysisIssue> issues)
    {
        var quarantined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            var opposite = rules.FirstOrDefault(candidate =>
                !ReferenceEquals(candidate, rule) &&
                candidate.BeforePackageId.Equals(rule.AfterPackageId, StringComparison.OrdinalIgnoreCase) &&
                candidate.AfterPackageId.Equals(rule.BeforePackageId, StringComparison.OrdinalIgnoreCase));
            if (opposite is null) continue;

            quarantined.Add(rule.RuleId);
            quarantined.Add(opposite.RuleId);
            var blocking = rule.Confidence == LoadOrderRuleConfidence.Hard || opposite.Confidence == LoadOrderRuleConfidence.Hard;
            issues.Add(new ModAnalysisIssue(
                AnalysisIssueCode.CuratedRuleConflict,
                blocking ? AnalysisIssueSeverity.Error : AnalysisIssueSeverity.Warning,
                rule.BeforePackageId,
                "Conflicting curated load-order rules",
                $"Rules {rule.RuleId} and {opposite.RuleId} prescribe opposite ordering. Both rules were quarantined for this analysis so RimForge does not silently choose a winner.",
                new[] { rule.AfterPackageId }));
        }
        return quarantined;
    }

    private static void AddUseThisInsteadIssues(
        IReadOnlyDictionary<string, ModRecord> mods,
        string? targetRimWorldVersion,
        ICollection<ModAnalysisIssue> issues)
    {
        var database = UseThisInsteadDatabase.LoadDefault();
        foreach (var packageId in mods.Keys)
        {
            foreach (var rule in database.GetApplicable(packageId, targetRimWorldVersion))
            {
                var replacementInstalled = mods.ContainsKey(rule.ReplacementPackageId);
                issues.Add(new ModAnalysisIssue(
                    AnalysisIssueCode.ReplacementRecommended,
                    rule.Confidence == LoadOrderRuleConfidence.Experimental
                        ? AnalysisIssueSeverity.Information
                        : AnalysisIssueSeverity.Warning,
                    packageId,
                    "Use this instead",
                    $"{rule.Reason} Recommended replacement: {rule.ReplacementPackageId}. " +
                    (replacementInstalled ? "The replacement is already installed." : "The replacement is not currently installed.") +
                    $" Source: {rule.RuleId} · {rule.Source}",
                    new[] { rule.ReplacementPackageId }));
            }
        }
    }

    private static void AddEvidenceFindings(
        IReadOnlyDictionary<string, ModRecord> mods,
        IReadOnlyList<ForgeEvidenceContribution>? evidence,
        ICollection<ModAnalysisIssue> issues,
        ICollection<AnalysisRelationship> relationships,
        CancellationToken cancellationToken)
    {
        foreach (var item in (evidence ?? Array.Empty<ForgeEvidenceContribution>())
                     .OrderBy(value => value.EvidenceId, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(value => value.SubjectId, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(value => value.EvidenceType, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subject = ModNameResolver.Normalize(item.SubjectId);
            if (!mods.ContainsKey(subject) || !TryClassifyEvidence(item, out var code, out var title)) continue;
            var related = string.IsNullOrWhiteSpace(item.RelatedSubjectId)
                ? Array.Empty<string>()
                : new[] { ModNameResolver.Normalize(item.RelatedSubjectId) };
            var severity = EvidenceSeverity(item);
            issues.Add(new ModAnalysisIssue(
                code,
                severity,
                subject,
                title,
                $"{item.Summary} Evidence: {item.Provenance.SourceId}; confidence {item.Confidence:P0}; observed {Math.Max(1, item.ObservationCount)} time(s).",
                related,
                string.IsNullOrWhiteSpace(item.EvidenceId)
                    ? $"{item.Provenance.SourceId}:{item.SubjectId}:{item.RelatedSubjectId}:{item.EvidenceType}"
                    : item.EvidenceId));

            if (related.Length == 0 || !mods.ContainsKey(related[0])) continue;
            relationships.Add(new AnalysisRelationship(
                subject,
                related[0],
                AnalysisRelationshipKind.ObservedConflict,
                item.Summary,
                item.ConfidenceBand >= ForgeEvidenceConfidenceBand.High
                    ? LoadOrderRuleConfidence.Recommended
                    : LoadOrderRuleConfidence.Experimental,
                $"{item.Provenance.SourceKind} · {item.Provenance.SourceId}",
                false));
        }
    }

    private static bool TryClassifyEvidence(
        ForgeEvidenceContribution item,
        out AnalysisIssueCode code,
        out string title)
    {
        var type = item.EvidenceType;
        if (type.Equals("replacement-recommendation", StringComparison.OrdinalIgnoreCase))
        {
            code = AnalysisIssueCode.ReplacementRecommended;
            title = "Replacement recommended";
            return true;
        }
        if (type.Equals("declared-incompatibility", StringComparison.OrdinalIgnoreCase))
        {
            code = AnalysisIssueCode.CompatibilityEvidenceConcern;
            title = "Declared incompatibility";
            return true;
        }
        if (type.Equals("compatibility-assessment", StringComparison.OrdinalIgnoreCase))
        {
            var conflictScore = item.EffectiveAttributes.TryGetValue("conflictScore", out var value) &&
                                double.TryParse(value, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
            if (conflictScore < 0.5)
            {
                code = default;
                title = string.Empty;
                return false;
            }
            code = AnalysisIssueCode.RuntimeObservedConflict;
            title = "Runtime compatibility concern";
            return true;
        }
        if (item.Provenance.SourceKind != ForgeEvidenceSourceKind.RuntimeCompanion)
        {
            code = default;
            title = string.Empty;
            return false;
        }
        if (type.Contains("performance", StringComparison.OrdinalIgnoreCase))
        {
            code = AnalysisIssueCode.RuntimePerformanceRegression;
            title = "Runtime performance regression";
            return true;
        }
        if (type.Contains("integration", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("reflection", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("failure", StringComparison.OrdinalIgnoreCase))
        {
            code = AnalysisIssueCode.RuntimeIntegrationFailure;
            title = "Runtime integration failure";
            return true;
        }
        if (type.Contains("conflict", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("incompat", StringComparison.OrdinalIgnoreCase))
        {
            code = AnalysisIssueCode.RuntimeObservedConflict;
            title = "Observed runtime conflict";
            return true;
        }
        code = default;
        title = string.Empty;
        return false;
    }

    private static AnalysisIssueSeverity EvidenceSeverity(ForgeEvidenceContribution item)
    {
        if (item.Provenance.EffectiveAttributes.TryGetValue("severity", out var severity))
        {
            if (severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
                severity.Equals("Critical", StringComparison.OrdinalIgnoreCase))
                return AnalysisIssueSeverity.Error;
            if (severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                return AnalysisIssueSeverity.Warning;
        }
        return item.ConfidenceBand >= ForgeEvidenceConfidenceBand.High
            ? AnalysisIssueSeverity.Warning
            : AnalysisIssueSeverity.Information;
    }

    private static IReadOnlyList<IReadOnlyList<string>> FindCycles(
        IReadOnlyDictionary<string, List<string>> adjacency,
        CancellationToken cancellationToken)
    {
        var state = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cycles = new List<IReadOnlyList<string>>();

        foreach (var node in adjacency.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Visit(node);
        }
        return cycles;

        void Visit(string node)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state.TryGetValue(node, out var current))
            {
                if (current != 1) return;
                var index = stack.FindLastIndex(value => value.Equals(node, StringComparison.OrdinalIgnoreCase));
                if (index < 0) return;
                var cycle = stack.Skip(index).Append(node).ToArray();
                var key = string.Join("|", cycle.Take(cycle.Length - 1).OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
                if (seen.Add(key)) cycles.Add(cycle);
                return;
            }

            state[node] = 1;
            stack.Add(node);
            if (adjacency.TryGetValue(node, out var targets))
                foreach (var target in targets) Visit(target);
            stack.RemoveAt(stack.Count - 1);
            state[node] = 2;
        }
    }

    private sealed class AnalysisRunTelemetry
    {
        private readonly IProgress<AnalysisProgress>? _progress;
        private readonly int _total;
        private readonly CancellationToken _cancellationToken;
        private readonly Stopwatch _stageWatch = Stopwatch.StartNew();
        private readonly List<AnalysisStageMetrics> _stages = new();
        private AnalysisStage? _current;

        public AnalysisRunTelemetry(
            IProgress<AnalysisProgress>? progress,
            int total,
            CancellationToken cancellationToken)
        {
            _progress = progress;
            _total = total;
            _cancellationToken = cancellationToken;
        }

        public IReadOnlyList<AnalysisStageMetrics> Stages => _stages.ToArray();

        public void Advance(AnalysisStage stage, int completed, string message)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            CloseCurrentStage();
            _current = stage;
            _stageWatch.Restart();
            _progress?.Report(new AnalysisProgress(stage, completed, _total, message));
        }

        public void Complete(string message)
        {
            Advance(AnalysisStage.Complete, _total, message);
            CloseCurrentStage();
            _current = null;
        }

        private void CloseCurrentStage()
        {
            if (_current is not { } stage) return;
            _stageWatch.Stop();
            _stages.Add(new AnalysisStageMetrics(stage, _stageWatch.Elapsed));
        }
    }

    private static TopologicalOrderResult BuildTopologicalOrder(
        IReadOnlyDictionary<string, List<string>> hardAdjacency,
        IReadOnlyList<OrderingPreference> preferences,
        IReadOnlyList<string> preferredOrder,
        IReadOnlyDictionary<string, ModRecord> mods,
        string? targetRimWorldVersion,
        CancellationToken cancellationToken)
    {
        var rank = preferredOrder
            .Select((packageId, index) => (packageId, index))
            .Where(item => !string.IsNullOrWhiteSpace(item.packageId))
            .GroupBy(item => item.packageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);

        var inDegree = hardAdjacency.Keys.ToDictionary(key => key, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var targets in hardAdjacency.Values)
            foreach (var target in targets)
                if (inDegree.ContainsKey(target)) inDegree[target]++;

        // All expensive classification and preference work is calculated once per plan. The stable
        // topological queue then compares only primitive values, which keeps large profiles responsive.
        var classifications = hardAdjacency.Keys.ToDictionary(
            packageId => packageId,
            packageId => mods.TryGetValue(packageId, out var mod)
                ? LoadOrderPolicy.Classify(mod, targetRimWorldVersion)
                : new LoadOrderClassification(LoadOrderCategory.Uncategorized, "No metadata is available.",
                    Confidence: LoadOrderRuleConfidence.Experimental),
            StringComparer.OrdinalIgnoreCase);

        var preferenceScore = hardAdjacency.Keys.ToDictionary(
            packageId => packageId, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var preference in preferences.Where(item => !item.IsMandatory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var weight = preference.Confidence switch
            {
                LoadOrderRuleConfidence.Recommended => 100,
                LoadOrderRuleConfidence.Experimental => 10,
                _ => 0
            };
            if (weight == 0) continue;
            if (preferenceScore.ContainsKey(preference.BeforePackageId))
                preferenceScore[preference.BeforePackageId] += weight;
            if (preferenceScore.ContainsKey(preference.AfterPackageId))
                preferenceScore[preference.AfterPackageId] -= weight;
        }

        var available = new List<string>(inDegree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var ordered = new List<string>(hardAdjacency.Count);
        while (available.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            available.Sort((left, right) =>
            {
                var leftAnchor = GetAnchorPriority(left);
                var rightAnchor = GetAnchorPriority(right);
                var byAnchorBand = leftAnchor.Band.CompareTo(rightAnchor.Band);
                if (byAnchorBand != 0) return byAnchorBand;
                var byAnchorIndex = leftAnchor.Index.CompareTo(rightAnchor.Index);
                if (byAnchorIndex != 0 && leftAnchor.Band != 1) return byAnchorIndex;

                // Curated recommendations influence only nodes that are simultaneously eligible.
                // They never become graph edges and therefore cannot create or hide a hard cycle.
                var byPreference = preferenceScore[right].CompareTo(preferenceScore[left]);
                if (byPreference != 0) return byPreference;

                var byCategory = ((int)classifications[left].Category)
                    .CompareTo((int)classifications[right].Category);
                if (byCategory != 0) return byCategory;

                var leftRank = rank.GetValueOrDefault(left, int.MaxValue);
                var rightRank = rank.GetValueOrDefault(right, int.MaxValue);
                var byRank = leftRank.CompareTo(rightRank);
                return byRank != 0 ? byRank : StringComparer.OrdinalIgnoreCase.Compare(left, right);
            });

            var node = available[0];
            available.RemoveAt(0);
            ordered.Add(node);
            foreach (var target in hardAdjacency[node])
            {
                if (--inDegree[target] == 0) available.Add(target);
            }
        }

        var blocked = inDegree.Where(pair => pair.Value > 0).Select(pair => pair.Key)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        var finalRank = ordered.Select((packageId, index) => (packageId, index))
            .ToDictionary(item => item.packageId, item => item.index, StringComparer.OrdinalIgnoreCase);
        var decisions = ordered.Select(packageId => BuildDecision(
            packageId,
            rank.GetValueOrDefault(packageId, -1),
            finalRank[packageId],
            classifications[packageId],
            preferences,
            finalRank)).ToArray();

        return new TopologicalOrderResult(
            blocked.Length == 0,
            ordered,
            blocked,
            blocked.Length == 0
                ? $"Generated a deterministic tri-hybrid order for {ordered.Count} mod(s): hard dependency and metadata constraints were preserved, curated recommendations were applied as preferences, and category policy plus the existing profile order resolved remaining ties."
                : $"Generated a partial tri-hybrid order for {ordered.Count} mod(s); {blocked.Length} mod(s) remain blocked by hard-constraint cycles.")
        {
            Decisions = decisions
        };
    }

    private static LoadOrderDecision BuildDecision(
        string packageId,
        int previousIndex,
        int proposedIndex,
        LoadOrderClassification classification,
        IReadOnlyList<OrderingPreference> preferences,
        IReadOnlyDictionary<string, int> finalRank)
    {
        if (LoadOrderRules.IsPositionAnchor(packageId))
        {
            return new LoadOrderDecision(
                packageId, previousIndex, proposedIndex,
                LoadOrderRules.IsTopAnchor(packageId)
                    ? "Canonical top-anchor placement"
                    : "Canonical bottom-anchor placement",
                "RimForge official-content and anchor policy",
                LoadOrderRuleConfidence.Hard,
                true,
                Array.Empty<string>());
        }

        var mandatory = preferences
            .Where(item => item.IsMandatory &&
                item.AfterPackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase) &&
                finalRank.ContainsKey(item.BeforePackageId))
            .OrderByDescending(item => item.Confidence)
            .ThenBy(item => item.BeforePackageId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (mandatory is not null)
        {
            return new LoadOrderDecision(
                packageId, previousIndex, proposedIndex,
                mandatory.Reason,
                mandatory.Source,
                mandatory.Confidence,
                true,
                new[] { mandatory.BeforePackageId });
        }

        var recommendation = preferences
            .Where(item => !item.IsMandatory &&
                (item.BeforePackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase) ||
                 item.AfterPackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.Confidence)
            .ThenBy(item => item.BeforePackageId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (recommendation is not null)
        {
            var related = recommendation.BeforePackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase)
                ? recommendation.AfterPackageId
                : recommendation.BeforePackageId;
            return new LoadOrderDecision(
                packageId, previousIndex, proposedIndex,
                recommendation.Reason,
                recommendation.Source,
                recommendation.Confidence,
                false,
                new[] { related });
        }

        return new LoadOrderDecision(
            packageId, previousIndex, proposedIndex,
            classification.Reason,
            classification.RuleSource,
            classification.Confidence,
            classification.Confidence == LoadOrderRuleConfidence.Hard,
            Array.Empty<string>());
    }

    private static (int Band, int Index) GetAnchorPriority(string packageId)
    {
        var top = LoadOrderRules.TopAnchors
            .Select((id, index) => (id, index))
            .FirstOrDefault(item => item.id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        if (top.id is not null) return (0, top.index);

        var bottom = LoadOrderRules.BottomAnchors
            .Select((id, index) => (id, index))
            .FirstOrDefault(item => item.id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        return bottom.id is not null ? (2, bottom.index) : (1, 0);
    }

}

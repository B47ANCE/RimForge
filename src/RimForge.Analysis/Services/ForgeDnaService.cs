using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using RimForge.Analysis.Models;
using RimForge.Core.Models;

namespace RimForge.Analysis.Services;

public sealed class ForgeDnaService : IForgeDnaService
{
    private readonly IModAnalysisEngine _analysisEngine;
    private readonly object _sync = new();
    private readonly Dictionary<string, ForgeDnaRecord> _cache = new(StringComparer.OrdinalIgnoreCase);
    private ForgeDnaSnapshot? _current;

    public ForgeDnaService(IModAnalysisEngine analysisEngine) => _analysisEngine = analysisEngine;

    public ForgeDnaSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return _current ?? EmptySnapshot();
            }
        }
    }

    public async Task<ForgeDnaSnapshot> AnalyzeAsync(
        IReadOnlyList<ModRecord> mods,
        IReadOnlyList<string>? currentLoadOrder = null,
        string? targetRimWorldVersion = null,
        IReadOnlyList<ForgeEvidenceContribution>? evidence = null,
        IProgress<ForgeDnaProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await AnalyzeCoreAsync(mods, currentLoadOrder, targetRimWorldVersion, evidence, progress, cancellationToken).ConfigureAwait(false);
    }

    public void Invalidate(string? packageId = null)
    {
        lock (_sync)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                _cache.Clear();
            else
                _cache.Remove(packageId);
            _current = null;
        }
    }

    private async Task<ForgeDnaSnapshot> AnalyzeCoreAsync(
        IReadOnlyList<ModRecord> mods,
        IReadOnlyList<string>? currentLoadOrder,
        string? targetRimWorldVersion,
        IReadOnlyList<ForgeEvidenceContribution>? evidence,
        IProgress<ForgeDnaProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        progress?.Report(new ForgeDnaProgress("Relationships", "Building shared dependency and issue analysis.", 0, Math.Max(1, mods.Count)));
        cancellationToken.ThrowIfCancellationRequested();
        var analysisResult = await _analysisEngine.AnalyzeAsync(
            new ModAnalysisRequest(mods, currentLoadOrder, targetRimWorldVersion, Evidence: evidence),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var analysis = analysisResult.Snapshot;

        var records = new List<ForgeDnaRecord>(mods.Count);
        var reused = 0;
        var rebuilt = 0;

        for (var index = 0; index < mods.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mod = mods[index];
            var packageId = NormalizePackageId(mod);
            var fingerprint = CreateFingerprint(mod);
            ForgeDnaRecord record;

            lock (_sync)
            {
                if (_cache.TryGetValue(packageId, out var cached) && cached.Fingerprint.Value == fingerprint.Value)
                {
                    record = RefreshAnalysisProjection(cached, analysis);
                    reused++;
                }
                else
                {
                    record = BuildRecord(mod, packageId, fingerprint, analysis);
                    rebuilt++;
                }
                _cache[packageId] = record;
            }

            records.Add(record);
            progress?.Report(new ForgeDnaProgress("Forge DNA", $"Analyzing {mod.DisplayName}\n{mod.RootPath}", index + 1, mods.Count));
        }

        stopwatch.Stop();
        var snapshot = new ForgeDnaSnapshot(
            records.OrderBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray(),
            analysis,
            new ForgeDnaMetrics(mods.Count, reused, rebuilt, stopwatch.Elapsed, DateTimeOffset.Now));

        lock (_sync) _current = snapshot;
        progress?.Report(new ForgeDnaProgress("Complete", $"Forge DNA ready: {rebuilt} rebuilt, {reused} reused.", mods.Count, mods.Count));
        return snapshot;
    }

    private static ForgeDnaRecord BuildRecord(
        ModRecord mod,
        string packageId,
        ForgeDnaFingerprint fingerprint,
        ModAnalysisSnapshot analysis)
    {
        var issues = analysis.GetIssues(packageId);
        var technologies = mod.Evidence.Badges
            .OrderBy(badge => badge.Kind)
            .Select(badge => new ForgeDnaTechnology(
                badge.Kind,
                badge.Label,
                badge.Count,
                badge.Summary,
                badge.FileList))
            .ToArray();

        return new ForgeDnaRecord(
            mod.Id,
            packageId,
            mod.DisplayName,
            mod.Author,
            mod.Source,
            mod.RootPath,
            mod.SupportedVersions,
            analysis.GetDependencies(packageId),
            analysis.GetDependents(packageId),
            technologies,
            mod.Evidence.Capabilities,
            mod.Evidence.NotableFindings,
            issues,
            ResolveHealth(issues),
            fingerprint);
    }

    private static ForgeDnaRecord RefreshAnalysisProjection(ForgeDnaRecord record, ModAnalysisSnapshot analysis)
    {
        var issues = analysis.GetIssues(record.PackageId);
        return record with
        {
            Dependencies = analysis.GetDependencies(record.PackageId),
            Dependents = analysis.GetDependents(record.PackageId),
            Issues = issues,
            Health = ResolveHealth(issues)
        };
    }

    private static ForgeDnaHealthState ResolveHealth(IReadOnlyList<ModAnalysisIssue> issues)
    {
        if (issues.Any(issue => issue.Severity == AnalysisIssueSeverity.Error)) return ForgeDnaHealthState.Error;
        if (issues.Any(issue => issue.Severity == AnalysisIssueSeverity.Warning)) return ForgeDnaHealthState.Warning;
        if (issues.Any(issue => issue.Severity == AnalysisIssueSeverity.Information)) return ForgeDnaHealthState.Information;
        return ForgeDnaHealthState.Healthy;
    }

    private static string NormalizePackageId(ModRecord mod) =>
        string.IsNullOrWhiteSpace(mod.PackageId) ? $"missing:{mod.Id}" : mod.PackageId.Trim();

    private static ForgeDnaFingerprint CreateFingerprint(ModRecord mod)
    {
        var input = string.Join("|",
            mod.Id,
            mod.PackageId,
            mod.LastModified.UtcTicks,
            mod.Evidence.TotalFiles,
            mod.Evidence.TotalBytes,
            mod.Evidence.DefinitionCount,
            mod.Evidence.PatchOperationCount,
            string.Join(",", mod.Dependencies.Select(dependency => dependency.PackageId).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
            string.Join(",", mod.LoadBefore.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
            string.Join(",", mod.LoadAfter.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
            string.Join(",", mod.Evidence.Badges.Select(badge => $"{badge.Kind}:{badge.Count}").OrderBy(value => value, StringComparer.Ordinal)));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new ForgeDnaFingerprint(
            Convert.ToHexString(hash),
            DateTimeOffset.Now,
            mod.LastModified,
            mod.Evidence.TotalFiles,
            mod.Dependencies.Count);
    }

    private static ForgeDnaSnapshot EmptySnapshot()
    {
        var analysis = new ModAnalysisEngine().Analyze(Array.Empty<ModRecord>());
        return new ForgeDnaSnapshot(
            Array.Empty<ForgeDnaRecord>(),
            analysis,
            new ForgeDnaMetrics(0, 0, 0, TimeSpan.Zero, DateTimeOffset.MinValue));
    }
}

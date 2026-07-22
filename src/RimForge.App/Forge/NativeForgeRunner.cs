using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using RimForge.Analysis.Models;
using RimForge.Analysis.Services;
using RimForge.Core.Models;

namespace RimForge.App.Forge;

public sealed record NativeForgeResult(
    ModAnalysisSnapshot Snapshot,
    AuditSummary Summary,
    string Result,
    TimeSpan Elapsed,
    int ErrorCount,
    int WarningCount,
    int InformationCount,
    IReadOnlyList<string> WrittenReports);

public sealed class NativeForgeRunner
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private readonly IForgeDnaService _forgeDnaService;

    public NativeForgeRunner(IForgeDnaService forgeDnaService) => _forgeDnaService = forgeDnaService;

    public bool IsRunning { get; private set; }

    public async Task<NativeForgeResult> RunAsync(
        IReadOnlyList<ModRecord> mods,
        RimForgeProfile? profile,
        string targetRimWorldVersion,
        IReadOnlyList<ForgeEvidenceContribution>? evidence,
        string reportsRoot,
        IProgress<ForgeProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (IsRunning) throw new InvalidOperationException("A native Forge session is already running.");
        IsRunning = true;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            progress?.Report(new ForgeProgress(ForgePhase.Configuration,
                $"Preparing native Forge session for {profile?.Name ?? "the current library"}.", 0.05, 1.0, 1, 9));

            var activeLoadOrder = profile?.ActiveMods?.ToArray();
            progress?.Report(new ForgeProgress(ForgePhase.DependencyGraph,
                $"Mapping dependencies for {mods.Count} installed mod(s).", 0.25, 0.2, 2, 9));

            var nameResolver = new ModNameResolver(mods);
            var forgeDna = await _forgeDnaService.AnalyzeAsync(
                mods,
                activeLoadOrder,
                targetRimWorldVersion,
                evidence,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var snapshot = forgeDna.Analysis;

            progress?.Report(new ForgeProgress(ForgePhase.ProfileProcessing,
                $"Evaluating profile health for {profile?.Name ?? "the full library"}.", 0.55, 0.65, 3, 9));

            var scopedIssues = activeLoadOrder is null
                ? snapshot.Issues
                : snapshot.Issues.Where(issue => activeLoadOrder.Contains(issue.PackageId, StringComparer.OrdinalIgnoreCase)).ToArray();
            var errors = scopedIssues.Count(issue => issue.Severity == AnalysisIssueSeverity.Error);
            var warnings = scopedIssues.Count(issue => issue.Severity == AnalysisIssueSeverity.Warning);
            var information = scopedIssues.Count(issue => issue.Severity == AnalysisIssueSeverity.Information);

            Directory.CreateDirectory(reportsRoot);
            var writtenReports = new List<string>();
            var nativeReportPath = Path.Combine(reportsRoot, "NativeForgeReport.json");
            progress?.Report(new ForgeProgress(ForgePhase.ReportGeneration,
                $"Writing native Forge analysis.\n{nativeReportPath}", 0.72, 0.25, 4, 9));

            var nativeReport = new
            {
                Generated = DateTimeOffset.Now,
                Engine = "Native .NET",
                Profile = profile?.Name,
                ModsAnalyzed = mods.Count,
                ActiveModsAnalyzed = activeLoadOrder?.Length ?? mods.Count,
                Relationships = snapshot.Relationships.Count,
                DependencyCycles = snapshot.LoadOrderPlan.CycleGroups,
                ProposedOrder = snapshot.LoadOrderPlan,
                Issues = snapshot.Issues.Select(issue => new
                {
                    issue.Code,
                    issue.Severity,
                    ModName = nameResolver.Resolve(issue.PackageId),
                    issue.PackageId,
                    issue.Title,
                    issue.Explanation,
                    RelatedMods = issue.RelatedPackageIds.Select(id => new { Name = nameResolver.Resolve(id), PackageId = id }).ToArray()
                }).ToArray(),
                ProfileIssues = scopedIssues.Select(issue => new
                {
                    issue.Code,
                    issue.Severity,
                    ModName = nameResolver.Resolve(issue.PackageId),
                    issue.PackageId,
                    issue.Title,
                    issue.Explanation,
                    RelatedMods = issue.RelatedPackageIds.Select(id => new { Name = nameResolver.Resolve(id), PackageId = id }).ToArray()
                }).ToArray()
            };
            await WriteJsonAtomicAsync(nativeReportPath, nativeReport, cancellationToken).ConfigureAwait(false);
            writtenReports.Add(nativeReportPath);

            var evidenceReportPath = Path.Combine(reportsRoot, "NativeEvidenceReport.json");
            progress?.Report(new ForgeProgress(ForgePhase.EvidenceScan,
                $"Writing native Evidence report.\n{evidenceReportPath}", 0.8, 0.5, 5, 9));
            var evidenceScope = activeLoadOrder is null
                ? mods
                : mods.Where(mod => mod.PackageId is not null && activeLoadOrder.Contains(mod.PackageId, StringComparer.OrdinalIgnoreCase)).ToArray();
            var evidenceReport = new
            {
                Generated = DateTimeOffset.Now,
                Engine = "Native .NET",
                Profile = profile?.Name,
                ModsConsidered = evidenceScope.Count,
                Totals = new
                {
                    Files = evidenceScope.Sum(mod => mod.Evidence.TotalFiles),
                    Bytes = evidenceScope.Sum(mod => mod.Evidence.TotalBytes),
                    XmlFiles = evidenceScope.Sum(mod => mod.Evidence.XmlFiles),
                    Assemblies = evidenceScope.Sum(mod => mod.Evidence.AssemblyFiles),
                    Textures = evidenceScope.Sum(mod => mod.Evidence.TextureFiles),
                    Audio = evidenceScope.Sum(mod => mod.Evidence.AudioFiles),
                    Definitions = evidenceScope.Sum(mod => mod.Evidence.DefinitionCount),
                    PatchOperations = evidenceScope.Sum(mod => mod.Evidence.PatchOperationCount)
                },
                Mods = evidenceScope
                    .OrderBy(mod => mod.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(mod => new
                    {
                        ModName = mod.DisplayName,
                        mod.PackageId,
                        mod.RootPath,
                        mod.Source,
                        mod.Evidence.TotalFiles,
                        mod.Evidence.TotalBytes,
                        Badges = mod.Evidence.Badges.Select(badge => new { badge.Kind, badge.Label, badge.Count, badge.Summary }).ToArray(),
                        mod.Evidence.Capabilities,
                        mod.Evidence.NotableFindings
                    }).ToArray()
            };
            await WriteJsonAtomicAsync(evidenceReportPath, evidenceReport, cancellationToken).ConfigureAwait(false);
            writtenReports.Add(evidenceReportPath);

            var forgeDnaReportPath = Path.Combine(reportsRoot, "ForgeDnaReport.json");
            progress?.Report(new ForgeProgress(ForgePhase.EvidenceScan,
                $"Writing shared Forge DNA report.\n{forgeDnaReportPath}", 0.84, 0.6, 6, 9));
            var forgeDnaReport = new
            {
                Generated = forgeDna.Metrics.GeneratedAt,
                Engine = "Forge DNA",
                forgeDna.Metrics,
                Mods = forgeDna.Records.Select(record => new
                {
                    record.DisplayName,
                    record.PackageId,
                    record.Author,
                    record.Source,
                    record.Health,
                    record.SupportedVersions,
                    record.Dependencies,
                    record.Dependents,
                    Technologies = record.Technologies.Select(technology => new { technology.Kind, technology.Label, technology.Count, technology.Summary }),
                    record.Capabilities,
                    record.Findings,
                    Issues = record.Issues.Select(issue => new { issue.Code, issue.Severity, issue.Title, issue.Explanation }),
                    Fingerprint = record.Fingerprint.Value
                }).ToArray()
            };
            await WriteJsonAtomicAsync(forgeDnaReportPath, forgeDnaReport, cancellationToken).ConfigureAwait(false);
            writtenReports.Add(forgeDnaReportPath);

            var compatibilityReportPath = Path.Combine(reportsRoot, "NativeCompatibilityReport.json");
            progress?.Report(new ForgeProgress(ForgePhase.VersionChecks,
                $"Writing native compatibility report.\n{compatibilityReportPath}", 0.86, 0.65, 7, 9));
            var compatibilityReport = new
            {
                Generated = DateTimeOffset.Now,
                Engine = "Native .NET",
                Profile = profile?.Name,
                TargetRimWorldVersion = targetRimWorldVersion,
                Mods = evidenceScope
                    .OrderBy(mod => mod.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(mod => new
                    {
                        ModName = mod.DisplayName,
                        mod.PackageId,
                        mod.SupportedVersions,
                        SupportsTargetVersion = mod.IsOfficialContent || mod.SupportedVersions.Count == 0 || mod.SupportedVersions.Contains(targetRimWorldVersion, StringComparer.OrdinalIgnoreCase),
                        MetadataErrors = mod.IsOfficialContent
                            ? mod.Errors.Where(error => !error.Equals("Missing name", StringComparison.OrdinalIgnoreCase)).ToArray()
                            : mod.Errors
                    }).ToArray(),
                UnsupportedMods = evidenceScope
                    .Where(mod => !mod.IsOfficialContent && mod.SupportedVersions.Count > 0 && !mod.SupportedVersions.Contains(targetRimWorldVersion, StringComparer.OrdinalIgnoreCase))
                    .Select(mod => new { ModName = mod.DisplayName, mod.PackageId, mod.SupportedVersions })
                    .ToArray()
            };
            await WriteJsonAtomicAsync(compatibilityReportPath, compatibilityReport, cancellationToken).ConfigureAwait(false);
            writtenReports.Add(compatibilityReportPath);

            var summaryPath = Path.Combine(reportsRoot, "ForgeSummary.json");
            progress?.Report(new ForgeProgress(ForgePhase.ReportGeneration,
                $"Writing Forge completion summary.\n{summaryPath}", 0.9, 0.8, 8, 9));

            stopwatch.Stop();
            var result = errors > 0 || warnings > 0 ? "CompletedWithConditions" : "Completed";
            var summaryDocument = new
            {
                Generated = DateTimeOffset.Now,
                Result = result,
                Engine = "Native .NET",
                Profile = profile?.Name,
                ModsAnalyzed = mods.Count,
                ProfilesAnalyzed = profile is null ? 0 : 1,
                DurationSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 3),
                ErrorCount = errors,
                WarningCount = warnings,
                InformationCount = information,
                CycleCount = snapshot.Cycles.Count,
                Conditions = scopedIssues.Select(issue => new
                {
                    Severity = issue.Severity.ToString().ToUpperInvariant(),
                    Subsystem = issue.Code.ToString(),
                    ModName = nameResolver.Resolve(issue.PackageId),
                    Message = issue.Explanation
                }).ToArray()
            };
            await WriteJsonAtomicAsync(summaryPath, summaryDocument, cancellationToken).ConfigureAwait(false);
            writtenReports.Add(summaryPath);

            var auditSummary = new AuditSummary(
                mods.Count,
                scopedIssues.Count(issue => issue.Code is AnalysisIssueCode.MissingRequiredDependency or AnalysisIssueCode.InactiveRequiredDependency),
                snapshot.Cycles.Count,
                scopedIssues.Count(issue => issue.Code == AnalysisIssueCode.MissingPackageId),
                DateTimeOffset.Now);

            progress?.Report(new ForgeProgress(ForgePhase.Complete,
                $"Native Forge complete. Wrote {writtenReports.Count} report(s) to {reportsRoot}.", 1.0, 1.0, 9, 9));

            return new NativeForgeResult(snapshot, auditSummary, result, stopwatch.Elapsed, errors, warnings, information, writtenReports);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private static async Task WriteJsonAtomicAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var temporaryPath = path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        File.Move(temporaryPath, path, overwrite: true);
    }
}

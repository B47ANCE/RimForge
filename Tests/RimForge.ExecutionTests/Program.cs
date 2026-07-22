using RimForge.Core.BackgroundTasks;
using RimForge.Core.Diagnostics;
using RimForge.Core.Models;
using RimForge.Core.Services;
using RimForge.Analysis.Models;
using RimForge.Analysis.Services;
using RimForge.Infrastructure.Services;
using RimForge.Companion.Host;
using RimForge.Protocol.Contracts;
using RimForge.Protocol.Serialization;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var snapshots = new List<BackgroundTaskSnapshot>();
var snapshotGate = new object();
var service = new BackgroundTaskService();
service.TaskChanged += (_, snapshot) =>
{
    lock (snapshotGate) snapshots.Add(snapshot);
};

var result = await service.RunAsync(
    "test.complete",
    "Completion Test",
    async context =>
    {
        context.Report(new BackgroundTaskProgress(
            "Processing",
            "Processing item one",
            "technical detail",
            25,
            1,
            4,
            "discovered item one",
            "item-one.xml"));
        await Task.Delay(20, context.CancellationToken);
        return 42;
    });

Require(result == 42, "The generic task result was not returned.");
Require(service.Current.State == BackgroundTaskState.Completed, "Completion state was not published.");
var completedProgress = service.Current.Progress;
Require(completedProgress is not null, "Completion progress was not retained.");
Require(completedProgress!.EffectivePercent == 100, "Completion did not normalize progress to 100 percent.");
Require(completedProgress.Completed == 4 && completedProgress.Total == 4, "Completion counts were not normalized.");
Require(completedProgress.TechnicalDetail == "technical detail", "Technical detail was not preserved.");
Require(completedProgress.DiscoveryDetail == "discovered item one", "Discovery detail was not preserved.");
Require(completedProgress.CurrentFile == "item-one.xml", "Current file was not preserved.");
Require(service.Current.Elapsed > TimeSpan.Zero, "Elapsed time was not recorded.");
Require(SawState(BackgroundTaskState.Running) && SawState(BackgroundTaskState.Completed), "Running/completed events were not both published.");

var cancellationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var cancellationTask = service.RunAsync(
    "test.cancel",
    "Cancellation Test",
    async context =>
    {
        context.Report(new BackgroundTaskProgress(
            "Waiting",
            "Waiting for cancellation",
            "cancel detail",
            null,
            2,
            10,
            "cancel discovery",
            "cancel-item.xml"));
        cancellationStarted.TrySetResult();
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            context.Report(new BackgroundTaskProgress(
                "Late progress",
                "This update must be ignored",
                "late detail",
                90,
                9,
                10,
                "late discovery",
                "late-item.xml"));
            throw;
        }
        return 0;
    });

await cancellationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
Require(service.CancelCurrent("Test cancellation requested."), "Cancellation request was not accepted.");
Require(service.Current.State == BackgroundTaskState.Cancelling, "Cancelling state was not published.");
await RequireThrowsAsync<OperationCanceledException>(() => cancellationTask, "Cancellation did not propagate to the caller.");
Require(service.Current.State == BackgroundTaskState.Cancelled, "Cancelled state was not published.");
Require(service.Current.Progress?.TechnicalDetail == "cancel detail", "Late progress overwrote cancellation detail.");
Require(service.Current.Progress?.CurrentFile == "cancel-item.xml", "Late progress overwrote the cancellation current file.");
Require(!service.Current.IsActive, "Cancellation left the service active.");

await RequireThrowsAsync<InvalidOperationException>(
    () => service.RunAsync<int>(
        "test.failure",
        "Failure Test",
        _ => throw new InvalidOperationException("expected failure")),
    "Failure did not propagate to the caller.");
Require(service.Current.State == BackgroundTaskState.Failed, "Failed state was not published.");
Require(service.Current.ErrorMessage == "expected failure", "Failure detail was not captured.");
Require(!service.Current.IsActive, "Failure left the service active.");

var recovery = await service.RunAsync(
    "test.recovery",
    "Recovery Test",
    context =>
    {
        context.Report(new BackgroundTaskProgress("Recovery", "Recovered", "recovery detail", 100, 1, 1));
        return Task.FromResult("ready");
    });
Require(recovery == "ready", "A new task could not run after failure.");
Require(service.Current.State == BackgroundTaskState.Completed, "Recovery task did not complete.");

var hostedEvents = new List<HostedBackgroundWorkSnapshot>();
await using (var hostedWork = new HostedBackgroundWorkService())
{
    hostedWork.WorkChanged += (_, snapshot) => hostedEvents.Add(snapshot);
    var hostedStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    await hostedWork.StartAsync(
        "test.listener",
        "Hosted listener",
        async cancellationToken =>
        {
            hostedStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
    await hostedStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    await hostedWork.StartAsync("test.listener", "Duplicate listener", _ => Task.CompletedTask);
    Require(hostedWork.Snapshot.Count(item => item.Key == "test.listener") == 1,
        "Hosted work allowed a duplicate registration for the same key.");
    Require(hostedWork.Snapshot.Single(item => item.Key == "test.listener").State == HostedBackgroundWorkState.Running,
        "Hosted work did not publish its running state.");
    await hostedWork.StopAsync("test.listener");
    Require(hostedWork.Snapshot.Single(item => item.Key == "test.listener").State == HostedBackgroundWorkState.Stopped,
        "Hosted work did not publish its stopped state.");
}
Require(hostedEvents.Any(item => item.State == HostedBackgroundWorkState.Running) &&
        hostedEvents.Any(item => item.State == HostedBackgroundWorkState.Stopped),
    "Hosted work lifecycle events were incomplete.");

var analysisObservedAt = DateTimeOffset.UtcNow;
var analysisAlpha = new ModRecord
{
    Id = "analysis-alpha",
    PackageId = "example.analysis.alpha",
    Name = "Analysis Alpha",
    FolderName = "AnalysisAlpha",
    RootPath = Path.Combine(Path.GetTempPath(), "analysis-alpha"),
    AboutPath = Path.Combine(Path.GetTempPath(), "analysis-alpha", "About", "About.xml"),
    LastModified = analysisObservedAt,
    Dependencies = [new ModDependency("example.analysis.beta", "Analysis Beta", null, null, "fixture")]
};
var analysisBeta = new ModRecord
{
    Id = "analysis-beta",
    PackageId = "example.analysis.beta",
    Name = "Analysis Beta",
    FolderName = "AnalysisBeta",
    RootPath = Path.Combine(Path.GetTempPath(), "analysis-beta"),
    AboutPath = Path.Combine(Path.GetTempPath(), "analysis-beta", "About", "About.xml"),
    LastModified = analysisObservedAt
};
var analysisEngine = new ModAnalysisEngine();
var analysisProgress = new List<AnalysisProgress>();
var analysisFirst = await analysisEngine.AnalyzeAsync(new ModAnalysisRequest(
    [analysisAlpha, analysisBeta], ["example.analysis.alpha"], "1.6"),
    new InlineProgress<AnalysisProgress>(analysisProgress.Add));
var analysisSecond = await analysisEngine.AnalyzeAsync(new ModAnalysisRequest(
    [analysisBeta, analysisAlpha], ["example.analysis.alpha"], "1.6"));
Require(analysisFirst.Metrics.InstalledLibraryCount == 2 && analysisFirst.Metrics.ActiveProfileCount == 1,
    "Analysis metrics did not distinguish the installed library from active profile scope.");
Require(analysisFirst.Snapshot.Relationships.Count == 1 &&
        analysisFirst.Metrics.InputFingerprint == analysisSecond.Metrics.InputFingerprint,
    "Analysis was not deterministic across installed-library input ordering.");
Require(analysisFirst.Cache.Disposition == AnalysisCacheDisposition.Miss &&
        analysisSecond.Cache.Disposition == AnalysisCacheDisposition.Hit &&
        ReferenceEquals(analysisFirst.Snapshot, analysisSecond.Snapshot) &&
        ReferenceEquals(analysisFirst.Explainability, analysisSecond.Explainability),
    "Analysis did not reuse the unchanged deterministic result.");
var analysisOverview = analysisFirst.Explainability.Overview;
var alphaExplanation = analysisFirst.Explainability.GetMod("EXAMPLE.ANALYSIS.ALPHA");
var betaExplanation = analysisFirst.Explainability.GetMod("example.analysis.beta");
Require(analysisOverview.InstalledModCount == 2 && analysisOverview.ActiveModCount == 1 &&
        analysisOverview.HealthyModCount == 1 && analysisOverview.AffectedModCount == 1 &&
        analysisOverview.ErrorCount == 1 && analysisOverview.Status == "Blocked",
    "Analysis overview did not summarize full-library and active-profile findings.");
Require(alphaExplanation is { IsActive: true } &&
        alphaExplanation.Diagnostics.Any(item => item.Code == AnalysisIssueCode.InactiveRequiredDependency.ToString()) &&
        alphaExplanation.Relationships.Count == 1 &&
        alphaExplanation.Recommendations.Any(item => item.Kind == RepairActionKind.ActivateDependency) &&
        betaExplanation is { IsActive: false } && betaExplanation.Relationships.Count == 1,
    "Per-mod explainability did not combine findings, relationships, recommendations, and profile state.");
Require(analysisProgress.Select(item => item.Stage).SequenceEqual(Enum.GetValues<AnalysisStage>()),
    "Analysis did not publish every typed stage in canonical order.");
Require(analysisFirst.Stages.Select(item => item.Stage).SequenceEqual(Enum.GetValues<AnalysisStage>()) &&
        analysisFirst.Stages.All(item => item.Elapsed >= TimeSpan.Zero),
    "Analysis did not return complete per-stage execution metrics.");
Require(analysisFirst.Diagnostics.Count == analysisFirst.Snapshot.Issues.Count &&
        analysisFirst.Diagnostics.Any(item => item.Code == AnalysisIssueCode.InactiveRequiredDependency.ToString() &&
                                              item.PackageId == "example.analysis.alpha"),
    "Analysis diagnostics did not project every issue with package context.");
var analysisLockedFirst = await analysisEngine.AnalyzeAsync(new ModAnalysisRequest(
    [analysisAlpha, analysisBeta], ["example.analysis.alpha", "example.analysis.beta"], "1.6",
    [new UserLoadOrderLock("example.analysis.alpha", 0, analysisObservedAt)]));
var analysisLockedSecond = await analysisEngine.AnalyzeAsync(new ModAnalysisRequest(
    [analysisAlpha, analysisBeta], ["example.analysis.alpha", "example.analysis.beta"], "1.6",
    [new UserLoadOrderLock("example.analysis.alpha", 1, analysisObservedAt)]));
Require(analysisLockedFirst.Metrics.InputFingerprint != analysisLockedSecond.Metrics.InputFingerprint,
    "Analysis fingerprint did not include user load-order locks.");
var analysisRefreshed = await analysisEngine.AnalyzeAsync(new ModAnalysisRequest(
    [analysisAlpha, analysisBeta], ["example.analysis.alpha"], "1.6",
    CachePolicy: AnalysisCachePolicy.Refresh));
Require(analysisRefreshed.Cache.Disposition == AnalysisCacheDisposition.Refreshed &&
        !ReferenceEquals(analysisFirst.Snapshot, analysisRefreshed.Snapshot),
    "Refresh policy reused an existing analysis result.");
var analysisBypassed = await analysisEngine.AnalyzeAsync(new ModAnalysisRequest(
    [analysisAlpha, analysisBeta], ["example.analysis.alpha"], "1.6",
    CachePolicy: AnalysisCachePolicy.Bypass));
Require(analysisBypassed.Cache.Disposition == AnalysisCacheDisposition.Bypassed &&
        !ReferenceEquals(analysisRefreshed.Snapshot, analysisBypassed.Snapshot),
    "Bypass policy did not execute an isolated analysis run.");
analysisEngine.InvalidateCache(analysisFirst.Metrics.InputFingerprint);
var analysisAfterInvalidation = await analysisEngine.AnalyzeAsync(new ModAnalysisRequest(
    [analysisAlpha, analysisBeta], ["example.analysis.alpha"], "1.6"));
Require(analysisAfterInvalidation.Cache.Disposition == AnalysisCacheDisposition.Miss,
    "Targeted analysis cache invalidation did not remove the matching result.");
var runtimePerformanceEvidence = new ForgeEvidenceContribution(
    "runtime-performance-1",
    "example.analysis.alpha",
    "runtime:performance-regression",
    "Observed repeatable tick-time regression.",
    0.98,
    ForgeEvidenceConfidenceBand.Authoritative,
    new ForgeEvidenceProvenance(
        ForgeEvidenceSourceKind.RuntimeCompanion,
        "test.runtime",
        "1.0",
        analysisObservedAt,
        Attributes: new Dictionary<string, string> { ["severity"] = "Error" }),
    analysisObservedAt,
    analysisObservedAt,
    3,
    "example.analysis.beta");
var declaredIncompatibilityEvidence = new ForgeEvidenceContribution(
    "declared-incompatibility-1",
    "example.analysis.beta",
    "declared-incompatibility",
    "The installed metadata declares these mods incompatible.",
    1,
    ForgeEvidenceConfidenceBand.Authoritative,
    new ForgeEvidenceProvenance(ForgeEvidenceSourceKind.StaticAnalysis, "test.static", "1.0", analysisObservedAt),
    analysisObservedAt,
    analysisObservedAt,
    RelatedSubjectId: "example.analysis.alpha");
var healthyCompatibilityEvidence = new ForgeEvidenceContribution(
    "healthy-compatibility-1",
    "example.analysis.alpha",
    "compatibility-assessment",
    "Runtime compatibility is stable.",
    0.9,
    ForgeEvidenceConfidenceBand.High,
    new ForgeEvidenceProvenance(ForgeEvidenceSourceKind.CompatibilityIntelligence, "test.compatibility", "1.0", analysisObservedAt),
    analysisObservedAt,
    analysisObservedAt,
    RelatedSubjectId: "example.analysis.beta",
    Attributes: new Dictionary<string, string> { ["conflictScore"] = "0.2" });
var evidenceAnalysis = await analysisEngine.AnalyzeAsync(new ModAnalysisRequest(
    [analysisAlpha, analysisBeta],
    ["example.analysis.alpha", "example.analysis.beta"],
    "1.6",
    Evidence: [runtimePerformanceEvidence, declaredIncompatibilityEvidence, healthyCompatibilityEvidence]));
var reversedEvidenceAnalysis = await analysisEngine.AnalyzeAsync(new ModAnalysisRequest(
    [analysisBeta, analysisAlpha],
    ["example.analysis.alpha", "example.analysis.beta"],
    "1.6",
    Evidence: [healthyCompatibilityEvidence, declaredIncompatibilityEvidence, runtimePerformanceEvidence]));
Require(evidenceAnalysis.Metrics.InputFingerprint == reversedEvidenceAnalysis.Metrics.InputFingerprint,
    "Unified evidence fingerprint depended on contribution enumeration order.");
Require(evidenceAnalysis.Snapshot.Issues.Any(item => item.Code == AnalysisIssueCode.RuntimePerformanceRegression &&
                                                     item.Severity == AnalysisIssueSeverity.Error) &&
        evidenceAnalysis.Snapshot.Issues.Any(item => item.Code == AnalysisIssueCode.CompatibilityEvidenceConcern) &&
        !evidenceAnalysis.Snapshot.Issues.Any(item => item.SourceIdentity == "healthy-compatibility-1") &&
        evidenceAnalysis.Snapshot.Relationships.Count(item => item.Kind == AnalysisRelationshipKind.ObservedConflict) == 2,
    "Canonical analysis did not classify actionable evidence or suppress a healthy compatibility assessment.");
Require(evidenceAnalysis.Explainability.GetMod("example.analysis.alpha")?.Diagnostics
            .Any(item => item.Code == AnalysisIssueCode.RuntimePerformanceRegression.ToString()) == true,
    "Unified evidence finding did not flow into canonical per-mod explainability.");
var evidenceIssueViewer = new IssueEngine().Build(
    evidenceAnalysis.Snapshot,
    IssueScopeKind.FullLibrary,
    "Evidence fixture",
    [analysisAlpha, analysisBeta]);
Require(evidenceIssueViewer.Issues.Any(item => item.Category == "Runtime Performance") &&
        evidenceIssueViewer.Issues.Any(item => item.Category == "Compatibility Evidence"),
    "Issue Viewer projection did not consume canonical evidence-backed findings.");
using (var cancelledAnalysis = new CancellationTokenSource())
{
    cancelledAnalysis.Cancel();
    await RequireThrowsAsync<OperationCanceledException>(
        () => analysisEngine.AnalyzeAsync(new ModAnalysisRequest([analysisAlpha, analysisBeta]), cancellationToken: cancelledAnalysis.Token),
        "Analysis did not honor cancellation.");
}

var resilienceRoot = Path.Combine(Path.GetTempPath(), $"RimForge-resilience-fixture-{Guid.NewGuid():N}");
try
{
    Directory.CreateDirectory(resilienceRoot);
    await File.WriteAllTextAsync(Path.Combine(resilienceRoot, "Config.json"), "{}");
    await File.WriteAllTextAsync(Path.Combine(resilienceRoot, "Features.json"), "{}");
    var resiliencePaths = RimForgePathLayout.Create(resilienceRoot, Path.Combine(resilienceRoot, "State"));
    resiliencePaths.EnsureGeneratedDirectories();
    using var resilienceDiagnostics = new DiagnosticService(
        new JsonlLogSink(Path.Combine(resiliencePaths.DiagnosticsRoot, "resilience.jsonl")));
    var validation = await new PlatformValidationService(resiliencePaths, resilienceDiagnostics).ValidateAsync();
    Require(validation.IsHealthy && validation.Checks.All(check => check.Passed),
        "Platform self-validation did not accept a healthy workspace.");

    var recoveryRoot = Path.Combine(resilienceRoot, "Recovery");
    var interruptedRun = new ApplicationRecoveryService(recoveryRoot);
    var initialRun = await interruptedRun.BeginRunAsync("test-version");
    Require(initialRun.PreviousShutdownWasClean, "A new recovery root was reported as interrupted.");
    var resumedRecovery = new ApplicationRecoveryService(recoveryRoot);
    var resumedRun = await resumedRecovery.BeginRunAsync("test-version");
    Require(!resumedRun.PreviousShutdownWasClean && resumedRun.InterruptedRunId == initialRun.CurrentRunId,
        "Interrupted-run recovery did not identify the previous run.");
    await resumedRecovery.CompleteRunAsync();
    Require(!File.Exists(resumedRun.MarkerPath), "Clean shutdown did not remove the active-run marker.");

    var preservation = new StatePreservationService(resiliencePaths);
    var preserved = await preservation.CaptureAsync("test-version");
    Require(preserved.ProtectedRoots.Contains(resiliencePaths.CacheRoot, StringComparer.OrdinalIgnoreCase) &&
            preserved.CriticalFileSha256.ContainsKey("Config.json"),
        "State preservation did not capture protected roots and critical configuration.");
    RequireThrows<InvalidOperationException>(
        () => preservation.ValidateInstallBoundary(resiliencePaths.OutputRoot),
        "State preservation accepted an install root that overlaps protected state.");

    var packagePath = Path.Combine(resilienceRoot, "update.pkg");
    await File.WriteAllBytesAsync(packagePath, Encoding.UTF8.GetBytes("signed update payload"));
    var packageHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(packagePath)));
    var updateManifest = new RimForgeUpdateManifest(
        1, "2.2.0-test", "test", packageHash, DateTimeOffset.UtcNow, ["RimForge.dll"]);
    var manifestJson = JsonSerializer.Serialize(updateManifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    using var signingKey = RSA.Create(2048);
    var signature = Convert.ToBase64String(signingKey.SignData(
        Encoding.UTF8.GetBytes(manifestJson), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    var publicKey = signingKey.ExportSubjectPublicKeyInfoPem();
    var updater = new SignedUpdateService(
        Path.Combine(resilienceRoot, "Updates"),
        preservation,
        new Dictionary<string, string> { ["test"] = publicKey });
    Require(updater.VerifyManifest(manifestJson, signature, publicKey),
        "A valid signed update manifest was rejected.");
    Require(!updater.VerifyManifest(manifestJson + " ", signature, publicKey),
        "A modified update manifest passed signature verification.");
    var untrustedUpdater = new SignedUpdateService(Path.Combine(resilienceRoot, "UntrustedUpdates"), preservation);
    var untrustedStage = await untrustedUpdater.StageAsync(manifestJson, signature, packagePath);
    Require(!untrustedStage.Success && untrustedStage.Message.Contains("not trusted", StringComparison.OrdinalIgnoreCase),
        "An update from an unpinned channel was staged.");
    var staged = await updater.StageAsync(manifestJson, signature, packagePath);
    Require(staged.Success && File.Exists(staged.StagedPackagePath) && File.Exists(staged.TransactionPath),
        "A valid update was not staged transactionally.");
    var installRoot = Path.Combine(resilienceRoot, "Install");
    Directory.CreateDirectory(installRoot);
    await File.WriteAllTextAsync(Path.Combine(installRoot, "RimForge.dll"), "previous version");
    var rollback = await updater.CaptureRollbackAsync(updateManifest, installRoot);
    Require(rollback.Success && File.Exists(Path.Combine(rollback.RollbackRoot!, "RimForge.dll")),
        "Update rollback capture did not preserve the installed file.");
    await File.WriteAllTextAsync(Path.Combine(installRoot, "RimForge.dll"), "new version");
    var rollbackRestore = await updater.RestoreRollbackAsync(rollback.RollbackRoot!, installRoot);
    Require(rollbackRestore.Success && await File.ReadAllTextAsync(Path.Combine(installRoot, "RimForge.dll")) == "previous version",
        "Rollback did not atomically restore the previous installation file.");
}
finally
{
    if (Directory.Exists(resilienceRoot)) Directory.Delete(resilienceRoot, recursive: true);
}

var diagnosticsFixtureRoot = Path.Combine(Path.GetTempPath(), $"RimForge-diagnostics-fixture-{Guid.NewGuid():N}");
try
{
    var globalPath = Path.Combine(diagnosticsFixtureRoot, "diagnostics.jsonl");
    var sessionLog = new SessionLog(Path.Combine(diagnosticsFixtureRoot, "Sessions"));
    using (var diagnostics = new DiagnosticService(new JsonlLogSink(globalPath), sessionLog))
    {
        var eventCount = 0;
        diagnostics.EventWritten += (_, _) => eventCount++;
        sessionLog.BeginSession("diagnostic-session");
        diagnostics.Write(
            RimForgeLogLevel.Warning,
            "DiagnosticsTest",
            "Structured diagnostic event.",
            operationId: "test-operation",
            sessionId: "diagnostic-session",
            properties: new Dictionary<string, string> { ["fixture"] = "true" });
        using (diagnostics.Measure("DiagnosticsTest", "Measured operation", sessionId: "diagnostic-session"))
            await Task.Delay(10);
        diagnostics.ReportHealth(new RuntimeHealth(
            HealthStatus.Degraded,
            "DiagnosticsTest",
            "Fixture health degraded.",
            DateTimeOffset.UtcNow,
            "test-detail"));
        RimForgeLogger.Information("LegacyLogger", "Legacy logger bridge event.");
        Require(eventCount >= 4, "Diagnostic service did not publish structured, timing, health, and logger events.");
        Require(diagnostics.RecentEvents.Count >= 4, "Diagnostic service did not retain its bounded recent-event projection.");
        Require(diagnostics.CurrentHealth.Status == HealthStatus.Degraded, "Diagnostic health state was not retained.");
        sessionLog.EndSession("diagnostic-session");
    }
    Require(File.ReadAllLines(globalPath).Length >= 4, "Global JSONL diagnostic sink did not persist events.");
    var sessionPath = Path.Combine(diagnosticsFixtureRoot, "Sessions", "diagnostic-session", "session-log.jsonl");
    Require(File.Exists(sessionPath) && File.ReadAllLines(sessionPath).Length >= 2,
        "Session log did not persist session-correlated diagnostics and performance timing.");
}
finally
{
    if (Directory.Exists(diagnosticsFixtureRoot)) Directory.Delete(diagnosticsFixtureRoot, recursive: true);
}

var forgeSessionRoot = Path.Combine(Path.GetTempPath(), $"RimForge-session-fixture-{Guid.NewGuid():N}");
try
{
    var workspace = Path.Combine(forgeSessionRoot, "workspace");
    Directory.CreateDirectory(workspace);
    ForgeSessionId sessionId;
    using (var sessions = new ForgeSessionService(sessionsRoot: forgeSessionRoot))
    {
        var started = sessions.Start(new ForgeSessionRequest(
            workspace,
            "Engineering Profile",
            "1.6",
            42,
            "Starting test forge."));
        sessionId = started.Id;
        Require(!string.IsNullOrWhiteSpace(sessionId.Value), "Forge session identity was not generated.");
        Require(started.State == ForgeSessionState.Starting, "Forge session did not enter Starting state.");
        Require(started.Workspace == Path.GetFullPath(workspace), "Forge session workspace was not normalized.");
        Require(started.ProfileName == "Engineering Profile" && started.GameVersion == "1.6" && started.ModCount == 42,
            "Forge session metadata was not retained.");
        Require(File.Exists(Path.Combine(forgeSessionRoot, sessionId.Value, "session.json")),
            "Forge session record was not persisted by identity.");
        Require(File.Exists(Path.Combine(forgeSessionRoot, "current.json")),
            "Current Forge session recovery marker was not persisted.");

        await RequireThrowsAsync<InvalidOperationException>(
            () => Task.Run(() => sessions.Start(new ForgeSessionRequest(workspace, null, "1.6", 0, "Duplicate"))),
            "A concurrent Forge session was accepted.");

        sessions.Report(new ForgeProgress(ForgePhase.EvidenceScan, "Scanning", 0.5, 0.25, 1, 4));
        Require(sessions.CurrentSession?.State == ForgeSessionState.Running, "Forge progress did not enter Running state.");
        Require(sessions.Current.OverallProgress == 50 && sessions.Current.StageProgress == 25,
            "Forge progress was not projected to the compatibility snapshot.");
        sessions.SetRuntimeStatus(ForgeRuntimeStatus.Connected, "Runtime connected.");
        Require(sessions.Current.RuntimeStatus == ForgeRuntimeStatus.Connected, "Runtime status was not projected.");
        Require(sessions.RequestCancellation(), "Forge session cancellation request was rejected.");
        Require(sessions.CancellationToken.IsCancellationRequested, "Forge session cancellation token was not cancelled.");
        Require(sessions.Current.Status == ForgeSessionStatus.Cancelling, "Cancelling state was not published.");
        sessions.Cancel("Cancelled by test.");
        Require(sessions.CurrentSession?.State == ForgeSessionState.Cancelled && sessions.Current.CompletedUtc is not null,
            "Forge session did not reach a durable Cancelled state.");
    }

    using (var restored = new ForgeSessionService(sessionsRoot: forgeSessionRoot))
    {
        Require(restored.Current.SessionId == sessionId && restored.Current.Status == ForgeSessionStatus.Cancelled,
            "Completed Forge session was not restored from disk.");
        restored.Reset();
        var interrupted = restored.Start(new ForgeSessionRequest(workspace, null, "1.6", 7, "Interrupt me"));
        sessionId = interrupted.Id;
    }

    using (var recovered = new ForgeSessionService(sessionsRoot: forgeSessionRoot))
    {
        Require(recovered.Current.SessionId == sessionId && recovered.Current.Status == ForgeSessionStatus.Failed,
            "Interrupted Forge session was not recovered as failed.");
        Require(recovered.Current.Stage == "Interrupted", "Interrupted Forge session recovery stage was not recorded.");
    }
}
finally
{
    if (Directory.Exists(forgeSessionRoot)) Directory.Delete(forgeSessionRoot, recursive: true);
}

var platformFixtureRoot = Path.Combine(Path.GetTempPath(), $"RimForge-platform-fixture-{Guid.NewGuid():N}");
try
{
    var primarySteam = Path.Combine(platformFixtureRoot, "Steam");
    var secondaryLibrary = Path.Combine(platformFixtureRoot, "Games");
    var steamApps = Path.Combine(primarySteam, "steamapps");
    var secondarySteamApps = Path.Combine(secondaryLibrary, "steamapps");
    Directory.CreateDirectory(steamApps);
    Directory.CreateDirectory(secondarySteamApps);
    await File.WriteAllTextAsync(
        Path.Combine(steamApps, "libraryfolders.vdf"),
        $"\"libraryfolders\"\n{{\n  \"1\"\n  {{\n    \"path\" \"{secondaryLibrary.Replace("\\", "\\\\")}\"\n  }}\n}}");
    await File.WriteAllTextAsync(
        Path.Combine(secondarySteamApps, "appmanifest_294100.acf"),
        "\"AppState\"\n{\n  \"appid\" \"294100\"\n  \"installdir\" \"RimWorld Test\"\n}");
    var gameRoot = Path.Combine(secondarySteamApps, "common", "RimWorld Test");
    var workshopRoot = Path.Combine(secondarySteamApps, "workshop", "content", "294100");
    Directory.CreateDirectory(Path.Combine(gameRoot, "Mods"));
    Directory.CreateDirectory(Path.Combine(gameRoot, "Data"));
    Directory.CreateDirectory(workshopRoot);
    await File.WriteAllTextAsync(Path.Combine(gameRoot, "RimWorldWin64.exe"), string.Empty);

    var repository = Path.Combine(platformFixtureRoot, "Repository");
    var localAppData = Path.Combine(platformFixtureRoot, "Users", "Engineer", "AppData", "Local");
    Directory.CreateDirectory(repository);
    Directory.CreateDirectory(localAppData);
    var discoveredInstallations = new SteamLibraryDiscoveryService().FindRimWorldInstallations([primarySteam]);
    var fixtureInstallation = discoveredInstallations.SingleOrDefault(candidate =>
        candidate.LibraryRoot.Equals(Path.GetFullPath(secondaryLibrary), StringComparison.OrdinalIgnoreCase));
    Require(fixtureInstallation is not null, "Steam library discovery did not traverse libraryfolders.vdf.");

    var workspace = new WorkspaceService(RimForgePathLayout.Create(repository, Path.Combine(platformFixtureRoot, "RimForgeData")));
    var discovery = new PlatformDiscoveryService(new FixtureSteamLibraryService([fixtureInstallation!]), workspace, localAppData);
    var platform = discovery.Discover([primarySteam]);

    Require(platform.Installations.Count == 1, "Central platform discovery did not publish installation results.");
    Require(platform.PreferredInstallation?.LibraryRoot == Path.GetFullPath(secondaryLibrary),
        "Central platform discovery selected the wrong RimWorld installation.");
    Require(platform.PreferredInstallation?.GameExecutable == Path.Combine(gameRoot, "RimWorldWin64.exe"),
        "RimWorld installdir metadata was not honored.");
    Require(platform.WorkshopRoots.SequenceEqual([workshopRoot], StringComparer.OrdinalIgnoreCase),
        "Workshop roots were not projected from the installation snapshot.");
    Require(platform.UserPaths.ModsConfigPath == Path.Combine(
            platformFixtureRoot, "Users", "Engineer", "AppData", "LocalLow", "Ludeon Studios", "RimWorld by Ludeon Studios", "Config", "ModsConfig.xml"),
        "ModsConfig path was not resolved centrally from LocalAppData.");
    Require(platform.UserPaths.PlayerLogPath == Path.Combine(platform.UserPaths.UserDataRoot, "Player.log"),
        "Player.log path was not resolved from the canonical RimWorld user-data root.");
    Require(platform.Workspace == workspace.Paths, "The canonical workspace layout was not included in platform discovery.");
    Require(new ProfileWorkspaceService(discovery).GetRimWorldModsConfigPath() == platform.UserPaths.ModsConfigPath,
        "Profile activation does not consume centralized platform discovery.");
}
finally
{
    if (Directory.Exists(platformFixtureRoot)) Directory.Delete(platformFixtureRoot, recursive: true);
}

var companionFixtureRoot = Path.Combine(Path.GetTempPath(), $"RimForge-companion-fixture-{Guid.NewGuid():N}");
try
{
    var pipeName = $"RimForge.Companion.Test.{Guid.NewGuid():N}";
    var options = new CompanionHostOptions("forge-session-test", companionFixtureRoot, pipeName);
    await using var companion = new CompanionHost(options);
    using var companionCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var hostTask = companion.RunAsync(companionCancellation.Token);
    await WaitUntilAsync(() => companion.Health.IpcListening, TimeSpan.FromSeconds(5));
    Require(companion.Health.Status == CompanionHealthStatus.Healthy, "Companion Host did not become healthy.");

    await using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous))
    {
        await client.ConnectAsync(companionCancellation.Token);
        await using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
        await writer.WriteLineAsync("not-json");
        var envelope = RimForgeEnvelope.Create("rimforge.test", "agent-session-test", new { value = 42 });
        await writer.WriteLineAsync(ProtocolSerializer.Serialize(envelope));
        await WaitUntilAsync(() => companion.Health.EnvelopesReceived == 1, TimeSpan.FromSeconds(5));
    }

    Require(companion.Health.RejectedEnvelopes == 1, "Companion IPC did not reject a malformed envelope.");
    Require(File.Exists(companion.EvidencePath), "Companion SessionBridge did not create durable session evidence.");
    using (var evidenceStream = new FileStream(companion.EvidencePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    using (var evidenceReader = new StreamReader(evidenceStream))
    {
        var persisted = await evidenceReader.ReadToEndAsync();
        Require(persisted.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 1,
            "Companion SessionBridge did not persist exactly the accepted envelope.");
    }
    companion.RequestStop();
    await hostTask;
    Require(companion.Health.Status == CompanionHealthStatus.Stopped, "Companion Host did not stop cleanly.");
}
finally
{
    if (Directory.Exists(companionFixtureRoot)) Directory.Delete(companionFixtureRoot, recursive: true);
}

var graphFixtureRoot = Path.Combine(Path.GetTempPath(), $"RimForge-graph-fixture-{Guid.NewGuid():N}");
try
{
    var alphaRoot = await CreateModFixtureAsync(
        graphFixtureRoot,
        "Alpha",
        """
        <incompatibleWith><li>example.beta</li></incompatibleWith>
        <incompatibleWithByVersion><v1.6><li>example.gamma</li></v1.6></incompatibleWithByVersion>
        """);
    var betaRoot = await CreateModFixtureAsync(
        graphFixtureRoot,
        "Beta",
        """
        <modDependencies><li><packageId>example.alpha</packageId><displayName>Alpha</displayName></li></modDependencies>
        <incompatibleWith><li>example.alpha</li></incompatibleWith>
        """);
    var gammaRoot = await CreateModFixtureAsync(graphFixtureRoot, "Gamma", string.Empty);
    var parser = new AboutXmlParser();
    var alpha = await parser.ParseAsync(alphaRoot);
    var beta = await parser.ParseAsync(betaRoot);
    var gamma = await parser.ParseAsync(gammaRoot);

    Require(alpha.IncompatibleWith.Contains("example.beta", StringComparer.OrdinalIgnoreCase),
        "About.xml incompatibleWith metadata was not parsed.");
    Require(alpha.IncompatibleWith.Contains("example.gamma", StringComparer.OrdinalIgnoreCase),
        "About.xml versioned incompatibility metadata was not parsed.");

    var graphResult = new DependencyGraphService().Build([alpha, beta, gamma]);
    Require(graphResult.Graph.Edges.Any(edge =>
            edge.Relationship == DependencyRelationshipType.Required &&
            edge.SourceId.Equals("example.beta", StringComparison.OrdinalIgnoreCase) &&
            edge.TargetId.Equals("example.alpha", StringComparison.OrdinalIgnoreCase)),
        "A required relationship sharing the same node pair was lost.");
    Require(graphResult.Graph.Edges.Count(edge =>
            edge.Relationship == DependencyRelationshipType.Incompatible &&
            new[] { edge.SourceId, edge.TargetId }.Contains("example.alpha", StringComparer.OrdinalIgnoreCase) &&
            new[] { edge.SourceId, edge.TargetId }.Contains("example.beta", StringComparer.OrdinalIgnoreCase)) == 1,
        "Symmetric incompatibleWith declarations were not consolidated.");
    Require(graphResult.Graph.Edges.Any(edge =>
            edge.Relationship == DependencyRelationshipType.Incompatible &&
            edge.DeclarationCount == 2),
        "Incompatibility declaration evidence was not retained.");
}
finally
{
    if (Directory.Exists(graphFixtureRoot)) Directory.Delete(graphFixtureRoot, recursive: true);
}


var evidenceRepositoryRoot = Path.Combine(Path.GetTempPath(), $"RimForge-evidence-platform-{Guid.NewGuid():N}");
try
{
    Directory.CreateDirectory(evidenceRepositoryRoot);
    await using var evidenceService = new ForgeEvidenceService();
    var observedAt = DateTimeOffset.UtcNow;
    var provenance = new ForgeEvidenceProvenance(
        ForgeEvidenceSourceKind.RuntimeCompanion,
        "runtime-test",
        "1.0",
        observedAt);
    var first = new ForgeEvidenceContribution(
        "runtime-1",
        "example.alpha",
        "runtime-interaction",
        "Alpha interacted with Beta.",
        0.8,
        ForgeEvidenceConfidenceBand.High,
        provenance,
        observedAt,
        observedAt,
        1,
        "example.beta");
    var second = first with
    {
        EvidenceId = "runtime-2",
        Confidence = 1.0,
        LastObservedAtUtc = observedAt.AddMinutes(1),
        ObservationCount = 2
    };

    var ingestion = await evidenceService.IngestAsync(
        new ForgeEvidenceIngestionBatch(
            "batch-1",
            ForgeEvidenceSchema.CurrentVersion,
            ForgeEvidenceSourceKind.RuntimeCompanion,
            observedAt,
            [first, second]),
        evidenceRepositoryRoot);

    Require(ingestion.Accepted == 2 && ingestion.Merged == 1 && ingestion.Rejected == 0,
        "Forge Evidence ingestion did not deterministically merge duplicate observations.");
    Require(evidenceService.Current.Contributions.Count == 1,
        "Forge Evidence retained duplicate contribution identities.");
    Require(evidenceService.Current.Contributions[0].ObservationCount == 3,
        "Forge Evidence observation counts were not consolidated.");
    Require(evidenceService.Current.Contributions[0].Confidence > 0.9,
        "Forge Evidence confidence was not weighted during consolidation.");

    await using var restoredService = new ForgeEvidenceService();
    var restored = await restoredService.RestoreAsync(evidenceRepositoryRoot);
    Require(restored is not null && restored.SchemaVersion == ForgeEvidenceSchema.CurrentVersion,
        "The versioned Forge Evidence snapshot could not be restored.");
    Require(restored!.Contributions.Count == 1 && restored.Contributions[0].ObservationCount == 3,
        "Persisted Forge Evidence lost consolidated observations.");

    var durableRoot = Path.Combine(evidenceRepositoryRoot, "durable-store");
    var durableStore = new ForgeEvidenceStore(durableRoot);
    await durableStore.SaveAsync(restored, CancellationToken.None);
    await durableStore.SaveAsync(restored, CancellationToken.None);
    var durablePath = Path.Combine(durableRoot, "snapshot.json");
    await File.WriteAllTextAsync(durablePath, "{corrupt", CancellationToken.None);
    var recovered = await durableStore.LoadAsync(CancellationToken.None);
    Require(recovered.Status == ForgeEvidenceStoreLoadStatus.RecoveredFromBackup && recovered.Snapshot is not null,
        "The durable Forge Evidence store did not recover its last-known-good snapshot.");

    var rejected = await restoredService.IngestAsync(
        new ForgeEvidenceIngestionBatch(
            "batch-invalid",
            ForgeEvidenceSchema.CurrentVersion + 1,
            ForgeEvidenceSourceKind.RuntimeCompanion,
            observedAt,
            [first]),
        evidenceRepositoryRoot);
    Require(rejected.Rejected == 1 && rejected.ValidationErrors.Count > 0,
        "Unsupported Forge Evidence schema versions were not rejected.");
}
finally
{
    if (Directory.Exists(evidenceRepositoryRoot)) Directory.Delete(evidenceRepositoryRoot, recursive: true);
}


var pipelineObservedAt = DateTimeOffset.UtcNow;
var pipelineMod = new ModRecord
{
    Id = "example.pipeline",
    RootPath = Path.Combine(Path.GetTempPath(), "RimForge-Pipeline-Mod"),
    FolderName = "Pipeline",
    AboutPath = Path.Combine(Path.GetTempPath(), "RimForge-Pipeline-Mod", "About", "About.xml"),
    Name = "Pipeline Fixture",
    PackageId = "example.pipeline",
    LastModified = pipelineObservedAt,
    Dependencies =
    [
        new ModDependency("example.required", "Required Fixture", null, null, "About.xml")
    ],
    LoadAfter = ["example.after"],
    LoadBefore = ["example.before"],
    IncompatibleWith = ["example.conflict"],
    Evidence = new ModEvidence
    {
        TotalFiles = 5,
        TotalBytes = 1024,
        Badges =
        [
            new ModEvidenceBadge(
                ModEvidenceKind.CSharp,
                "C#",
                1,
                "Contains compiled code.",
                "One assembly was discovered.")
        ]
    }
};
var pipeline = new ForgeEvidencePipeline(
[
    new DependencyMetadataEvidenceProducer(),
    new StaticModMetadataEvidenceProducer()
]);
var pipelineResult = await pipeline.CollectAsync(
    new ForgeEvidenceCollectionContext(
        [pipelineMod],
        "1.6",
        new ForgeEvidenceSnapshotDescriptor(
            0,
            ForgeEvidenceSchema.CurrentVersion,
            ForgeEvidenceSchema.PlatformVersion,
            string.Empty,
            DateTimeOffset.MinValue,
            0,
            0),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        false,
        pipelineObservedAt));

Require(pipelineResult.ProducersCompleted == 2 && pipelineResult.ProducersFailed == 0,
    "The Forge Evidence pipeline did not execute all registered producers.");
Require(pipelineResult.Contributions.Any(item =>
        item.EvidenceType == "mod-inventory" &&
        item.Provenance.SourceKind == ForgeEvidenceSourceKind.StaticAnalysis),
    "Static metadata was not projected into Forge Evidence.");
Require(pipelineResult.Contributions.Any(item =>
        item.EvidenceType == "required-dependency" &&
        item.RelatedSubjectId == "example.required"),
    "Dependency metadata was not projected into Forge Evidence.");
Require(pipelineResult.Contributions.All(item => item.EvidenceId.Length == 64),
    "Forge Evidence did not assign deterministic SHA-256 evidence identifiers.");

var repeatedPipelineResult = await pipeline.CollectAsync(
    new ForgeEvidenceCollectionContext(
        [pipelineMod],
        "1.6",
        new ForgeEvidenceSnapshotDescriptor(
            0,
            ForgeEvidenceSchema.CurrentVersion,
            ForgeEvidenceSchema.PlatformVersion,
            string.Empty,
            DateTimeOffset.MinValue,
            0,
            0),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        false,
        pipelineObservedAt));
Require(
    pipelineResult.Contributions.Select(item => item.EvidenceId)
        .SequenceEqual(repeatedPipelineResult.Contributions.Select(item => item.EvidenceId), StringComparer.Ordinal),
    "Equivalent producer runs produced different evidence identifiers.");

var pipelineIndex = new ForgeEvidenceIndex(pipelineResult.Contributions);
Require(pipelineIndex.ForSubject("EXAMPLE.PIPELINE").Count == pipelineResult.Contributions.Count,
    "The Forge Evidence subject index is not case-insensitive.");
Require(pipelineIndex.Between("example.pipeline", "example.required").Count == 1,
    "The Forge Evidence relationship index did not return the required dependency.");
Require(pipelineIndex.FromSource(ForgeEvidenceSourceKind.DependencyAnalysis).Count >= 4,
    "The Forge Evidence source index omitted dependency-analysis contributions.");

var incrementalRepositoryRoot = Path.Combine(Path.GetTempPath(), $"RimForge-incremental-evidence-{Guid.NewGuid():N}");
try
{
    Directory.CreateDirectory(incrementalRepositoryRoot);
    var projectionProducer = new MutableProjectionProducer();
    var incrementalBus = new ForgeEvidenceBus();
    await using var incrementalService = new ForgeEvidenceService(incrementalBus, [projectionProducer]);
    var firstProjection = await incrementalService.RefreshAsync(
        Array.Empty<ModRecord>(),
        incrementalRepositoryRoot,
        "1.6");
    Require(firstProjection.Contributions.Count == 1,
        "The initial producer projection was not published.");

    projectionProducer.EmitEvidence = false;
    var secondProjection = await incrementalService.RefreshAsync(
        Array.Empty<ModRecord>(),
        incrementalRepositoryRoot,
        "1.6",
        forceRescan: true);
    Require(secondProjection.Contributions.Count == 0,
        "A completed empty producer projection did not remove stale evidence.");
    Require(secondProjection.Metrics.ReconciledContributions == 1,
        "Reconciled contribution metrics did not report stale evidence removal.");

    await using var scheduler = new ForgeEvidenceRefreshScheduler(
        incrementalService,
        new ForgeEvidenceRefreshSchedulerOptions
        {
            InvalidationSettleDelay = TimeSpan.FromMilliseconds(10)
        });
    scheduler.Configure(new ForgeEvidenceRefreshRequest(
        Array.Empty<ModRecord>(),
        incrementalRepositoryRoot,
        "1.6"));
    var scheduledPublication = new TaskCompletionSource<ForgeEvidencePublication>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    incrementalBus.Published += (_, publication) => scheduledPublication.TrySetResult(publication);
    scheduler.Start();
    incrementalService.Invalidate(incrementalRepositoryRoot, ForgeEvidenceInvalidationReason.Manual);
    var automaticPublication = await scheduledPublication.Task.WaitAsync(TimeSpan.FromSeconds(5));
    var automaticSnapshot = automaticPublication.Snapshot;
    Require(automaticPublication.Reason == ForgeEvidencePublicationReason.Refreshed &&
            ReferenceEquals(incrementalBus.Current, automaticSnapshot),
        "The evidence bus did not publish the authoritative refreshed generation.");
    Require(automaticSnapshot.Metrics.PendingInvalidations == 1,
        "The automatic refresh did not capture the pending invalidation.");
}
finally
{
    if (Directory.Exists(incrementalRepositoryRoot))
        Directory.Delete(incrementalRepositoryRoot, recursive: true);
}

Console.WriteLine("RimForge.ExecutionTests: PASSED");
return;

static async Task<string> CreateModFixtureAsync(string root, string name, string relationships)
{
    var modRoot = Path.Combine(root, name);
    var aboutRoot = Path.Combine(modRoot, "About");
    Directory.CreateDirectory(aboutRoot);
    var packageId = $"example.{name.ToLowerInvariant()}";
    var xml = $"""
        <ModMetaData>
          <name>{name}</name>
          <packageId>{packageId}</packageId>
          <supportedVersions><li>1.6</li></supportedVersions>
          {relationships}
        </ModMetaData>
        """;
    await File.WriteAllTextAsync(Path.Combine(aboutRoot, "About.xml"), xml);
    return modRoot;
}

bool SawState(BackgroundTaskState state)
{
    lock (snapshotGate) return snapshots.Any(snapshot => snapshot.State == state);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task RequireThrowsAsync<TException>(Func<Task> operation, string message)
    where TException : Exception
{
    try
    {
        await operation();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void RequireThrows<TException>(Action operation, string message)
    where TException : Exception
{
    try { operation(); }
    catch (TException) { return; }
    throw new InvalidOperationException(message);
}

static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
{
    var started = DateTimeOffset.UtcNow;
    while (!condition())
    {
        if (DateTimeOffset.UtcNow - started > timeout) throw new TimeoutException("Timed out waiting for test condition.");
        await Task.Delay(25);
    }
}


file sealed class MutableProjectionProducer : IForgeEvidenceProducer
{
    public bool EmitEvidence { get; set; } = true;
    public string ProducerId => "test.mutable-projection";
    public ForgeEvidenceSourceKind SourceKind => ForgeEvidenceSourceKind.StaticAnalysis;
    public int Order => 1;

    public Task<ForgeEvidenceProducerResult> CollectAsync(
        ForgeEvidenceCollectionContext context,
        IProgress<ForgeEvidenceProducerProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ForgeEvidenceContribution> contributions = EmitEvidence
            ? [new ForgeEvidenceContribution(
                "mutable-projection",
                "example.removed",
                "test-projection",
                "Mutable projection evidence.",
                1,
                ForgeEvidenceConfidenceBand.Authoritative,
                new ForgeEvidenceProvenance(
                    SourceKind,
                    ProducerId,
                    "1.0",
                    context.StartedAtUtc),
                context.StartedAtUtc,
                context.StartedAtUtc)]
            : Array.Empty<ForgeEvidenceContribution>();
        return Task.FromResult(new ForgeEvidenceProducerResult(
            ProducerId,
            SourceKind,
            contributions,
            Array.Empty<ForgeEvidenceProducerDiagnostic>(),
            TimeSpan.Zero));
    }
}

file sealed class FixtureSteamLibraryService(IReadOnlyList<SteamInstallationCandidate> installations) : ISteamLibraryService
{
    public IReadOnlyList<SteamInstallationCandidate> FindRimWorldInstallations(
        IEnumerable<string>? additionalRoots = null) => installations;
}

file sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

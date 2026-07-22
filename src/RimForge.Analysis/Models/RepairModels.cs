namespace RimForge.Analysis.Models;

public enum RepairExecutionMode
{
    Automatic,
    Assisted,
    ManualChoice
}

public enum RepairPlanStatus
{
    PreviewOnly,
    AwaitingUserChoice,
    Ready,
    BlockedByPreconditions,
    Unsupported
}

public enum RepairConfidence
{
    Low,
    Medium,
    High
}

public enum RepairSafetyClass
{
    SafeAutomatic,
    ConfirmationRequired,
    Destructive,
    Unsupported
}

public enum RepairPreconditionKind
{
    ActiveProfile,
    ProfileUnlocked,
    WorkspaceAvailable,
    ConfigurationDirectoryAvailable,
    AffectedModAvailable
}

public sealed record RepairEvidenceReference(
    string Id,
    string Source,
    string Summary,
    string? PackageId = null);

public sealed record RepairPrecondition(
    RepairPreconditionKind Kind,
    string Target,
    bool IsSatisfied,
    string FailureMessage);

public sealed record RepairPreview(
    string ChangeSummary,
    IReadOnlyList<string> AffectedPackageIds,
    IReadOnlyList<string> AffectedPaths,
    bool PerformsWrites = false);

public sealed record RepairPlanningContext(
    string? ProfileName,
    string? WorkspacePath,
    string? ModsConfigPath,
    bool HasActiveProfile,
    bool IsProfileLocked,
    bool WorkspaceExists,
    bool ModsConfigDirectoryExists);

public sealed record RepairCertification(
    string PolicyId,
    bool AutomaticExecutionAllowlisted,
    bool RequiresExplicitConfirmation,
    bool RuntimeEvidenceAdvisoryOnly,
    string Reason)
{
    public static RepairCertification RestrictiveDefault { get; } = new(
        "repair-policy:uncertified", false, true, false,
        "The repair plan has not passed safety certification.");
}

public sealed record RepairPlanStep(
    int Order,
    string Action,
    string TargetName,
    string? TargetPath,
    string TechnicalDetail,
    bool IsDestructive,
    bool RequiresConfirmation);

public sealed record RepairPlan(
    string Id,
    string IssueId,
    string Title,
    string Summary,
    RepairExecutionMode ExecutionMode,
    RepairPlanStatus Status,
    IReadOnlyList<RepairPlanStep> Steps,
    IReadOnlyList<string> ChoicePackageIds,
    string ExpectedResult,
    bool RequiresConfirmation,
    bool IsDestructive)
{
    public RepairConfidence Confidence { get; init; } = RepairConfidence.Low;
    public RepairSafetyClass SafetyClass { get; init; } = RepairSafetyClass.Unsupported;
    public IReadOnlyList<RepairEvidenceReference> Evidence { get; init; } = Array.Empty<RepairEvidenceReference>();
    public IReadOnlyList<RepairPrecondition> Preconditions { get; init; } = Array.Empty<RepairPrecondition>();
    public RepairPreview Preview { get; init; } = new("No changes are available.", Array.Empty<string>(), Array.Empty<string>());
    public RepairCertification Certification { get; init; } = RepairCertification.RestrictiveDefault;
    public string DeterministicKey { get; init; } = string.Empty;
    public bool PreconditionsSatisfied => Preconditions.All(item => item.IsSatisfied);
    public bool CanExecute => Status == RepairPlanStatus.Ready && PreconditionsSatisfied && SafetyClass != RepairSafetyClass.Unsupported;
}

public sealed record RepairHistoryEntry(
    string Id,
    DateTimeOffset Started,
    DateTimeOffset? Completed,
    string IssueId,
    string RepairTitle,
    string Result,
    IReadOnlyList<string> Actions,
    string? TechnicalDetail);

public enum RepairTransactionState
{
    Planned,
    Executing,
    Committed,
    RollingBack,
    RolledBack,
    Cancelled,
    Failed,
    RecoveryRequired
}

public sealed record RepairAuditEvent(
    DateTimeOffset Timestamp,
    RepairTransactionState State,
    string Message,
    string? TechnicalDetail = null);

public sealed record RepairMutationResult(
    bool Success,
    string Message,
    string? BackupPath = null,
    string? TechnicalDetail = null);

public sealed record RepairTransactionJournal(
    string Id,
    string PlanId,
    string IssueId,
    string DeterministicPlanKey,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    RepairTransactionState State,
    IReadOnlyList<RepairAuditEvent> AuditTrail,
    string? CertificationPolicyId = null,
    RepairSafetyClass SafetyClass = RepairSafetyClass.Unsupported,
    string? BackupPath = null,
    string? Outcome = null)
{
    public bool IsTerminal => State is RepairTransactionState.Committed or RepairTransactionState.RolledBack or
        RepairTransactionState.Cancelled or RepairTransactionState.Failed;
}

public sealed record RepairExecutionResult(
    bool Success,
    RepairTransactionState State,
    string Message,
    RepairTransactionJournal Journal);

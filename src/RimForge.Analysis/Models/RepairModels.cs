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

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
    Unsupported
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
    bool IsDestructive);

public sealed record RepairHistoryEntry(
    string Id,
    DateTimeOffset Started,
    DateTimeOffset? Completed,
    string IssueId,
    string RepairTitle,
    string Result,
    IReadOnlyList<string> Actions,
    string? TechnicalDetail);

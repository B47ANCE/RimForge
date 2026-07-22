using RimForge.Analysis.Models;

namespace RimForge.Analysis.Services;

public interface IRepairSafetyPolicy
{
    RepairPlan Certify(RepairPlan plan, IssueWorkItem issue);
}

public sealed class RepairSafetyPolicy : IRepairSafetyPolicy
{
    public const string PolicyId = "rimforge.repair-safety.v1";
    private static readonly IReadOnlySet<(AnalysisIssueCode Code, RepairActionKind Action)> AutomaticAllowlist =
        new HashSet<(AnalysisIssueCode, RepairActionKind)>
        {
            (AnalysisIssueCode.LoadOrderViolation, RepairActionKind.ReorderProfile)
        };
    private static readonly IReadOnlySet<AnalysisIssueCode> RuntimeEvidenceCodes = new HashSet<AnalysisIssueCode>
    {
        AnalysisIssueCode.RuntimeObservedConflict,
        AnalysisIssueCode.RuntimePerformanceRegression,
        AnalysisIssueCode.RuntimeIntegrationFailure
    };

    public RepairPlan Certify(RepairPlan plan, IssueWorkItem issue)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(issue);
        var runtimeEvidence = RuntimeEvidenceCodes.Contains(issue.Code);
        var allowlisted = AutomaticAllowlist.Contains((issue.Code, issue.RepairAction)) &&
            plan.ExecutionMode == RepairExecutionMode.Automatic &&
            plan.Confidence == RepairConfidence.High &&
            !plan.IsDestructive &&
            !runtimeEvidence;
        var requiresConfirmation = plan.IsDestructive || !allowlisted || plan.RequiresConfirmation;
        var reason = runtimeEvidence
            ? "Companion runtime evidence is advisory and can never authorize an automatic mutation."
            : allowlisted
                ? "Deterministic dependency-safe profile reordering is explicitly allowlisted."
                : plan.IsDestructive
                    ? "Destructive repair plans always require explicit user confirmation."
                    : "This action is uncertain or assisted and is not on the automatic allowlist.";
        var safety = plan.IsDestructive
            ? RepairSafetyClass.Destructive
            : allowlisted ? RepairSafetyClass.SafeAutomatic
            : plan.Status == RepairPlanStatus.Unsupported ? RepairSafetyClass.Unsupported
            : RepairSafetyClass.ConfirmationRequired;
        return plan with
        {
            SafetyClass = safety,
            Certification = new RepairCertification(PolicyId, allowlisted, requiresConfirmation, runtimeEvidence, reason)
        };
    }
}

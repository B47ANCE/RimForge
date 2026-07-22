using RimForge.Analysis.Models;
using RimForge.Core.Models;

namespace RimForge.Analysis.Services;

public sealed class RepairPlanner
{
    private readonly IRepairSafetyPolicy _safetyPolicy;

    public RepairPlanner(IRepairSafetyPolicy? safetyPolicy = null) =>
        _safetyPolicy = safetyPolicy ?? new RepairSafetyPolicy();

    public RepairPlan Build(
        IssueWorkItem issue,
        IReadOnlyCollection<ModRecord> mods,
        string? selectedCycleFirstPackageId = null,
        RepairPlanningContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(mods);

        var names = mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.PackageId))
            .GroupBy(mod => mod.PackageId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().DisplayName, StringComparer.OrdinalIgnoreCase);

        string NameOf(string? packageId) =>
            !string.IsNullOrWhiteSpace(packageId) && names.TryGetValue(packageId, out var name)
                ? name
                : packageId ?? "Unknown mod";

        var plan = issue.RepairAction switch
        {
            RepairActionKind.InstallDependency => BuildDependencyPlan(issue, NameOf),
            RepairActionKind.ActivateDependency => BuildActivationPlan(issue, NameOf),
            RepairActionKind.ReorderProfile => BuildLoadOrderPlan(issue, NameOf),
            RepairActionKind.DisableDuplicate => BuildDuplicatePlan(issue, NameOf),
            RepairActionKind.ReviewCycle => BuildCyclePlan(issue, NameOf, selectedCycleFirstPackageId),
            _ => BuildManualPlan(issue, NameOf),
        };
        return _safetyPolicy.Certify(Enrich(plan, issue, mods, context), issue);
    }

    public static RepairPlanningContext CaptureContext(RimForgeProfile? profile)
    {
        var configDirectory = string.IsNullOrWhiteSpace(profile?.ModsConfigPath)
            ? null
            : Path.GetDirectoryName(profile.ModsConfigPath);
        return new RepairPlanningContext(
            profile?.Name,
            profile?.WorkspacePath,
            profile?.ModsConfigPath,
            profile is not null,
            profile?.IsLocked == true,
            !string.IsNullOrWhiteSpace(profile?.WorkspacePath) && Directory.Exists(profile.WorkspacePath),
            !string.IsNullOrWhiteSpace(configDirectory) && Directory.Exists(configDirectory));
    }

    private static RepairPlan Enrich(
        RepairPlan plan,
        IssueWorkItem issue,
        IReadOnlyCollection<ModRecord> mods,
        RepairPlanningContext? context)
    {
        var affectedIds = issue.RelatedPackageIds
            .Append(issue.PackageId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var installedIds = mods
            .Select(mod => mod.PackageId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mutatesProfile = issue.RepairAction is RepairActionKind.ReorderProfile or RepairActionKind.ActivateDependency or RepairActionKind.ReviewCycle;
        var preconditions = new List<RepairPrecondition>
        {
            new(RepairPreconditionKind.AffectedModAvailable, issue.PackageId,
                installedIds.Contains(issue.PackageId), $"Affected mod '{issue.PackageId}' is no longer installed.")
        };
        if (mutatesProfile)
        {
            preconditions.Add(new(RepairPreconditionKind.ActiveProfile, context?.ProfileName ?? "Active profile",
                context?.HasActiveProfile == true, "Select an active profile before applying this repair."));
            preconditions.Add(new(RepairPreconditionKind.ProfileUnlocked, context?.ProfileName ?? "Active profile",
                context?.HasActiveProfile == true && context.IsProfileLocked == false, "Unlock the active profile before applying this repair."));
            preconditions.Add(new(RepairPreconditionKind.WorkspaceAvailable, context?.WorkspacePath ?? "Profile workspace",
                context?.WorkspaceExists == true, "The profile workspace is unavailable."));
            preconditions.Add(new(RepairPreconditionKind.ConfigurationDirectoryAvailable, context?.ModsConfigPath ?? "ModsConfig directory",
                context?.ModsConfigDirectoryExists == true, "The RimWorld configuration directory is unavailable."));
        }

        var safety = plan.IsDestructive
            ? RepairSafetyClass.Destructive
            : plan.ExecutionMode == RepairExecutionMode.Automatic && !plan.RequiresConfirmation
                ? RepairSafetyClass.SafeAutomatic
                : plan.Status == RepairPlanStatus.Unsupported
                    ? RepairSafetyClass.Unsupported
                    : RepairSafetyClass.ConfirmationRequired;
        var confidence = issue.RepairAction == RepairActionKind.ReorderProfile
            ? RepairConfidence.High
            : issue.RelatedPackageIds.Count > 0 ? RepairConfidence.Medium : RepairConfidence.Low;
        var status = plan.Status == RepairPlanStatus.Ready && preconditions.Any(item => !item.IsSatisfied)
            ? RepairPlanStatus.BlockedByPreconditions
            : plan.Status;
        var paths = new[] { context?.WorkspacePath, context?.ModsConfigPath }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var key = string.Join("|", new[] { issue.Id, issue.RepairAction.ToString(), context?.ProfileName ?? "no-profile" }
            .Concat(affectedIds).Select(value => value.Trim().ToLowerInvariant()));

        return plan with
        {
            Status = status,
            Confidence = confidence,
            SafetyClass = safety,
            Evidence =
            [
                new($"issue:{issue.Id}", "CanonicalAnalysis", issue.Explanation, issue.PackageId),
                new($"recommendation:{issue.Code}", "RepairRecommendation", issue.RecommendedAction, issue.PackageId)
            ],
            Preconditions = preconditions,
            Preview = new RepairPreview(plan.Summary, affectedIds, paths, PerformsWrites: false),
            DeterministicKey = key
        };
    }

    private static RepairPlan BuildDependencyPlan(IssueWorkItem issue, Func<string?, string> nameOf)
    {
        var missingId = issue.RelatedPackageIds.FirstOrDefault();
        var missingName = nameOf(missingId);
        return new RepairPlan(
            $"repair:{issue.Id}", issue.Id, $"Install {missingName}",
            $"Subscribe to or enable {missingName}, wait for Steam to finish, refresh the library, and revalidate the profile.",
            RepairExecutionMode.Assisted, RepairPlanStatus.PreviewOnly,
            [
                new(1, "Locate dependency", missingName, null, $"Resolve Workshop metadata for {missingName}.", false, false),
                new(2, "Subscribe", missingName, null, $"Request a Steam Workshop subscription for {missingName}.", false, true),
                new(3, "Wait for download", missingName, null, "Monitor Steam until the Workshop content is installed.", false, false),
                new(4, "Refresh library", missingName, null, "Rescan the installed mod library and update the active profile.", false, false),
                new(5, "Revalidate", nameOf(issue.PackageId), null, "Re-run dependency validation for the affected mod.", false, false),
            ],
            Array.Empty<string>(), "The missing dependency is installed and the profile is revalidated.", true, false);
    }

    private static RepairPlan BuildActivationPlan(IssueWorkItem issue, Func<string?, string> nameOf)
    {
        var dependencyId = issue.RelatedPackageIds.FirstOrDefault();
        var dependencyName = nameOf(dependencyId);
        return new RepairPlan(
            $"repair:{issue.Id}", issue.Id, $"Enable {dependencyName}",
            $"Add the installed dependency {dependencyName} to the active profile and recalculate its dependency-safe position.",
            RepairExecutionMode.Assisted, RepairPlanStatus.PreviewOnly,
            [
                new(1, "Preview activation", dependencyName, null, "Calculate the dependency closure and proposed insertion position.", false, false),
                new(2, "Update working profile", dependencyName, null, "Enable the dependency without silently persisting unrelated changes.", false, true),
                new(3, "Recalculate order", nameOf(issue.PackageId), null, "Rebuild the tri-hybrid load-order preview.", false, false),
                new(4, "Revalidate", nameOf(issue.PackageId), null, "Verify that the required dependency is active and ordered before its dependent.", false, false),
            ],
            dependencyId is null ? Array.Empty<string>() : new[] { dependencyId },
            "The installed dependency is active and ordered before the affected mod.", true, false);
    }

    private static RepairPlan BuildLoadOrderPlan(IssueWorkItem issue, Func<string?, string> nameOf) =>
        new($"repair:{issue.Id}", issue.Id, "Apply recommended load order",
            $"Preview and apply a deterministic order for {nameOf(issue.PackageId)} and related mods.",
            RepairExecutionMode.Automatic, RepairPlanStatus.Ready,
            [
                new(1, "Preview order", nameOf(issue.PackageId), null, "Calculate the dependency-safe profile order.", false, false),
                new(2, "Write profile", nameOf(issue.PackageId), null, "Write the approved order to the active RimForge profile.", false, true),
                new(3, "Revalidate", nameOf(issue.PackageId), null, "Re-run load-order validation.", false, false),
            ],
            Array.Empty<string>(), "The profile uses the recommended dependency-safe order.", true, false);

    private static RepairPlan BuildDuplicatePlan(IssueWorkItem issue, Func<string?, string> nameOf) =>
        new($"repair:{issue.Id}", issue.Id, "Resolve duplicate installation",
            $"Choose the authoritative installation of {nameOf(issue.PackageId)} and disable the duplicate source.",
            RepairExecutionMode.Assisted, RepairPlanStatus.PreviewOnly,
            [
                new(1, "Compare installations", nameOf(issue.PackageId), null, "Compare source, path, Workshop ID, and modification time.", false, false),
                new(2, "Choose authoritative copy", nameOf(issue.PackageId), null, "Require the user to approve which installation remains active.", false, true),
                new(3, "Refresh and revalidate", nameOf(issue.PackageId), null, "Refresh the library and verify package resolution.", false, false),
            ],
            issue.RelatedPackageIds, "Only one authoritative installation remains active.", true, false);

    private static RepairPlan BuildCyclePlan(
        IssueWorkItem issue,
        Func<string?, string> nameOf,
        string? selectedFirstPackageId)
    {
        var members = issue.RelatedPackageIds
            .Append(issue.PackageId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (string.IsNullOrWhiteSpace(selectedFirstPackageId) ||
            !members.Contains(selectedFirstPackageId, StringComparer.OrdinalIgnoreCase))
        {
            return new RepairPlan(
                $"repair:{issue.Id}", issue.Id, "Resolve dependency cycle",
                "Choose which mod should load first. RimForge will use that choice to create a deterministic order and validate the result.",
                RepairExecutionMode.ManualChoice, RepairPlanStatus.AwaitingUserChoice,
                [new(1, "Choose first mod", string.Join(" / ", members.Select(nameOf)), null,
                    "A dependency cycle requires the user to select the first mod in the cycle.", false, true)],
                members, "The selected mod becomes the first item in the cycle and the resulting order is revalidated.", true, false);
        }

        var selectedName = nameOf(selectedFirstPackageId);
        return new RepairPlan(
            $"repair:{issue.Id}:{selectedFirstPackageId}", issue.Id, "Resolve dependency cycle",
            $"Place {selectedName} first, order the remaining cycle members deterministically, then revalidate.",
            RepairExecutionMode.ManualChoice, RepairPlanStatus.Ready,
            [
                new(1, "Set cycle anchor", selectedName, null, $"Use {selectedName} as the first mod in the cycle.", false, true),
                new(2, "Reorder cycle members", string.Join(" / ", members.Select(nameOf)), null, "Apply a deterministic order beginning with the selected anchor.", false, true),
                new(3, "Revalidate", selectedName, null, "Re-run dependency and load-order validation.", false, false),
            ],
            members, "The cycle has an explicit user-approved first mod and a deterministic load order.", true, false);
    }

    private static RepairPlan BuildManualPlan(IssueWorkItem issue, Func<string?, string> nameOf) =>
        new($"repair:{issue.Id}", issue.Id, issue.Title,
            $"Inspect {nameOf(issue.PackageId)} and follow the recommended action.",
            RepairExecutionMode.ManualChoice, RepairPlanStatus.Unsupported,
            [new(1, "Inspect issue", nameOf(issue.PackageId), null, issue.RecommendedAction, false, false)],
            Array.Empty<string>(), "The issue is reviewed manually.", false, false);
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RimForge.UI.Controls;

namespace RimForge.UI.Dialogs;

public static class ForgeDialogService
{
    public static string? ShowPrompt(Window owner, string title, string initialValue, string? message = null, string primaryText = "Save")
    {
        primaryText = ResolveSemanticAction(title, primaryText);
        var dialog = new ForgeDialogWindow
        {
            Owner = owner,
            TitleText = title,
            Heading = title,
            Message = message ?? string.Empty,
            InputText = initialValue,
            IsInputVisible = true,
            PrimaryText = primaryText,
            DialogIconKind = InferDialogIcon(title),
            PrimaryIconKind = InferPrimaryIcon(primaryText)
        };

        ShowWithDimmedOwner(owner, dialog);
        return dialog.Accepted ? dialog.InputText.Trim() : null;
    }

    public static void ShowMessage(Window owner, string title, string message, string primaryText = "OK")
    {
        var dialog = new ForgeDialogWindow
        {
            Owner = owner,
            TitleText = title,
            Heading = title,
            Message = message,
            IsInputVisible = false,
            IsCancelVisible = false,
            PrimaryText = primaryText,
            DialogIconKind = ForgeIconKind.Inspector,
            PrimaryIconKind = ForgeIconKind.Inspector
        };

        ShowWithDimmedOwner(owner, dialog);
    }

    public static bool ShowConfirmation(Window owner, string title, string message, string primaryText = "Confirm", bool danger = false)
    {
        primaryText = ResolveSemanticAction(title, primaryText);
        var dialog = new ForgeDialogWindow
        {
            Owner = owner,
            TitleText = title,
            Heading = title,
            Message = message,
            IsInputVisible = false,
            PrimaryText = primaryText,
            IsDanger = danger,
            DialogIconKind = danger ? ForgeIconKind.Delete : InferDialogIcon(title),
            PrimaryIconKind = danger ? ForgeIconKind.Delete : InferPrimaryIcon(primaryText)
        };

        ShowWithDimmedOwner(owner, dialog);
        return dialog.Accepted;
    }



    public static void ShowRepairPlanSummary(
        Window owner,
        int totalPlans,
        int readyPlans,
        int userChoicePlans,
        int assistedPlans)
    {
        var dialog = new ForgeDialogWindow
        {
            Owner = owner,
            TitleText = "Fix All Issues",
            Heading = "Repair Plan Ready",
            Message = "RimForge analyzed the visible issues and prepared a deterministic repair preview.",
            DetailContent = BuildRepairSummary(totalPlans, readyPlans, userChoicePlans, assistedPlans),
            IsInputVisible = false,
            IsCancelVisible = false,
            PrimaryText = "Close",
            DialogIconKind = ForgeIconKind.Repair,
            PrimaryIconKind = ForgeIconKind.Repair
        };

        ShowWithDimmedOwner(owner, dialog);
    }

    public static void ShowRepairPreview(
        Window owner,
        string title,
        string summary,
        IReadOnlyList<string> steps,
        IReadOnlyList<string> choices,
        string expectedResult)
    {
        var dialog = new ForgeDialogWindow
        {
            Owner = owner,
            TitleText = "ForgeRepair Preview",
            Heading = title,
            Message = summary,
            DetailContent = BuildRepairPreview(steps, choices, expectedResult),
            IsInputVisible = false,
            IsCancelVisible = false,
            PrimaryText = "Close",
            DialogIconKind = ForgeIconKind.Repair,
            PrimaryIconKind = ForgeIconKind.Repair
        };

        ShowWithDimmedOwner(owner, dialog);
    }


    public static bool ShowLaunchReadinessReview(
        Window owner,
        string profileName,
        int activeModCount,
        int errorCount,
        int warningCount,
        bool hasSavedProfile,
        bool executableFound,
        string? executablePath,
        string? latestForgeState,
        bool hasUnsavedChanges)
    {
        var isReady = hasSavedProfile && executableFound && errorCount == 0;
        var hasWarnings = isReady && (warningCount > 0 || hasUnsavedChanges);
        var heading = !isReady
            ? "NOT READY FOR LAUNCH"
            : hasWarnings
                ? "GO FOR LAUNCH (WITH WARNINGS)"
                : "GO FOR LAUNCH";

        var message = !hasSavedProfile
            ? "RimForge could not find a saved profile revision to launch."
            : !executableFound
                ? "Someone stole our rocket. RimWorld.exe could not be located."
                : errorCount > 0
                    ? "The saved profile has blocking errors that must be resolved before launch."
                    : hasUnsavedChanges
                        ? "The workspace contains unsaved changes. RimForge can only launch the last saved profile revision."
                        : hasWarnings
                            ? "The saved profile can launch, but RimForge found warnings you should review."
                            : "The saved profile passed the current launch-readiness checks.";

        var dialog = new ForgeDialogWindow
        {
            Owner = owner,
            TitleText = "Launch Readiness Review",
            Heading = heading,
            Message = message,
            DetailContent = BuildLaunchReadinessSummary(
                profileName, activeModCount, errorCount, warningCount,
                hasSavedProfile, executableFound, executablePath, latestForgeState,
                hasUnsavedChanges),
            IsInputVisible = false,
            IsCancelVisible = isReady,
            PrimaryText = isReady ? "Launch Saved Profile" : "Close",
            DialogIconKind = ForgeIconKind.Launch,
            PrimaryIconKind = ForgeIconKind.Launch
        };

        ShowWithDimmedOwner(owner, dialog);
        return isReady && dialog.Accepted;
    }

    private static FrameworkElement BuildLaunchReadinessSummary(
        string profileName,
        int activeModCount,
        int errorCount,
        int warningCount,
        bool hasSavedProfile,
        bool executableFound,
        string? executablePath,
        string? latestForgeState,
        bool hasUnsavedChanges)
    {
        var root = new StackPanel();
        var metrics = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        for (var i = 0; i < 3; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        metrics.Children.Add(BuildMetricCard("ACTIVE MODS", activeModCount.ToString(), 0));
        metrics.Children.Add(BuildMetricCard("ERRORS", errorCount.ToString(), 1));
        metrics.Children.Add(BuildMetricCard("WARNINGS", warningCount.ToString(), 2));
        root.Children.Add(metrics);

        var profileState = !hasSavedProfile
            ? LaunchCheckState.Blocked
            : hasUnsavedChanges
                ? LaunchCheckState.Warning
                : LaunchCheckState.Pass;
        var profileDetail = !hasSavedProfile
            ? "No saved profile revision is available"
            : hasUnsavedChanges
                ? $"{profileName} — unsaved changes detected; the last saved revision will launch after confirmation"
                : $"{profileName} — saved and ready to launch";
        root.Children.Add(CreateLaunchCheck("Active profile", profileState, profileDetail));
        root.Children.Add(CreateLaunchCheck(
            "RimWorld executable",
            executableFound ? LaunchCheckState.Pass : LaunchCheckState.Blocked,
            executableFound ? (executablePath ?? "Located") : "Not found"));
        root.Children.Add(CreateLaunchCheck(
            "Latest Forge state",
            string.IsNullOrWhiteSpace(latestForgeState) ? LaunchCheckState.Warning : LaunchCheckState.Pass,
            string.IsNullOrWhiteSpace(latestForgeState) ? "No completed Forge recorded" : latestForgeState!));

        var note = CreateText(
            "RimForge launches the last saved profile revision. Unsaved workspace changes are never launched automatically.",
            11, FontWeights.Normal);
        note.Margin = new Thickness(0, 14, 0, 0);
        note.TextWrapping = TextWrapping.Wrap;
        note.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        root.Children.Add(note);
        return root;
    }

    private enum LaunchCheckState
    {
        Pass,
        Warning,
        Blocked
    }

    private static Border CreateLaunchCheck(string label, LaunchCheckState stateValue, string detail)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };
        card.SetResourceReference(Border.BackgroundProperty, "Bg2Brush");
        var brushKey = stateValue switch
        {
            LaunchCheckState.Pass => "SuccessBrush",
            LaunchCheckState.Warning => "WarningBrush",
            _ => "DangerBrush"
        };
        card.SetResourceReference(Border.BorderBrushProperty, brushKey);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var stateLabel = stateValue switch
        {
            LaunchCheckState.Pass => "PASS",
            LaunchCheckState.Warning => "WARNING",
            _ => "BLOCKED"
        };
        var state = CreateText(stateLabel, 9, FontWeights.Bold);
        state.Margin = new Thickness(0, 2, 12, 0);
        state.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        grid.Children.Add(state);

        var stack = new StackPanel();
        Grid.SetColumn(stack, 1);
        stack.Children.Add(CreateText(label, 12, FontWeights.SemiBold));
        var detailText = CreateText(detail, 11, FontWeights.Normal);
        detailText.Margin = new Thickness(0, 3, 0, 0);
        detailText.TextWrapping = TextWrapping.Wrap;
        detailText.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        stack.Children.Add(detailText);
        grid.Children.Add(stack);
        card.Child = grid;
        return card;
    }

    private static FrameworkElement BuildRepairSummary(int total, int ready, int choices, int assisted)
    {
        var root = new StackPanel();
        var metrics = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        for (var i = 0; i < 3; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        metrics.Children.Add(BuildMetricCard("READY", ready.ToString(), 0));
        metrics.Children.Add(BuildMetricCard("USER DECISIONS", choices.ToString(), 1));
        metrics.Children.Add(BuildMetricCard("ASSISTED", assisted.ToString(), 2));
        root.Children.Add(metrics);

        var status = CreateText($"{total} repair plan(s) analyzed: {ready} automatic, {choices} requiring a choice, and {assisted} assisted.", 12, FontWeights.SemiBold);
        status.Margin = new Thickness(0, 0, 0, 8);
        root.Children.Add(status);

        var note = CreateText(
            "Automatic repairs run only after confirmation and use the shared task lifecycle. Choice and assisted plans remain available for explicit review.",
            11, FontWeights.Normal);
        note.TextWrapping = TextWrapping.Wrap;
        note.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        root.Children.Add(note);
        return root;
    }

    private static FrameworkElement BuildRepairPreview(IReadOnlyList<string> steps, IReadOnlyList<string> choices, string expectedResult)
    {
        var root = new StackPanel();
        if (steps.Count > 0)
        {
            root.Children.Add(CreateSectionLabel("REPAIR PLAN"));
            foreach (var step in steps) root.Children.Add(CreateBullet(step));
        }

        if (choices.Count > 0)
        {
            root.Children.Add(CreateSectionLabel("USER DECISION REQUIRED", new Thickness(0, 14, 0, 7)));
            foreach (var choice in choices) root.Children.Add(CreateBullet(choice));
        }

        root.Children.Add(CreateSectionLabel("EXPECTED RESULT", new Thickness(0, 14, 0, 7)));
        var expected = CreateText(expectedResult, 12, FontWeights.SemiBold);
        expected.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(expected);

        var note = CreateText("Preview only — no files or profiles will be changed.", 11, FontWeights.Normal);
        note.Margin = new Thickness(0, 14, 0, 0);
        note.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        root.Children.Add(note);
        return root;
    }

    private static Border BuildMetricCard(string label, string value, int column)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Margin = new Thickness(column == 0 ? 0 : 5, 0, column == 2 ? 0 : 5, 0)
        };
        card.SetResourceReference(Border.BackgroundProperty, "Bg2Brush");
        card.SetResourceReference(Border.BorderBrushProperty, "Bg4Brush");
        Grid.SetColumn(card, column);
        var stack = new StackPanel();
        var labelText = CreateText(label, 9, FontWeights.Bold);
        labelText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        var valueText = CreateText(value, 22, FontWeights.Bold);
        valueText.Margin = new Thickness(0, 4, 0, 0);
        stack.Children.Add(labelText);
        stack.Children.Add(valueText);
        card.Child = stack;
        return card;
    }

    private static TextBlock CreateSectionLabel(string text, Thickness? margin = null)
    {
        var block = CreateText(text, 10, FontWeights.Bold);
        block.Margin = margin ?? new Thickness(0, 0, 0, 7);
        block.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        return block;
    }

    private static TextBlock CreateBullet(string text)
    {
        var block = CreateText($"• {text}", 12, FontWeights.Normal);
        block.Margin = new Thickness(0, 2, 0, 2);
        block.TextWrapping = TextWrapping.Wrap;
        return block;
    }

    private static TextBlock CreateText(string text, double fontSize, FontWeight weight) => new()
    {
        Text = text,
        FontSize = fontSize,
        FontWeight = weight
    };

    private static string ResolveSemanticAction(string title, string requestedText)
    {
        var isGeneric = string.IsNullOrWhiteSpace(requestedText)
            || requestedText.Equals("Save", StringComparison.OrdinalIgnoreCase)
            || requestedText.Equals("Confirm", StringComparison.OrdinalIgnoreCase)
            || requestedText.Equals("OK", StringComparison.OrdinalIgnoreCase);

        if (!isGeneric)
        {
            return requestedText;
        }

        if (title.Contains("create", StringComparison.OrdinalIgnoreCase)) return "Create";
        if (title.Contains("rename", StringComparison.OrdinalIgnoreCase)) return "Rename";
        if (title.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || title.Contains("clone", StringComparison.OrdinalIgnoreCase)) return "Duplicate";
        if (title.Contains("import", StringComparison.OrdinalIgnoreCase)) return "Import";
        if (title.Contains("export", StringComparison.OrdinalIgnoreCase)) return "Export";
        if (title.Contains("delete", StringComparison.OrdinalIgnoreCase) || title.Contains("remove", StringComparison.OrdinalIgnoreCase)) return "Delete";
        if (title.Contains("unlock", StringComparison.OrdinalIgnoreCase)) return "Unlock";
        if (title.Contains("lock", StringComparison.OrdinalIgnoreCase)) return "Lock";
        if (title.Contains("favorite", StringComparison.OrdinalIgnoreCase)) return "Favorite";
        return requestedText;
    }

    private static ForgeIconKind InferDialogIcon(string title)
    {
        if (title.Contains("create", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Add;
        if (title.Contains("rename", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Rename;
        if (title.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || title.Contains("clone", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Duplicate;
        if (title.Contains("import", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Import;
        if (title.Contains("export", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Export;
        if (title.Contains("favorite", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Favorite;
        if (title.Contains("lock", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Lock;
        if (title.Contains("delete", StringComparison.OrdinalIgnoreCase) || title.Contains("remove", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Delete;
        return ForgeIconKind.Profile;
    }

    private static ForgeIconKind InferPrimaryIcon(string text)
    {
        if (text.Contains("create", StringComparison.OrdinalIgnoreCase) || text.Contains("add", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Add;
        if (text.Contains("rename", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Rename;
        if (text.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || text.Contains("clone", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Duplicate;
        if (text.Contains("import", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Import;
        if (text.Contains("export", StringComparison.OrdinalIgnoreCase) || text.Contains("save", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Export;
        if (text.Contains("delete", StringComparison.OrdinalIgnoreCase) || text.Contains("remove", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Delete;
        if (text.Contains("favorite", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Favorite;
        if (text.Contains("lock", StringComparison.OrdinalIgnoreCase) || text.Contains("unlock", StringComparison.OrdinalIgnoreCase)) return ForgeIconKind.Lock;
        return ForgeIconKind.Profile;
    }

    private static void ShowWithDimmedOwner(Window owner, Window dialog)
    {
        var originalOpacity = owner.Opacity;
        var originalEffect = owner.Effect;
        try
        {
            owner.Opacity = 0.42;
            dialog.ShowDialog();
        }
        finally
        {
            owner.Opacity = originalOpacity;
            owner.Effect = originalEffect;
            owner.Activate();
        }
    }
}

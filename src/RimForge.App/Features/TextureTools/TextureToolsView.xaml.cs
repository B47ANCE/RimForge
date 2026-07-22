using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RimForge.Core.BackgroundTasks;
using RimForge.Core.Models;

namespace RimForge.App.Features.TextureTools;

public partial class TextureToolsView : UserControl, INotifyPropertyChanged
{
    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public InlineProgress(Action<T> report) => _report = report;

        public void Report(T value) => _report(value);
    }

    private sealed record TextureDiscoveryResult(IReadOnlyList<TextureQueueItem> Items, int SkippedRoots);
    private sealed record TextureConversionSummary(int Completed, int Skipped, int Failed);
    private sealed record TextureCandidate(string SourcePath, string RelativePath);

    private TextureConversionEngine _engine = null!;
    private string _statusTitle = "Ready to analyze textures";
    private string _statusDetail = "Analyze the active profile to begin.";
    private double _progressValue;
    private bool _isConverting;
    private bool _hasAnalyzedProfile;
    private bool _isAnalyzing;
    private string _outputPath = string.Empty;

    public TextureToolsView()
    {
        InitializeComponent();
        Queue.CollectionChanged += (_, _) => RefreshSummary();
        Loaded += (_, _) => RefreshSummary();
    }

    public void ConfigurePaths(RimForgePathLayout paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _engine = new TextureConversionEngine(paths.TempRoot);
        _outputPath = Path.Combine(paths.ExportsRoot, "ConvertedTextures");
        OnPropertyChanged(nameof(DefaultOutputPath));
        if (OutputPathText is not null) OutputPathText.Text = _outputPath;
    }

    public ObservableCollection<TextureQueueItem> Queue { get; } = new();
    public string EngineStatus => File.Exists(Path.Combine(AppContext.BaseDirectory, "Tools", "DirectXTex", "texconv.exe"))
        ? "Conversion engine + DirectXTex ready"
        : "Conversion engine ready • DDS export needs texconv";
    public string QueueSummary => $"{Queue.Count} analyzed";
    public string SourceSizeSummary => TextureQueueItem.FormatBytes(Queue.Sum(item => item.SourceBytes));
    public string DefaultOutputPath => _outputPath;
    public string AnalyzeButtonText => _isAnalyzing
        ? "Analyzing Active Profile..."
        : _hasAnalyzedProfile
            ? "Re-Analyze Active Profile"
            : "Analyze Active Profile";
    public string StatusTitle { get => _statusTitle; private set { _statusTitle = value; OnPropertyChanged(); } }
    public string StatusDetail { get => _statusDetail; private set { _statusDetail = value; OnPropertyChanged(); } }
    public double ProgressValue { get => _progressValue; private set { _progressValue = value; OnPropertyChanged(); } }
    public bool IsConverting
    {
        get => _isConverting;
        private set
        {
            _isConverting = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConvert));
            OnPropertyChanged(nameof(CanRunTools));
            OnPropertyChanged(nameof(AnalyzeButtonText));
        }
    }
    public bool CanConvert => Queue.Any(IsEligibleForConversion) && !IsConverting;
    public bool CanRunTools => !IsConverting;
    public event EventHandler<string>? ActivityRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    private MainWindow? Host => Window.GetWindow(this) as MainWindow;

    private async void AnalyzeProfile_Click(object sender, RoutedEventArgs e)
    {
        if (IsConverting) return;
        var host = Host;
        if (host?.SelectedProfile is null)
        {
            SetStatus("No active profile", "Select a profile before analysis.");
            return;
        }
        if (host.IsBackgroundTaskRunning)
        {
            SetStatus("Another operation is running", host.BackgroundTaskCurrentOperation);
            return;
        }

        var profileName = host.SelectedProfile.Name;
        var ids = host.SelectedProfile.ActiveMods.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mods = host.Mods
            .Where(mod => mod.PackageId is not null && ids.Contains(mod.PackageId) && Directory.Exists(mod.RootPath))
            .ToList();

        SetBusy(isBusy: true, isAnalyzing: true);
        ProgressValue = 0;
        Queue.Clear();
        SetStatus("Analyzing active profile", profileName);

        try
        {
            var discovery = await host.RunFeatureTaskAsync(
                "texture.analyze",
                "Analyze Profile Textures",
                async context =>
                {
                    var result = DiscoverProfileTextures(mods, context);
                    const int batchSize = 100;
                    for (var offset = 0; offset < result.Items.Count; offset += batchSize)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        var batch = result.Items.Skip(offset).Take(batchSize).ToArray();
                        await Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var item in batch) Queue.Add(item);
                            var synchronized = Math.Min(offset + batch.Length, result.Items.Count);
                            ProgressValue = result.Items.Count == 0 ? 100d : 85d + synchronized * 15d / result.Items.Count;
                            SetStatus($"Loading texture {synchronized} of {result.Items.Count}", batch.LastOrDefault()?.SourcePath ?? profileName);
                        }).Task.ConfigureAwait(false);

                        var synchronizedCount = Math.Min(offset + batch.Length, result.Items.Count);
                        context.Report(new BackgroundTaskProgress(
                            "Synchronizing texture queue",
                            $"Loaded {synchronizedCount} of {result.Items.Count} texture(s).",
                            batch.LastOrDefault()?.RelativePath ?? profileName,
                            result.Items.Count == 0 ? 100d : 85d + synchronizedCount * 15d / result.Items.Count,
                            synchronizedCount,
                            result.Items.Count,
                            profileName,
                            batch.LastOrDefault()?.SourcePath ?? string.Empty));
                    }
                    return result;
                });

            _hasAnalyzedProfile = true;
            OnPropertyChanged(nameof(AnalyzeButtonText));
            var invalidDds = Queue.Count(item =>
                Path.GetExtension(item.SourcePath).Equals(".dds", StringComparison.OrdinalIgnoreCase) &&
                item.State == TextureQueueState.Failed);
            var eligible = Queue.Count(IsEligibleForConversion);
            var skippedText = discovery.SkippedRoots > 0 ? $" • {discovery.SkippedRoots} inaccessible mod folder(s) skipped" : string.Empty;
            ProgressValue = 100;
            SetStatus(
                "Active profile analysis complete",
                $"{Queue.Count} textures • {eligible} eligible for conversion • {invalidDds} invalid DDS{skippedText}");
            ActivityRequested?.Invoke(
                this,
                $"Texture analysis complete for {profileName}: {Queue.Count} textures, {eligible} eligible, {invalidDds} invalid DDS.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Profile analysis cancelled", $"{Queue.Count} textures were synchronized before cancellation.");
        }
        catch (Exception ex)
        {
            SetStatus("Profile analysis failed", ex.Message);
        }
        finally
        {
            SetBusy(isBusy: false, isAnalyzing: false);
            RefreshSummary();
        }
    }

    private static TextureDiscoveryResult DiscoverProfileTextures(
        IReadOnlyList<ModRecord> mods,
        BackgroundTaskContext context)
    {
        var candidates = new List<TextureCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skippedRoots = 0;

        for (var modIndex = 0; modIndex < mods.Count; modIndex++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var mod = mods[modIndex];
            var prefix = SanitizeSegment(mod.PackageId ?? mod.DisplayName);
            try
            {
                foreach (var file in Directory.EnumerateFiles(mod.RootPath, "*.*", SearchOption.AllDirectories))
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    if (!TextureConversionEngine.IsSupportedInput(file) || !seen.Add(file)) continue;
                    candidates.Add(new TextureCandidate(
                        file,
                        Path.Combine(prefix, Path.GetRelativePath(mod.RootPath, file))));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skippedRoots++;
            }

            var foldersProcessed = modIndex + 1;
            context.Report(new BackgroundTaskProgress(
                "Discovering profile textures",
                $"Scanned {mod.DisplayName} ({foldersProcessed}/{mods.Count}).",
                mod.RootPath,
                mods.Count == 0 ? 15d : foldersProcessed * 15d / mods.Count,
                foldersProcessed,
                mods.Count,
                $"{candidates.Count} supported texture(s) discovered",
                mod.RootPath));
        }

        var results = new List<TextureQueueItem>(candidates.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var candidate = candidates[index];
            var info = new FileInfo(candidate.SourcePath);
            var item = new TextureQueueItem
            {
                SourcePath = candidate.SourcePath,
                RelativePath = candidate.RelativePath,
                SourceBytes = info.Length
            };

            if (Path.GetExtension(candidate.SourcePath).Equals(".dds", StringComparison.OrdinalIgnoreCase))
            {
                var valid = TextureConversionEngine.ValidateDds(candidate.SourcePath, out var message);
                item.Status = message;
                item.State = valid ? TextureQueueState.Completed : TextureQueueState.Failed;
            }
            else
            {
                item.Status = "Eligible for DDS conversion";
                item.State = TextureQueueState.Queued;
            }
            results.Add(item);

            var processed = index + 1;
            if (processed == candidates.Count || processed % 20 == 0)
            {
                context.Report(new BackgroundTaskProgress(
                    "Validating profile textures",
                    $"Validated {processed} of {candidates.Count} texture(s).",
                    candidate.RelativePath,
                    candidates.Count == 0 ? 85d : 15d + processed * 70d / candidates.Count,
                    processed,
                    candidates.Count,
                    Path.GetFileName(candidate.SourcePath),
                    candidate.SourcePath));
            }
        }

        if (candidates.Count == 0)
        {
            context.Report(new BackgroundTaskProgress(
                "Validating profile textures",
                "No supported textures were found in the active profile.",
                string.Empty,
                100d,
                0,
                0,
                "Texture discovery complete"));
        }

        return new TextureDiscoveryResult(results, skippedRoots);
    }

    private static string SanitizeSegment(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(value) ? "UnknownMod" : value;
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select conversion output folder",
            InitialDirectory = Directory.Exists(_outputPath) ? _outputPath : null
        };
        if (dialog.ShowDialog() != true) return;
        _outputPath = dialog.FolderName;
        OnPropertyChanged(nameof(DefaultOutputPath));
        OutputPathText.Text = _outputPath;
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FormatCombo is null || SizeCombo is null || PreserveAlphaCheck is null || SkipUnchangedCheck is null) return;
        switch (PresetCombo.SelectedIndex)
        {
            case 1:
                FormatCombo.SelectedIndex = 6;
                SizeCombo.SelectedIndex = 0;
                PreserveAlphaCheck.IsChecked = false;
                SkipUnchangedCheck.IsChecked = true;
                SetStatus("Performance preset selected", "DDS BC1 • 1024 maximum • unchanged outputs skipped");
                break;
            case 2:
                FormatCombo.SelectedIndex = 4;
                SizeCombo.SelectedIndex = 2;
                PreserveAlphaCheck.IsChecked = true;
                SkipUnchangedCheck.IsChecked = true;
                SetStatus("Quality preset selected", "DDS BC7 • 4096 maximum • alpha preserved");
                break;
            default:
                FormatCombo.SelectedIndex = 4;
                SizeCombo.SelectedIndex = 1;
                PreserveAlphaCheck.IsChecked = true;
                SkipUnchangedCheck.IsChecked = true;
                if (IsLoaded)
                    SetStatus("Balanced preset selected", "DDS BC7 • 2048 maximum • source files remain untouched");
                break;
        }
    }

    private async void ConvertSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = QueueList.SelectedItems.Cast<TextureQueueItem>().Where(IsEligibleForConversion).ToList();
        if (selected.Count == 0)
        {
            SetStatus("No eligible textures selected", "Select one or more non-DDS textures from the active profile analysis.");
            return;
        }
        await ConvertItemsAsync(selected);
    }

    private async void ConvertAll_Click(object sender, RoutedEventArgs e) =>
        await ConvertItemsAsync(Queue.Where(IsEligibleForConversion).ToList());

    private async Task ConvertItemsAsync(IReadOnlyList<TextureQueueItem> items)
    {
        if (items.Count == 0 || IsConverting) return;
        var host = Host;
        if (host is null) return;
        if (host.IsBackgroundTaskRunning)
        {
            SetStatus("Another operation is running", host.BackgroundTaskCurrentOperation);
            return;
        }

        var outputPath = _outputPath;
        var format = (FormatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "DDS (BC7)";
        var maxSize = int.TryParse((SizeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var parsed) ? parsed : 2048;
        var options = new TextureConversionOptions(
            format,
            maxSize,
            PreserveAlphaCheck.IsChecked == true,
            SkipUnchangedCheck.IsChecked == true,
            outputPath,
            outputPath);

        SetBusy(isBusy: true, isAnalyzing: false);
        ProgressValue = 0;
        try
        {
            var summary = await host.RunFeatureTaskAsync(
                "texture.convert",
                "Convert Profile Textures",
                async context =>
                {
                    Directory.CreateDirectory(outputPath);
                    var completed = 0;
                    var failed = 0;
                    var skipped = 0;
                    for (var index = 0; index < items.Count; index++)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        var item = items[index];
                        await Dispatcher.InvokeAsync(() =>
                        {
                            item.State = TextureQueueState.Converting;
                            item.Status = "Converting…";
                            SetStatus($"Converting {index + 1} of {items.Count}", item.SourcePath);
                        }).Task.ConfigureAwait(false);

                        context.Report(new BackgroundTaskProgress(
                            "Converting profile textures",
                            $"Converting {index + 1} of {items.Count}: {item.FileName}",
                            item.RelativePath,
                            index * 100d / items.Count,
                            index,
                            items.Count,
                            format,
                            item.SourcePath));
                        ActivityRequested?.Invoke(this, $"Texture conversion: {item.SourcePath}");

                        var itemOutputRoot = Path.Combine(outputPath, Path.GetDirectoryName(item.RelativePath) ?? string.Empty);
                        var result = await _engine.ConvertAsync(
                            item.SourcePath,
                            options with { OutputRoot = itemOutputRoot },
                            context.CancellationToken).ConfigureAwait(false);

                        if (result.Skipped) skipped++;
                        else if (result.Success) completed++;
                        else failed++;
                        var processed = index + 1;
                        await Dispatcher.InvokeAsync(() =>
                        {
                            item.OutputPath = result.OutputPath;
                            item.Status = result.Message;
                            item.State = result.Skipped
                                ? TextureQueueState.Skipped
                                : result.Success
                                    ? TextureQueueState.Completed
                                    : TextureQueueState.Failed;
                            ProgressValue = processed * 100d / items.Count;
                        }).Task.ConfigureAwait(false);
                        context.Report(new BackgroundTaskProgress(
                            "Converting profile textures",
                            $"Processed {processed} of {items.Count}: {item.FileName}",
                            result.Message,
                            processed * 100d / items.Count,
                            processed,
                            items.Count,
                            result.Success ? "Conversion output verified" : result.Message,
                            result.OutputPath ?? item.SourcePath));
                    }
                    return new TextureConversionSummary(completed, skipped, failed);
                });

            SetStatus(
                "Texture conversion complete",
                $"{summary.Completed} converted • {summary.Skipped} skipped • {summary.Failed} failed • {outputPath}");
            ActivityRequested?.Invoke(
                this,
                $"Texture conversion complete: {summary.Completed} converted, {summary.Skipped} skipped, {summary.Failed} failed.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Conversion cancelled", "Completed outputs remain valid; no further textures were processed.");
        }
        catch (Exception ex)
        {
            SetStatus("Conversion failed", ex.Message);
        }
        finally
        {
            SetBusy(isBusy: false, isAnalyzing: false);
            RefreshSummary();
        }
    }

    private async void RevertConversion_Click(object sender, RoutedEventArgs e)
    {
        if (IsConverting) return;
        var host = Host;
        if (host is null) return;
        if (host.IsBackgroundTaskRunning)
        {
            SetStatus("Another operation is running", host.BackgroundTaskCurrentOperation);
            return;
        }

        var outputPath = _outputPath;
        SetBusy(isBusy: true, isAnalyzing: false);
        ProgressValue = 0;
        try
        {
            var result = await host.RunFeatureTaskAsync(
                "texture.revert",
                "Revert Texture Conversion",
                context =>
                {
                    var progress = new InlineProgress<TextureRevertProgress>(update =>
                    {
                        context.Report(new BackgroundTaskProgress(
                            "Reverting generated textures",
                            $"Reviewed {update.Processed} of {update.Total} generated file(s).",
                            update.Action,
                            update.Total == 0 ? 100d : update.Processed * 100d / update.Total,
                            update.Processed,
                            update.Total,
                            update.Action,
                            update.OutputPath));
                        Dispatcher.Invoke(() =>
                        {
                            ProgressValue = update.Total == 0 ? 100d : update.Processed * 100d / update.Total;
                            SetStatus("Reverting generated textures", update.OutputPath);
                        });
                    });
                    return TextureConversionManifestStore.RevertAsync(outputPath, progress, context.CancellationToken);
                });
            ProgressValue = 100;
            SetStatus(
                "Conversion revert complete",
                $"{result.Deleted} RimForge-generated file(s) removed • {result.Skipped} skipped");
            ActivityRequested?.Invoke(this, $"Texture conversion revert complete: {result.Deleted} deleted, {result.Skipped} skipped.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Conversion revert cancelled", "No further generated files were removed.");
        }
        catch (Exception ex)
        {
            SetStatus("Conversion revert failed", ex.Message);
        }
        finally
        {
            SetBusy(isBusy: false, isAnalyzing: false);
        }
    }

    private static bool IsEligibleForConversion(TextureQueueItem item) =>
        !Path.GetExtension(item.SourcePath).Equals(".dds", StringComparison.OrdinalIgnoreCase);

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (Host?.CancelFeatureTask("Texture operation cancellation requested.") == true)
            SetStatus("Cancelling texture operation", BackgroundTaskCurrentFileOrStatus());
    }

    private string BackgroundTaskCurrentFileOrStatus() =>
        string.IsNullOrWhiteSpace(Host?.BackgroundTaskCurrentFile)
            ? "Waiting for the current file operation to stop safely."
            : Host.BackgroundTaskCurrentFile;

    private void SetBusy(bool isBusy, bool isAnalyzing)
    {
        _isAnalyzing = isAnalyzing;
        IsConverting = isBusy;
        OnPropertyChanged(nameof(AnalyzeButtonText));
    }

    private void RefreshSummary()
    {
        EmptyQueuePanel.Visibility = Queue.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(SourceSizeSummary));
        OnPropertyChanged(nameof(CanConvert));
    }

    private void SetStatus(string title, string detail)
    {
        StatusTitle = title;
        StatusDetail = detail;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

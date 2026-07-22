using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RimForge.App.Features.TextureTools;

public enum TextureQueueState { Queued, Converting, Completed, Skipped, Failed }

public sealed class TextureQueueItem : INotifyPropertyChanged
{
    private TextureQueueState _state = TextureQueueState.Queued;
    private string _status = "Queued";
    private string? _outputPath;

    public required string SourcePath { get; init; }
    public required string RelativePath { get; init; }
    public required long SourceBytes { get; init; }
    public string FileName => Path.GetFileName(SourcePath);
    public string SourceSize => FormatBytes(SourceBytes);
    public TextureQueueState State { get => _state; set { _state = value; OnPropertyChanged(); OnPropertyChanged(nameof(StateText)); } }
    public string StateText => State.ToString();
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public string? OutputPath { get => _outputPath; set { _outputPath = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}

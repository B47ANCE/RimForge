using System.Text;
using RimForge.Core.Models;
using RimForge.Core.Services;

namespace RimForge.Infrastructure.Services;

public sealed class GameLogService : IGameLogService
{
    private const int InitialTailLines = 500;
    private const int InitialTailBytes = 256 * 1024;

    private readonly SemaphoreSlim _historyGate = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _watchTask;
    private long _historyPosition;

    public bool IsWatching => _cts is not null;
    public string? CurrentPath { get; private set; }
    public event EventHandler<GameLogEntry>? EntryReceived;
    public event EventHandler<GameLogReplaySummary>? StartupReplayCompleted;
    public event EventHandler<bool>? WatchingChanged;

    public async Task StartAsync(string playerLogPath, bool startAtEnd = true, CancellationToken cancellationToken = default)
    {
        await StopAsync();
        CurrentPath = playerLogPath;
        _historyPosition = 0;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        WatchingChanged?.Invoke(this, true);

        long watchPosition = 0;
        if (File.Exists(playerLogPath))
        {
            try
            {
                await using var stream = OpenReadStream(playerLogPath);
                watchPosition = stream.Length;
                if (startAtEnd && stream.Length > 0)
                {
                    var tail = await ReadWindowEndingAtAsync(stream, stream.Length, InitialTailLines, InitialTailBytes, _cts.Token);
                    _historyPosition = tail.StartPosition;
                    foreach (var entry in tail.Entries)
                        EntryReceived?.Invoke(this, entry);

                    StartupReplayCompleted?.Invoke(this, new GameLogReplaySummary(
                        playerLogPath,
                        stream.Length,
                        tail.Entries.Count,
                        tail.Entries.Count(entry => entry.Severity == GameLogSeverity.Warning),
                        tail.Entries.Count(entry => entry.Severity == GameLogSeverity.Error),
                        tail.IncludedUnterminatedFinalLine,
                        DateTimeOffset.UtcNow));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        _watchTask = WatchAsync(playerLogPath, startAtEnd ? watchPosition : 0, _cts.Token);
    }

    public async Task<IReadOnlyList<GameLogEntry>> LoadPreviousAsync(
        int maxLines = InitialTailLines,
        int maxBytes = InitialTailBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CurrentPath) || _historyPosition <= 0 || maxLines <= 0 || maxBytes <= 0)
            return Array.Empty<GameLogEntry>();

        await _historyGate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(CurrentPath) || _historyPosition <= 0)
                return Array.Empty<GameLogEntry>();

            await using var stream = OpenReadStream(CurrentPath);
            var endPosition = Math.Min(_historyPosition, stream.Length);
            var window = await ReadWindowEndingAtAsync(stream, endPosition, maxLines, maxBytes, cancellationToken);
            _historyPosition = window.StartPosition;
            return window.Entries;
        }
        catch (IOException)
        {
            return Array.Empty<GameLogEntry>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<GameLogEntry>();
        }
        finally
        {
            _historyGate.Release();
        }
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        _cts.Cancel();
        try { if (_watchTask is not null) await _watchTask; }
        catch (OperationCanceledException) { }
        _cts.Dispose();
        _cts = null;
        _watchTask = null;
        WatchingChanged?.Invoke(this, false);
    }

    private async Task WatchAsync(string path, long initialPosition, CancellationToken cancellationToken)
    {
        var position = initialPosition;
        var partial = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!File.Exists(path))
                {
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                await using var stream = OpenReadStream(path);
                if (stream.Length < position)
                {
                    position = 0;
                    _historyPosition = 0;
                    partial.Clear();
                }

                stream.Seek(position, SeekOrigin.Begin);
                using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true);
                var chunk = await reader.ReadToEndAsync(cancellationToken);
                position = stream.Position;
                if (chunk.Length > 0) EmitLines(partial, chunk);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            await Task.Delay(250, cancellationToken);
        }
    }

    private static FileStream OpenReadStream(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete,
        4096,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static async Task<LogWindow> ReadWindowEndingAtAsync(
        FileStream stream,
        long endPosition,
        int maxLines,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        endPosition = Math.Clamp(endPosition, 0, stream.Length);
        var startPosition = Math.Max(0, endPosition - maxBytes);
        var length = checked((int)(endPosition - startPosition));
        if (length == 0) return new LogWindow(startPosition, Array.Empty<GameLogEntry>(), false);

        var buffer = new byte[length];
        stream.Seek(startPosition, SeekOrigin.Begin);
        var read = 0;
        while (read < length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read, length - read), cancellationToken);
            if (count == 0) break;
            read += count;
        }

        var sliceStart = 0;
        if (startPosition > 0)
        {
            while (sliceStart < read && buffer[sliceStart] != (byte)'\n') sliceStart++;
            if (sliceStart < read) sliceStart++;
        }

        var lineStarts = new List<int> { sliceStart };
        for (var i = sliceStart; i < read; i++)
        {
            if (buffer[i] == (byte)'\n' && i + 1 < read)
                lineStarts.Add(i + 1);
        }

        var selectedLineIndex = Math.Max(0, lineStarts.Count - maxLines);
        sliceStart = lineStarts[selectedLineIndex];
        var selectedStartPosition = startPosition + sliceStart;
        var text = Encoding.UTF8.GetString(buffer, sliceStart, read - sliceStart);
        // Player.log can be observed mid-write; the final non-newline line may therefore be valid and must be replayed.
        var includedUnterminatedFinalLine = text.Length > 0 && !text.EndsWith('\n');
        var entries = ParseWindowLines(text);
        return new LogWindow(selectedStartPosition, entries, includedUnterminatedFinalLine);
    }

    private static IReadOnlyList<GameLogEntry> ParseWindowLines(string text)
    {
        // Player.log is frequently observed while RimWorld is still writing. The final
        // diagnostic line may therefore be valid but not newline-terminated yet. Startup
        // replay must include it or early warnings can disappear forever when live tailing
        // begins at the current file end.
        var lines = text.Split('\n');
        var entries = new List<GameLogEntry>(lines.Length);
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length > 0)
                entries.Add(new GameLogEntry(DateTimeOffset.Now, line, Classify(line)));
        }
        return entries;
    }

    private void EmitLines(StringBuilder partial, string chunk)
    {
        partial.Append(chunk);
        var text = partial.ToString();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            var line = text[start..i].TrimEnd('\r');
            if (line.Length > 0) EntryReceived?.Invoke(this, new GameLogEntry(DateTimeOffset.Now, line, Classify(line)));
            start = i + 1;
        }
        partial.Clear();
        if (start < text.Length) partial.Append(text[start..]);
    }

    private static GameLogSeverity Classify(string line)
    {
        if (line.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Could not", StringComparison.OrdinalIgnoreCase)) return GameLogSeverity.Error;
        if (line.Contains("Warning", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("missing", StringComparison.OrdinalIgnoreCase)) return GameLogSeverity.Warning;
        return GameLogSeverity.Information;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _historyGate.Dispose();
    }

    private sealed record LogWindow(long StartPosition, IReadOnlyList<GameLogEntry> Entries, bool IncludedUnterminatedFinalLine);
}

using System.Collections.Concurrent;
using RimForge.Core.Models;

namespace RimForge.Infrastructure.Services;

public sealed record ForgeEvidenceServiceOptions
{
    public static ForgeEvidenceServiceOptions Default { get; } = new();

    public TimeSpan WatcherDebounce { get; init; } = TimeSpan.FromMilliseconds(400);
    public int WatcherBufferSize { get; init; } = 64 * 1024;
    public int MaximumParallelScans { get; init; } = Math.Clamp(Environment.ProcessorCount - 1, 2, 6);
    public bool IgnoreTransientFiles { get; init; } = true;

    internal void Validate()
    {
        if (WatcherDebounce < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(WatcherDebounce));
        if (WatcherBufferSize is < 4 * 1024 or > 64 * 1024)
            throw new ArgumentOutOfRangeException(nameof(WatcherBufferSize), "FileSystemWatcher buffers must be between 4 KiB and 64 KiB.");
        if (MaximumParallelScans < 1)
            throw new ArgumentOutOfRangeException(nameof(MaximumParallelScans));
    }
}

public sealed record ForgeEvidenceInvalidation(
    long Sequence,
    string RootPath,
    ForgeEvidenceInvalidationReason Reason,
    DateTimeOffset InvalidatedAtUtc);

/// <summary>
/// Tracks invalidations using monotonic sequence numbers so a refresh only acknowledges
/// the exact changes it observed. File events arriving during a refresh remain pending.
/// </summary>
internal sealed class ForgeEvidenceInvalidationJournal
{
    private readonly ConcurrentDictionary<string, ForgeEvidenceInvalidation> _entries =
        new(StringComparer.OrdinalIgnoreCase);
    private long _sequence;

    public ForgeEvidenceInvalidation Record(string rootPath, ForgeEvidenceInvalidationReason reason)
    {
        var fullPath = Path.GetFullPath(rootPath);
        return _entries.AddOrUpdate(
            fullPath,
            _ => Create(fullPath, reason),
            (_, current) => Merge(current, reason));
    }

    public IReadOnlyList<ForgeEvidenceInvalidation> Capture() =>
        _entries.Values
            .OrderBy(value => value.Sequence)
            .ThenBy(value => value.RootPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public void Acknowledge(IEnumerable<ForgeEvidenceInvalidation> captured)
    {
        foreach (var invalidation in captured)
        {
            if (_entries.TryGetValue(invalidation.RootPath, out var current) &&
                current.Sequence == invalidation.Sequence)
            {
                _entries.TryRemove(invalidation.RootPath, out _);
            }
        }
    }

    public bool Contains(string rootPath) => _entries.ContainsKey(Path.GetFullPath(rootPath));

    private ForgeEvidenceInvalidation Create(string rootPath, ForgeEvidenceInvalidationReason reason) =>
        new(Interlocked.Increment(ref _sequence), rootPath, reason, DateTimeOffset.UtcNow);

    private ForgeEvidenceInvalidation Merge(
        ForgeEvidenceInvalidation current,
        ForgeEvidenceInvalidationReason reason)
    {
        var effectiveReason = Priority(reason) >= Priority(current.Reason) ? reason : current.Reason;
        return Create(current.RootPath, effectiveReason);
    }

    private static int Priority(ForgeEvidenceInvalidationReason reason) => reason switch
    {
        ForgeEvidenceInvalidationReason.WatcherOverflow => 100,
        ForgeEvidenceInvalidationReason.CacheCorrupt => 90,
        ForgeEvidenceInvalidationReason.TargetVersionChanged => 80,
        ForgeEvidenceInvalidationReason.ModRemoved => 70,
        ForgeEvidenceInvalidationReason.ModAdded => 60,
        ForgeEvidenceInvalidationReason.Manual => 50,
        ForgeEvidenceInvalidationReason.FileChanged => 10,
        _ => 0
    };
}

internal static class ForgeEvidenceContributionReconciler
{
    /// <summary>
    /// Replaces the complete projection for every source kind that participated in the refresh.
    /// Append-oriented sources that did not run, such as runtime observations, remain untouched.
    /// This also removes evidence belonging to mods that are no longer installed.
    /// </summary>
    public static IReadOnlyList<ForgeEvidenceContribution> ReconcileRefresh(
        IEnumerable<ForgeEvidenceContribution> existing,
        IEnumerable<ForgeEvidenceContribution> collected,
        IReadOnlySet<ForgeEvidenceSourceKind> refreshedSources,
        out int replacedCount)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(collected);
        ArgumentNullException.ThrowIfNull(refreshedSources);

        var collectedArray = collected.ToArray();
        var existingArray = existing.ToArray();
        var retained = existingArray
            .Where(value => !refreshedSources.Contains(value.Provenance.SourceKind))
            .ToArray();
        replacedCount = existingArray.Length - retained.Length;
        return ForgeEvidenceContributionMerger.Merge(retained, collectedArray, out _);
    }
}

internal static class ForgeEvidenceWatcherFilter
{
    private static readonly string[] RelevantExtensions =
    [
        ".xml", ".dll", ".json", ".yaml", ".yml", ".txt", ".png", ".dds", ".jpg", ".jpeg",
        ".wav", ".ogg", ".mp3", ".cs", ".csproj", ".manifest"
    ];

    public static bool ShouldInvalidate(string? path, bool ignoreTransientFiles)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        var fileName = Path.GetFileName(path);
        if (ignoreTransientFiles &&
            (fileName.StartsWith("~", StringComparison.Ordinal) ||
             fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
             fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
             fileName.EndsWith(".swp", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (Directory.Exists(path)) return true;
        var extension = Path.GetExtension(path);
        return string.IsNullOrEmpty(extension) || RelevantExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}

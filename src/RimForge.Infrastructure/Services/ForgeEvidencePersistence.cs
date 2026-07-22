using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RimForge.Core.Models;

namespace RimForge.Infrastructure.Services;

internal sealed record PersistedForgeEvidenceDocument(
    int SchemaVersion,
    string PlatformVersion,
    int Generation,
    string TargetRimWorldVersion,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyDictionary<string, ForgeEvidenceEntry> Entries,
    IReadOnlyList<ForgeEvidenceContribution> Contributions,
    [property: JsonPropertyName("contributorDiagnostics")]
    IReadOnlyList<ForgeEvidenceProducerDiagnostic>? ProducerDiagnostics,
    ForgeEvidenceMetrics Metrics);

public enum ForgeEvidenceStoreLoadStatus
{
    Missing,
    Loaded,
    RecoveredFromBackup,
    UnsupportedSchema,
    Corrupt
}

public sealed record ForgeEvidenceStoreLoadResult(
    ForgeEvidenceSnapshot? Snapshot,
    ForgeEvidenceStoreLoadStatus Status,
    string Path);

public interface IForgeEvidenceStore
{
    Task SaveAsync(ForgeEvidenceSnapshot snapshot, CancellationToken cancellationToken);
    Task<ForgeEvidenceStoreLoadResult> LoadAsync(CancellationToken cancellationToken);
}

public sealed class ForgeEvidenceStore : IForgeEvidenceStore
{
    private readonly string _storeRoot;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public ForgeEvidenceStore(string storeRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeRoot);
        _storeRoot = Path.GetFullPath(storeRoot);
    }

    public async Task SaveAsync(ForgeEvidenceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var path = GetSnapshotPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        var document = new PersistedForgeEvidenceDocument(
            snapshot.SchemaVersion,
            snapshot.PlatformVersion,
            snapshot.Generation,
            snapshot.TargetRimWorldVersion,
            snapshot.PublishedAtUtc,
            snapshot.Entries,
            snapshot.Contributions,
            snapshot.ProducerDiagnostics,
            snapshot.Metrics);

        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        var backupPath = path + ".bak";
        if (File.Exists(path)) File.Copy(path, backupPath, overwrite: true);
        File.Move(temporaryPath, path, overwrite: true);
    }

    public async Task<ForgeEvidenceStoreLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        var path = GetSnapshotPath();
        if (!File.Exists(path)) return new(null, ForgeEvidenceStoreLoadStatus.Missing, path);

        try
        {
            var snapshot = await LoadSnapshotAsync(path, cancellationToken).ConfigureAwait(false);
            return snapshot is null
                ? new(null, ForgeEvidenceStoreLoadStatus.UnsupportedSchema, path)
                : new(snapshot, ForgeEvidenceStoreLoadStatus.Loaded, path);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Quarantine(path);
            var backupPath = path + ".bak";
            if (!File.Exists(backupPath)) return new(null, ForgeEvidenceStoreLoadStatus.Corrupt, path);
            try
            {
                var backup = await LoadSnapshotAsync(backupPath, cancellationToken).ConfigureAwait(false);
                return backup is null
                    ? new(null, ForgeEvidenceStoreLoadStatus.UnsupportedSchema, path)
                    : new(backup, ForgeEvidenceStoreLoadStatus.RecoveredFromBackup, path);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                Quarantine(backupPath);
                return new(null, ForgeEvidenceStoreLoadStatus.Corrupt, path);
            }
        }
    }

    private static async Task<ForgeEvidenceSnapshot?> LoadSnapshotAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var document = await JsonSerializer.DeserializeAsync<PersistedForgeEvidenceDocument>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (document is null || document.SchemaVersion > ForgeEvidenceSchema.CurrentVersion) return null;
        return new ForgeEvidenceSnapshot(
            document.Generation,
            document.TargetRimWorldVersion,
            document.PublishedAtUtc,
            new Dictionary<string, ForgeEvidenceEntry>(document.Entries, StringComparer.OrdinalIgnoreCase),
            document.Metrics,
            document.SchemaVersion,
            document.PlatformVersion,
            document.Contributions,
            document.ProducerDiagnostics ?? Array.Empty<ForgeEvidenceProducerDiagnostic>());
    }

    private string GetSnapshotPath() => Path.Combine(_storeRoot, "snapshot.json");

    private static void Quarantine(string path)
    {
        try
        {
            var quarantinePath = $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(path, quarantinePath, overwrite: false);
        }
        catch
        {
            // Persistence recovery is best effort; a future refresh can recreate the snapshot.
        }
    }
}

internal static class ForgeEvidenceContributionMerger
{
    public static IReadOnlyList<ForgeEvidenceContribution> Merge(
        IEnumerable<ForgeEvidenceContribution> existing,
        IEnumerable<ForgeEvidenceContribution> incoming,
        out int mergedCount)
    {
        var byIdentity = new Dictionary<string, ForgeEvidenceContribution>(StringComparer.OrdinalIgnoreCase);
        foreach (var contribution in existing) byIdentity[Identity(contribution)] = Normalize(contribution);

        mergedCount = 0;
        foreach (var candidate in incoming)
        {
            var normalized = Normalize(candidate);
            var identity = Identity(normalized);
            if (!byIdentity.TryGetValue(identity, out var current))
            {
                byIdentity[identity] = normalized;
                continue;
            }

            mergedCount++;
            var observations = Math.Max(1, current.ObservationCount) + Math.Max(1, normalized.ObservationCount);
            var weightedConfidence = ((current.Confidence * Math.Max(1, current.ObservationCount)) +
                                      (normalized.Confidence * Math.Max(1, normalized.ObservationCount))) / observations;
            var attributes = current.EffectiveAttributes
                .Concat(normalized.EffectiveAttributes)
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

            byIdentity[identity] = normalized with
            {
                EvidenceId = string.CompareOrdinal(current.EvidenceId, normalized.EvidenceId) <= 0 ? current.EvidenceId : normalized.EvidenceId,
                Confidence = Math.Clamp(weightedConfidence, 0, 1),
                ConfidenceBand = Band(weightedConfidence),
                FirstObservedAtUtc = current.FirstObservedAtUtc <= normalized.FirstObservedAtUtc ? current.FirstObservedAtUtc : normalized.FirstObservedAtUtc,
                LastObservedAtUtc = current.LastObservedAtUtc >= normalized.LastObservedAtUtc ? current.LastObservedAtUtc : normalized.LastObservedAtUtc,
                ObservationCount = observations,
                Attributes = attributes
            };
        }

        return byIdentity.Values
            .OrderBy(value => value.SubjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.EvidenceType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.RelatedSubjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Provenance.SourceKind)
            .ThenBy(value => value.EvidenceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static ForgeEvidenceContribution Normalize(ForgeEvidenceContribution value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value with
        {
            EvidenceId = string.IsNullOrWhiteSpace(value.EvidenceId) ? CreateDeterministicId(value) : value.EvidenceId.Trim(),
            SubjectId = value.SubjectId.Trim(),
            EvidenceType = value.EvidenceType.Trim(),
            Summary = value.Summary.Trim(),
            Confidence = Math.Clamp(value.Confidence, 0, 1),
            ConfidenceBand = Band(value.Confidence),
            ObservationCount = Math.Max(1, value.ObservationCount)
        };
    }

    private static string CreateDeterministicId(ForgeEvidenceContribution value)
    {
        var identity = string.Join(
            "\n",
            value.SubjectId.Trim(),
            value.RelatedSubjectId?.Trim() ?? string.Empty,
            value.EvidenceType.Trim(),
            value.Provenance.SourceKind,
            value.Provenance.SourceId.Trim(),
            value.Provenance.SessionId?.Trim() ?? string.Empty,
            value.FirstObservedAtUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    private static string Identity(ForgeEvidenceContribution value) =>
        string.Join("|", value.SubjectId, value.RelatedSubjectId ?? string.Empty, value.EvidenceType,
            value.Provenance.SourceKind, value.Provenance.SourceId);

    private static ForgeEvidenceConfidenceBand Band(double confidence) => confidence switch
    {
        >= 0.98 => ForgeEvidenceConfidenceBand.Authoritative,
        >= 0.80 => ForgeEvidenceConfidenceBand.High,
        >= 0.55 => ForgeEvidenceConfidenceBand.Medium,
        > 0 => ForgeEvidenceConfidenceBand.Low,
        _ => ForgeEvidenceConfidenceBand.Unknown
    };
}

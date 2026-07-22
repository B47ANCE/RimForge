using System.Diagnostics;
using RimForge.Core.Models;

namespace RimForge.Infrastructure.Services;

public sealed record ForgeEvidencePipelineOptions(
    int MaximumProducerAttempts = 2,
    TimeSpan? RetryDelay = null)
{
    public TimeSpan EffectiveRetryDelay => RetryDelay ?? TimeSpan.FromMilliseconds(150);
}

public sealed record ForgeEvidencePipelineResult(
    IReadOnlyList<ForgeEvidenceContribution> Contributions,
    IReadOnlyList<ForgeEvidenceProducerDiagnostic> Diagnostics,
    int ProducersCompleted,
    int ProducersFailed,
    int DuplicateContributions,
    TimeSpan Elapsed,
    IReadOnlySet<ForgeEvidenceSourceKind> CompletedSourceKinds);

public interface IForgeEvidencePipeline
{
    Task<ForgeEvidencePipelineResult> CollectAsync(
        ForgeEvidenceCollectionContext context,
        IProgress<ForgeEvidenceProducerProgress>? progress = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> ValidateBatch(ForgeEvidenceIngestionBatch batch);
}

/// <summary>
/// Executes registered evidence producers in a stable order and returns one validated,
/// immutable transaction payload. The caller commits the payload only after collection,
/// validation, and consolidation complete successfully.
/// </summary>
public sealed class ForgeEvidencePipeline : IForgeEvidencePipeline
{
    private readonly IReadOnlyList<IForgeEvidenceProducer> _producers;
    private readonly ForgeEvidencePipelineOptions _options;

    public ForgeEvidencePipeline(
        IEnumerable<IForgeEvidenceProducer>? producers = null,
        ForgeEvidencePipelineOptions? options = null)
    {
        _producers = (producers ?? Array.Empty<IForgeEvidenceProducer>())
            .OrderBy(producer => producer.Order)
            .ThenBy(producer => producer.SourceKind)
            .ThenBy(producer => producer.ProducerId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _options = options ?? new ForgeEvidencePipelineOptions();
        if (_options.MaximumProducerAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "At least one producer attempt is required.");
    }

    public async Task<ForgeEvidencePipelineResult> CollectAsync(
        ForgeEvidenceCollectionContext context,
        IProgress<ForgeEvidenceProducerProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var stopwatch = Stopwatch.StartNew();
        var accepted = new List<ForgeEvidenceContribution>();
        var diagnostics = new List<ForgeEvidenceProducerDiagnostic>();
        var completed = 0;
        var failed = 0;
        var completedSourceKinds = new HashSet<ForgeEvidenceSourceKind>();

        foreach (var producer in _producers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ForgeEvidenceProducerProgress(
                producer.ProducerId,
                producer.SourceKind,
                ForgeEvidenceCollectionStage.Preparing,
                completed,
                _producers.Count));

            ForgeEvidenceProducerResult? result = null;
            Exception? lastFailure = null;
            for (var attempt = 1; attempt <= _options.MaximumProducerAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    result = await producer.CollectAsync(context, progress, cancellationToken).ConfigureAwait(false);
                    ValidateProducerResult(producer, result);
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < _options.MaximumProducerAttempts && IsTransient(ex))
                {
                    lastFailure = ex;
                    await Task.Delay(_options.EffectiveRetryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastFailure = ex;
                    break;
                }
            }

            if (result is null)
            {
                failed++;
                diagnostics.Add(new ForgeEvidenceProducerDiagnostic(
                    producer.ProducerId,
                    producer.SourceKind,
                    "RF-EVIDENCE-PRODUCER-FAILED",
                    lastFailure?.Message ?? "The producer failed without an exception detail.",
                    lastFailure is not null && IsTransient(lastFailure),
                    DateTimeOffset.UtcNow));
                continue;
            }

            completed++;
            completedSourceKinds.Add(producer.SourceKind);
            diagnostics.AddRange(result.Diagnostics);
            accepted.AddRange(result.Contributions);
            progress?.Report(new ForgeEvidenceProducerProgress(
                producer.ProducerId,
                producer.SourceKind,
                ForgeEvidenceCollectionStage.Completed,
                completed,
                _producers.Count,
                $"{result.Contributions.Count} contribution(s)"));
        }

        progress?.Report(new ForgeEvidenceProducerProgress(
            "forge-evidence-pipeline",
            ForgeEvidenceSourceKind.StaticAnalysis,
            ForgeEvidenceCollectionStage.Validating,
            completed,
            _producers.Count));

        var validationErrors = ForgeEvidenceBatchValidator.ValidateContributions(accepted);
        if (validationErrors.Count > 0)
            throw new ForgeEvidenceValidationException(validationErrors);

        var consolidated = ForgeEvidenceContributionMerger.Merge(
            Array.Empty<ForgeEvidenceContribution>(),
            accepted,
            out var duplicateCount);

        stopwatch.Stop();
        return new ForgeEvidencePipelineResult(
            consolidated,
            diagnostics
                .OrderBy(item => item.SourceKind)
                .ThenBy(item => item.ProducerId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Code, StringComparer.Ordinal)
                .ToArray(),
            completed,
            failed,
            duplicateCount,
            stopwatch.Elapsed,
            completedSourceKinds);
    }

    public IReadOnlyList<string> ValidateBatch(ForgeEvidenceIngestionBatch batch) =>
        ForgeEvidenceBatchValidator.Validate(batch);

    private static void ValidateProducerResult(
        IForgeEvidenceProducer producer,
        ForgeEvidenceProducerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!string.Equals(producer.ProducerId, result.ProducerId, StringComparison.Ordinal))
            throw new InvalidDataException($"Producer '{producer.ProducerId}' returned a mismatched producer ID.");
        if (producer.SourceKind != result.SourceKind)
            throw new InvalidDataException($"Producer '{producer.ProducerId}' returned a mismatched source kind.");
        if (result.Contributions is null)
            throw new InvalidDataException($"Producer '{producer.ProducerId}' returned a null contribution collection.");
        if (result.Contributions.Any(item => item is null || item.Provenance is null || item.Provenance.SourceKind != producer.SourceKind))
            throw new InvalidDataException($"Producer '{producer.ProducerId}' returned evidence for another source kind or without provenance.");
    }

    private static bool IsTransient(Exception exception) => exception is
        IOException or TimeoutException;
}

public sealed class ForgeEvidenceValidationException : Exception
{
    public ForgeEvidenceValidationException(IReadOnlyList<string> validationErrors)
        : base(string.Join(Environment.NewLine, validationErrors))
    {
        ValidationErrors = validationErrors;
    }

    public IReadOnlyList<string> ValidationErrors { get; }
}

internal static class ForgeEvidenceBatchValidator
{
    public static IReadOnlyList<string> Validate(ForgeEvidenceIngestionBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(batch.BatchId)) errors.Add("BatchId is required.");
        if (batch.SchemaVersion != ForgeEvidenceSchema.CurrentVersion)
            errors.Add($"Unsupported evidence schema version {batch.SchemaVersion}.");
        if (batch.Contributions is null)
        {
            errors.Add("Contributions are required.");
            return errors;
        }

        errors.AddRange(ValidateContributions(batch.Contributions, batch.SourceKind));
        return errors;
    }

    public static IReadOnlyList<string> ValidateContributions(
        IReadOnlyList<ForgeEvidenceContribution> contributions,
        ForgeEvidenceSourceKind? requiredSourceKind = null)
    {
        var errors = new List<string>();
        for (var index = 0; index < contributions.Count; index++)
        {
            var value = contributions[index];
            if (value is null)
            {
                errors.Add($"Contribution {index} is null.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(value.SubjectId)) errors.Add($"Contribution {index} requires SubjectId.");
            if (string.IsNullOrWhiteSpace(value.EvidenceType)) errors.Add($"Contribution {index} requires EvidenceType.");
            if (string.IsNullOrWhiteSpace(value.Summary)) errors.Add($"Contribution {index} requires Summary.");
            if (value.Provenance is null)
            {
                errors.Add($"Contribution {index} requires provenance.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(value.Provenance.SourceId)) errors.Add($"Contribution {index} requires a provenance SourceId.");
            if (value.Confidence is < 0 or > 1) errors.Add($"Contribution {index} confidence must be between 0 and 1.");
            if (value.ObservationCount < 1) errors.Add($"Contribution {index} observation count must be positive.");
            if (value.LastObservedAtUtc < value.FirstObservedAtUtc)
                errors.Add($"Contribution {index} has an invalid observation window.");
            if (requiredSourceKind.HasValue && value.Provenance.SourceKind != requiredSourceKind.Value)
                errors.Add($"Contribution {index} source kind does not match its batch.");
        }
        return errors;
    }
}

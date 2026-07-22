using System.Text.Json;
using RimForge.Analysis.Models;

namespace RimForge.Analysis.Services;

public interface IRepairTransactionExecutor
{
    Task<RepairExecutionResult> ExecuteAsync(
        RepairPlan plan,
        Func<CancellationToken, Task<RepairMutationResult>> apply,
        Func<RepairTransactionJournal, CancellationToken, Task<RepairMutationResult>> rollback,
        CancellationToken cancellationToken = default,
        bool userConfirmed = false);
    IReadOnlyList<RepairTransactionJournal> DiscoverInterrupted();
    Task<RepairExecutionResult> RecoverAsync(
        string transactionId,
        Func<RepairTransactionJournal, CancellationToken, Task<RepairMutationResult>> rollback,
        CancellationToken cancellationToken = default);
}

public sealed class RepairTransactionExecutor : IRepairTransactionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _root;
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    public RepairTransactionExecutor(string root) => _root = string.IsNullOrWhiteSpace(root)
        ? throw new ArgumentException("A repair transaction root is required.", nameof(root))
        : Path.GetFullPath(root);

    public async Task<RepairExecutionResult> ExecuteAsync(
        RepairPlan plan,
        Func<CancellationToken, Task<RepairMutationResult>> apply,
        Func<RepairTransactionJournal, CancellationToken, Task<RepairMutationResult>> rollback,
        CancellationToken cancellationToken = default,
        bool userConfirmed = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(rollback);
        if (!plan.CanExecute) throw new InvalidOperationException("Only a ready repair plan with satisfied preconditions can execute.");
        if (!string.Equals(plan.Certification.PolicyId, RepairSafetyPolicy.PolicyId, StringComparison.Ordinal))
            throw new InvalidOperationException("The repair plan was not certified by the active safety policy.");
        if (plan.ExecutionMode == RepairExecutionMode.Automatic && !plan.Certification.AutomaticExecutionAllowlisted)
            throw new InvalidOperationException("Automatic repair execution is not allowlisted by the active safety policy.");
        if (plan.Certification.RuntimeEvidenceAdvisoryOnly)
            throw new InvalidOperationException("Runtime evidence cannot authorize a repair mutation.");
        if (plan.Certification.RequiresExplicitConfirmation && !userConfirmed)
            throw new InvalidOperationException("This repair requires explicit user confirmation.");

        await _executionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var journal = new RepairTransactionJournal(
                $"repair-{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}", plan.Id, plan.IssueId, plan.DeterministicKey,
                now, now, RepairTransactionState.Planned,
                [new(now, RepairTransactionState.Planned, $"Repair transaction certified by {plan.Certification.PolicyId}.", plan.Certification.Reason)],
                CertificationPolicyId: plan.Certification.PolicyId,
                SafetyClass: plan.SafetyClass);
            journal = await TransitionAsync(journal, RepairTransactionState.Executing, "Atomic repair operation started.", cancellationToken: cancellationToken).ConfigureAwait(false);
            try
            {
                var mutation = await apply(cancellationToken).ConfigureAwait(false);
                if (mutation.Success)
                {
                    journal = await TransitionAsync(journal, RepairTransactionState.Committed, mutation.Message,
                        mutation.TechnicalDetail, mutation.BackupPath, mutation.Message, CancellationToken.None).ConfigureAwait(false);
                    return new(true, journal.State, mutation.Message, journal);
                }
                return await RollBackAsync(journal, mutation.Message, rollback, cancelled: false,
                    technicalDetail: mutation.TechnicalDetail, backupPath: mutation.BackupPath).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return await RollBackAsync(journal, "Repair execution was cancelled.", rollback, cancelled: true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return await RollBackAsync(journal, "Repair execution failed.", rollback, cancelled: false, ex.ToString()).ConfigureAwait(false);
            }
        }
        finally
        {
            _executionGate.Release();
        }
    }

    public IReadOnlyList<RepairTransactionJournal> DiscoverInterrupted()
    {
        _executionGate.Wait();
        try
        {
            if (!Directory.Exists(_root)) return Array.Empty<RepairTransactionJournal>();
            var interrupted = new List<RepairTransactionJournal>();
            foreach (var path in Directory.EnumerateFiles(_root, "repair-*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var journal = TryLoad(path);
                if (journal is null || journal.IsTerminal) continue;
                if (journal.State != RepairTransactionState.RecoveryRequired)
                {
                    journal = Transition(journal, RepairTransactionState.RecoveryRequired,
                        "An interrupted repair was detected and requires rollback recovery.");
                    WriteAtomic(path, journal);
                }
                interrupted.Add(journal);
            }
            return interrupted.OrderBy(item => item.StartedAt).ToArray();
        }
        finally { _executionGate.Release(); }
    }

    public async Task<RepairExecutionResult> RecoverAsync(
        string transactionId,
        Func<RepairTransactionJournal, CancellationToken, Task<RepairMutationResult>> rollback,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionId)) throw new ArgumentException("A transaction ID is required.", nameof(transactionId));
        ArgumentNullException.ThrowIfNull(rollback);
        await _executionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = JournalPath(transactionId);
            var journal = TryLoad(path) ?? throw new FileNotFoundException("Repair transaction journal was not found.", path);
            if (journal.IsTerminal) return new(journal.State == RepairTransactionState.Committed, journal.State, journal.Outcome ?? "Transaction is already complete.", journal);
            cancellationToken.ThrowIfCancellationRequested();
            return await RollBackAsync(journal, "Interrupted repair recovery requested.", rollback, cancelled: false).ConfigureAwait(false);
        }
        finally { _executionGate.Release(); }
    }

    private async Task<RepairExecutionResult> RollBackAsync(
        RepairTransactionJournal journal,
        string reason,
        Func<RepairTransactionJournal, CancellationToken, Task<RepairMutationResult>> rollback,
        bool cancelled,
        string? technicalDetail = null,
        string? backupPath = null)
    {
        journal = await TransitionAsync(journal, RepairTransactionState.RollingBack, reason, technicalDetail,
            backupPath, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        try
        {
            var result = await rollback(journal, CancellationToken.None).ConfigureAwait(false);
            if (!result.Success)
            {
                journal = await TransitionAsync(journal, RepairTransactionState.RecoveryRequired,
                    result.Message, result.TechnicalDetail, result.BackupPath, result.Message, CancellationToken.None).ConfigureAwait(false);
                return new(false, journal.State, result.Message, journal);
            }
            var finalState = cancelled ? RepairTransactionState.Cancelled : RepairTransactionState.RolledBack;
            journal = await TransitionAsync(journal, finalState, result.Message, result.TechnicalDetail,
                result.BackupPath, result.Message, CancellationToken.None).ConfigureAwait(false);
            return new(false, journal.State, result.Message, journal);
        }
        catch (Exception ex)
        {
            journal = await TransitionAsync(journal, RepairTransactionState.RecoveryRequired,
                "Rollback failed; manual recovery is required.", ex.ToString(), outcome: ex.Message,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
            return new(false, journal.State, journal.Outcome ?? "Rollback failed.", journal);
        }
    }

    private async Task<RepairTransactionJournal> TransitionAsync(
        RepairTransactionJournal journal,
        RepairTransactionState state,
        string message,
        string? technicalDetail = null,
        string? backupPath = null,
        string? outcome = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var updated = Transition(journal, state, message, technicalDetail, backupPath, outcome);
        await WriteAtomicAsync(JournalPath(journal.Id), updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private static RepairTransactionJournal Transition(
        RepairTransactionJournal journal,
        RepairTransactionState state,
        string message,
        string? technicalDetail = null,
        string? backupPath = null,
        string? outcome = null)
    {
        var now = DateTimeOffset.UtcNow;
        return journal with
        {
            UpdatedAt = now,
            State = state,
            BackupPath = backupPath ?? journal.BackupPath,
            Outcome = outcome ?? journal.Outcome,
            AuditTrail = journal.AuditTrail.Append(new RepairAuditEvent(now, state, message, technicalDetail)).ToArray()
        };
    }

    private string JournalPath(string id) => Path.Combine(_root, id + ".json");

    private static RepairTransactionJournal? TryLoad(string path)
    {
        try { return JsonSerializer.Deserialize<RepairTransactionJournal>(File.ReadAllText(path), JsonOptions); }
        catch { return null; }
    }

    private static async Task WriteAtomicAsync(string path, RepairTransactionJournal journal, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(journal, JsonOptions), cancellationToken).ConfigureAwait(false);
        File.Move(temporary, path, true);
    }

    private static void WriteAtomic(string path, RepairTransactionJournal journal)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(journal, JsonOptions));
        File.Move(temporary, path, true);
    }
}

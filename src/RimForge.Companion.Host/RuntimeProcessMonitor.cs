using System.Diagnostics;

namespace RimForge.Companion.Host;

public sealed class RuntimeProcessMonitor(int processId)
{
    public int ProcessId { get; } = processId;
    public bool IsRunning { get; private set; }
    public event EventHandler<bool>? RunningChanged;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var running = IsProcessRunning();
            if (running != IsRunning)
            {
                IsRunning = running;
                RunningChanged?.Invoke(this, running);
            }
            if (!running) return;
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool IsProcessRunning()
    {
        try { return !Process.GetProcessById(ProcessId).HasExited; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}

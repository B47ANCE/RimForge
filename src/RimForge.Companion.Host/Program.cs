using RimForge.Companion.Host;

var options = CompanionHostOptions.Parse(args);
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

await using var host = new CompanionHost(options);
host.HealthChanged += (_, health) =>
    Console.WriteLine($"[{health.ObservedUtc:O}] {health.Status}: {health.Message}");

try
{
    await host.RunAsync(shutdown.Token);
    return 0;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

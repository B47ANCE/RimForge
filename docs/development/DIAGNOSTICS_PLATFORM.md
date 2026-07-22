# Shared Diagnostics Platform

Epic A provides one diagnostics contract for the RimForge client, Forge Sessions, and Companion Host control.

## Contracts

- `DiagnosticEvent` is the structured event envelope.
- `RuntimeHealth` and `HealthStatus` describe the current component condition.
- `PerformanceMeasurement` and `PerformanceTimer` capture elapsed operations.
- `ILogSink` receives structured events.
- `ISessionLog` owns the active Forge Session log.
- `IDiagnosticService` publishes events, health, recent history, and timing scopes.

## Persistence

Global diagnostics are written as JSON Lines beneath the canonical diagnostics root:

```text
Diagnostics/rimforge-diagnostics.jsonl
```

Forge-correlated diagnostics are also written beneath the session:

```text
Sessions/<forge-session-id>/session-log.jsonl
```

Writes use one synchronized append stream per sink and remain readable by diagnostic tooling while RimForge is running. The in-memory projection retains the most recent 500 events.

## Integration

The existing `RimForgeLogger` is bridged into `DiagnosticService`, preserving existing instrumentation while moving it onto the durable pipeline. Forge Session lifecycle/progress and Companion Host process/health transitions include session correlation and structured properties. Overall health is projected into the existing main-client Forge status surface.

Performance scopes emit an event on disposal with invariant elapsed milliseconds, operation identity, and optional session identity. Diagnostics failures are isolated from product workflows.

# Forge Session Foundation

Epic A establishes `IForgeSessionManager` as the lifecycle boundary for every Forge execution.

## Contract

Each `ForgeSession` contains:

- a stable `ForgeSessionId`
- explicit starting, running, cancelling, completed, failed, or cancelled state
- start and completion timestamps
- normalized workspace and optional profile name
- target RimWorld version and installed-mod count
- runtime companion status
- current stage, narrative, progress, and failure detail

The manager owns a cancellation token, rejects overlapping sessions, and publishes the existing `ForgeSessionSnapshot` and application event so current WPF consumers remain compatible.

## Persistence and recovery

Session records are written atomically beneath `RimForgePathLayout.SessionsRoot`:

```text
Sessions/
  current.json
  <session-id>/
    session.json
```

On startup, the latest terminal session is restored. A persisted active session indicates an interrupted process and is converted to a failed `Interrupted` record rather than silently discarded.

## Integration

`MainWindow.RunAudit_Click` creates the session before evidence generation and supplies the workspace, profile, game version, and library size. Progress continues through the established event projection. User cancellation requests both the shared background task and session-owned token. Application shutdown finalizes active sessions and disposes manager resources.

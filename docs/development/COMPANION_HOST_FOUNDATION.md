# Companion Host Foundation

The Companion Host is a lifecycle-managed background process owned by RimForge. It has no standalone user interface or visible console window; status and failures are projected into the main client.

## Components

- `CompanionHost` coordinates component lifetime and health.
- `IpcServer` accepts one local, current-user named-pipe Agent connection and validates every envelope.
- `SessionBridge` appends accepted envelopes to the Forge session's durable JSONL stream.
- `PlayerLogWatcher` tails Player.log without blocking RimWorld's writer.
- `RuntimeProcessMonitor` observes the selected RimWorld process and ends the host lifetime after process exit.
- `CompanionHostService` is the RimForge-side hidden-process controller.

## UI integration

The host is started with `CreateNoWindow` and hidden window style. Standard output and error are drained in the background so the child process cannot block on full pipes. `CompanionHostProcessSnapshot` is projected through `MainWindow.CompanionHostStatusText` in the existing global Forge status surface.

There is intentionally no second tray icon, taskbar window, or console UI. Host lifecycle remains subordinate to RimForge application lifecycle and Forge Session identity.

## Session storage

Accepted runtime envelopes are written beneath the configured state root:

```text
Sessions/<forge-session-id>/runtime-envelopes.jsonl
```

Malformed, oversized, unsupported-version, or incomplete envelopes are rejected before persistence. The health snapshot tracks accepted and rejected envelope counts, IPC/listener state, Agent connection state, and RimWorld process state.

# Epic A Pass 1 Infrastructure

Epic A Pass 1 is complete. The client now has explicit ownership boundaries for foreground operations, hosted service loops, platform discovery, workspace paths, Forge Sessions, Companion control, and diagnostics.

## Background execution

`IBackgroundTaskService` remains the single foreground-operation lane used by the main client for visible progress and cancellation. `IHostedBackgroundWorkService` owns long-lived service loops that must run concurrently with that lane. Hosted work is keyed, rejects duplicate starts, publishes lifecycle snapshots, captures failures, and is cancelled and awaited during composition shutdown.

The runtime sensor pipe is the first hosted service. It starts only from the client startup pipeline; application composition no longer fire-and-forgets evidence loading or listener startup.

Event debounce operations remain locally owned by their watcher or scheduler because they are short, replaceable delays rather than application-lifetime services. Their cancellation sources and exceptions remain contained by those owners.

## Path ownership

`RimForgePathLayout` resolves the repository and every mutable client workspace directory. `IPlatformDiscoveryService` owns machine-specific Steam and RimWorld discovery. Features receive projected paths:

- Texture exports use `ExportsRoot`; conversion scratch files use `TempRoot`.
- Curated databases resolve from the centralized repository layout and packaged application candidates.
- Companion Host requires an explicit state root from its controller.
- Steam metadata requires an explicit cache root from composition.
- The runtime Agent isolates its framework-compatible LocalAppData discovery in `AgentPathLayout`.

No feature or Forge Session fallback derives semantic state from the process working directory.

## Verification

- `RimForge.ExecutionTests` covers hosted-work start, duplicate prevention, cancellation, and published state.
- `tests/EpicAInfrastructureBoundaries-Test.ps1` prevents the runtime sensor from regaining private task ownership and prevents path discovery from leaking back into migrated features.
- A clean solution build must complete with zero warnings and zero errors.

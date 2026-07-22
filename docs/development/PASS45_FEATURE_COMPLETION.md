# Pass 45 — Feature Completion

## Scope

Pass 45 completes application workflows and stabilizes execution architecture. It deliberately excludes Chrome+, performance-only optimization, companion-mod integration, and release engineering.

## Recovered execution boundary

`src/RimForge.App/MainWindow.FeatureTasks.cs` is the recovered common feature boundary. Its generic and non-generic helpers validate task identity, hand work to `IBackgroundTaskService`, write lifecycle activity, report standard failures, and provide cooperative cancellation. Explicit user work can pause optional shared intelligence without leaving an unobserved task.

`BackgroundTaskService` owns the only feature worker scheduler. It creates one linked token, publishes Running, ignores late progress after cancellation starts, forces cancellation when a token was requested before an operation returned, then publishes Completed, Cancelled, or Failed and releases its state.

## Migrated workflows

- Native library discovery and library projection
- Incremental Forge Evidence and native Forge analysis
- Optional shared intelligence refresh
- Texture discovery, DDS validation, conversion, and revert
- Profile load/create/rename/duplicate/import/export/delete/restore/activate
- Active load-order save and automatic issue-repair persistence
- Steam discovery, Inspector metadata refresh, and Settings load/save
- Profile-scoped workspace analysis without UI-thread sync-over-async
- Player.log initialization and paged history loading
- Launch installation discovery, recoverable activation, and game start

Player.log streaming and file-watcher debounce remain scoped application services because they are continuous subscriptions rather than finite exclusive operations.

## Progress contract

Every migrated workflow can publish:

- percentage or indeterminate state;
- current operation;
- technical detail;
- discovery detail;
- elapsed time;
- current file;
- processed count; and
- total count.

The shared snapshot is projected into command status and the global feature bar. Forge and Texture Tools retain their focused presentations while consuming the same lifecycle.

## Texture Tools completion

The active profile is the authoritative input scope. Analysis discovers supported files, validates DDS headers, and adds queue entries in dispatcher batches. Balanced and Quality presets default to BC7; BC3 remains an explicit compatibility option and BC1 powers the Performance preset. Presets also update maximum-size, alpha, and skip-current settings. Conversion never changes sources. Successful outputs are recorded through atomic manifest replacement, corrupt manifests are quarantined, and revert deletes only unmodified manifest-owned output. Cancellation kills texconv and propagates normally.

Codec resolution is explicit and deterministic: BC7 selects `BC7_UNORM`, BC3/DXT5 selects `BC3_UNORM`, and BC1/DXT1 selects `BC1_UNORM`. An unqualified DDS request falls back to BC7 rather than the legacy BC3 path.

## ForgeView relationship completion

Graph edges now leave and enter the nearest side of each node instead of assuming fixed left/right sockets. Required, optional, ordering, patch-target, and incompatible relationships have distinct stroke patterns, with directional arrowheads appropriate to their semantics. When one endpoint is selected, orange identifies an edge from the selected dependent to its dependency and cyan identifies an incoming dependent; incompatibilities remain red and bidirectional.

Core-to-normal-mod edges are hidden by `ForgeGraphPresentationPolicy` because the relationship is implicit. The policy keeps official DLC attached to Core and does not suppress dependencies declared by DLC. It changes presentation and focused counts only; the authoritative evidence graph is unchanged.

## Mod Sorter group operations

Extended selection no longer collapses before drag initiation. Ctrl/Shift selection, source-order payload construction, package-ID deduplication, group activation/reordering/deactivation, dependency assistance, and orphan cleanup operate together. Load-order snapshots retain the exact active/inactive item-reference arrays so exceptions restore every item—even duplicate package-ID records or an item temporarily between collections. Successful group moves are one undo unit and remain selected in the destination list.

## Error recovery

All feature operations terminate in a published lifecycle state. Common failure notification links to the Activity Feed, and guarded UI commands consume already-reported cancellation/failure exceptions so they cannot escape through WPF `async void` event dispatch. UI busy state is cleared in `finally` blocks, cancellation is token-based, and the next task may start after any terminal state. Dependency decisions use RimForge dialogs; no feature terminates threads directly.

## Validation

`Tests/Pass45TextureExecutionIntegration-Test.ps1` verifies the helper contract, lifecycle states, common progress fields, Texture Tool migration, preset wiring, codec selection, texconv cancellation, revert progress, the absence of isolated `Task.Run`, and the absence of direct MainWindow execution-service calls. `Tests/Pass45FeatureCompletionAdditions-Test.ps1` validates BC7 defaults, graph port/direction/pattern policy, Core/DLC presentation, and atomic multi-select drag contracts. `Tests/BackgroundTaskLifecycle-Test.ps1` runs a package-free .NET harness that exercises completion, cancellation, late-progress rejection, elapsed time, progress detail retention, exception capture, and recovery after failure.

The source/package validation record is maintained in `docs/development/PASS45_VALIDATION_RESULTS.md`.

Required Windows acceptance commands:

```powershell
dotnet clean .\RimForge.sln
dotnet restore .\RimForge.sln
dotnet build .\RimForge.sln --configuration Debug
powershell -ExecutionPolicy Bypass -File .\Tests\Pass45TextureExecutionIntegration-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\Pass45FeatureCompletionAdditions-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\BackgroundTaskLifecycle-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\Commit11Completion-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\Commit12Completion-Test.ps1
.\src\RimForge.App\bin\Debug\net10.0-windows\RimForge.exe --logging
```

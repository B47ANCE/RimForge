# RimForge

RimForge Client is the native Windows engineering platform for discovering, diagnosing, repairing, sorting, and managing RimWorld mod libraries. It contains the .NET 10 WPF client, local Companion Host/bootstrapper, protocol authority, evidence ingestion, analysis, repair, and visualization systems. The RimWorld-loaded mod lives in the sibling `RimForge.Companion` repository.

RimForge is in active alpha development. The canonical version is stored in [`VERSION`](VERSION) and applied to every project by [`Directory.Build.props`](Directory.Build.props).

Global search also acts as a command palette backed by RimForge's canonical workspace/action catalog. In Mod Sorter, extended selections can be enabled or disabled as one previewed, undoable profile operation.

## Repository layout

| Path | Responsibility |
|---|---|
| `src/RimForge.App` | WPF application, composition root, and feature orchestration |
| `src/RimForge.Core` | Domain models and service contracts |
| `src/RimForge.Infrastructure` | Discovery, persistence, runtime ingestion, and external services |
| `src/RimForge.Analysis` | Analysis, dependency intelligence, sorting, and repair planning |
| `src/RimForge.UI` | Shared WPF controls, themes, and presentation components |
| `src/RimForge.Protocol` | Shared desktop/host/mod wire contracts (`netstandard2.0`) |
| `src/RimForge.Companion.Host` | Local Companion Host and IPC endpoint |
| `tests` | Client .NET tests, executable checks, and PowerShell contract gates |
| `build` | Client build tooling |
| `docs` | Client architecture, development, and protocol documentation |
| `../RimForge.Companion` | RimWorld-loaded companion mod and package skeleton |
| `../RimForge.Companion.TestSuite` | Controlled runtime fixtures and Companion certification |

Generated `bin`, `obj`, `artifacts`, reports, runtime caches, and local configuration are ignored and must not be committed.

## Requirements

- Windows 10 or 11
- .NET 10 SDK
- PowerShell 5.1 or newer

## Build and validate

```powershell
.\Build-Test-All.ps1
```

The standard build validates the desktop application, Companion Host, shared protocol, .NET checks, and client repository contract tests. Companion runtime validation is owned by `../RimForge.Companion.TestSuite`.

Build only the desktop solution:

```powershell
dotnet restore .\RimForge.sln
dotnet build .\RimForge.sln -c Debug
```

Launch after a Debug build with `Launch-RimForge.bat`.

## Architecture and safety

The accepted runtime flow is:

`RimForge App -> Companion Host -> named-pipe session -> RimWorld Agent -> runtime evidence -> analysis -> repair/UI`

The companion mod is diagnostics-only: it must fail closed, avoid gameplay mutations, avoid network listeners, and never write to saves. Runtime state belongs beneath the application data directory, never in the repository.

Long-lived client services run through the shared hosted-work coordinator, while user-visible operations run through the foreground background-task service. Machine and workspace paths are resolved by the platform discovery and `RimForgePathLayout` boundaries; features consume those paths instead of probing the host independently.

All desktop, dependency, community, runtime, and compatibility observations enter Forge Evidence through `IForgeEvidenceProducer`. Immutable generations are persisted by `IForgeEvidenceStore`, published through `IForgeEvidenceBus`, and consumed by the client and ForgeView without parallel evidence projections.

Startup performs platform health validation, records recoverable active-run state, and captures the protected-state boundary. Updates fail closed unless their channel key is pinned, their manifest signature and package hash verify, and their installation root cannot overlap settings, profiles, output, caches, sessions, or diagnostics.

The Analysis Engine accepts an explicit full-library request plus unified Forge Evidence and returns a deterministic snapshot with static and runtime findings, complete structured diagnostics, typed stage progress, per-stage timing, scope metrics, a reproducible input fingerprint, and canonical full-library/per-mod explanations. Unchanged runs reuse a bounded in-memory result cache with explicit refresh, bypass, provenance, and invalidation controls. Active profile order influences scoped findings and proposed ordering without excluding inactive installed mods from dependency analysis.

See [Architecture](ARCHITECTURE.md), [Runtime Companion](RUNTIME_COMPANION.md), [Roadmap](ROADMAP.md), [Contributing](CONTRIBUTING.md), and the [documentation index](docs/README.md).

## Project status

RimForge is an independent community project and is not affiliated with Ludeon Studios, Valve, RimPy, RimSort, or Steam Workshop projects with similar names.

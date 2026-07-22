# RimForge Architecture

## System boundary

RimForge is one product split across three repositories and three runtime processes:

1. **RimForge App** — WPF workstation and composition root.
2. **Companion Host** — desktop-owned session, IPC, process, log, and persistence host.
3. **RimForge Agent** — a normal RimWorld mod owned by `../RimForge.Companion` that emits diagnostics and performance evidence.

The shared `RimForge.Protocol` assembly is the only wire-contract authority. Protocol types must never be copied into another project.

## Dependency direction

```text
RimForge.App -> RimForge.UI
             -> RimForge.Infrastructure -> RimForge.Protocol
             -> RimForge.Analysis
             -> RimForge.Core

RimForge.Companion.Host -> RimForge.Protocol
../RimForge.Companion/RimForge.Agent -> RimForge.Protocol
```

`RimForge.Core` has no infrastructure or UI dependencies. `RimForge.UI` does not reference the application. Long-running desktop work uses the shared background-task framework.

## Runtime flow

```text
Create Forge Session
  -> launch Companion Host
  -> open local named-pipe endpoint
  -> launch RimWorld
  -> Agent connects and identifies the session
  -> runtime/log evidence is buffered and persisted
  -> Forge Evidence ingestion
  -> analysis and repair planning
  -> UI projection
```

The Companion Host owns lifecycle, buffering, process monitoring, health, and recovery. The Agent owns RimWorld/Harmony observation only. This separation is an accepted architecture decision.

## Persistence

Repository files are immutable product inputs. User settings, logs, reports, caches, sessions, crash data, and generated evidence resolve through centralized path services beneath the user application-data location. Persistent writes must be atomic when practical and must preserve user data during upgrades or rollback.

## Repository policy

- `src` contains production source only.
- `tests` contains executable validation code and scripts.
- Companion mod source and package assets belong only to `../RimForge.Companion`.
- Runtime harnesses and controlled fixtures belong only to `../RimForge.Companion.TestSuite`.
- `build` contains Client build entry points only.
- Historical build outputs, hotfix backups, copied projects, and duplicate contracts are prohibited.

Detailed references are indexed in [`docs/README.md`](docs/README.md).

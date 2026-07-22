# Repository Boundaries

RimForge uses three sibling repositories under the canonical workspace root.

## RimForge.Client

Owns the WPF application, domain and analysis engines, infrastructure, evidence ingestion and persistence, protocol authority, Companion Host/bootstrapper, reports, and every feature that views or uses Companion data.

It must not contain the RimWorld-loaded Agent, distributable Companion mod assets, runtime fixture mods, or the runtime test harness.

## RimForge.Companion

Owns only code and assets that execute inside or directly touch RimWorld: `RimForge.Agent`, the `RimForge.Runtime` mod skeleton, and Companion packaging/install tooling.

It consumes the versioned protocol authority from the sibling Client checkout. It does not own the desktop Companion Host.

## RimForge.Companion.TestSuite

Owns protocol compatibility tests, the RimWorld runtime harness, fixture mods, expected evidence, development fixture installation, and runtime acceptance documentation.

The suite is developer-only and does not ship with either production repository.

`tests/RepositoryBoundary-Test.ps1` prevents these ownership boundaries from regressing.

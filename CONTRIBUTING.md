# Contributing to RimForge

RimForge is a Windows WPF/.NET project focused on safe, explainable RimWorld modpack engineering.

## Before contributing

Read:

- [ROADMAP.md](ROADMAP.md)
- [ARCHITECTURE.md](ARCHITECTURE.md)
- [ENGINEERING_PHILOSOPHY.md](ENGINEERING_PHILOSOPHY.md)
- [DESIGN_SYSTEM.md](DESIGN_SYSTEM.md) for UI work
- [SORTING_ENGINE.md](SORTING_ENGINE.md) for ordering logic
- [DATABASES.md](DATABASES.md) for curated rules

Open an issue or design discussion before large architecture, persistence, schema, branding, or user-data changes.

## Development environment

- Windows 10 or 11
- .NET 10 SDK
- PowerShell 5.1 or newer
- Visual Studio with .NET desktop development, Rider, or VS Code with suitable C# tooling
- Optional RimWorld installation and test mod library for integration testing
- Optional DirectXTex/texconv for DDS workflows

## Build and test

```powershell
dotnet restore .\RimForge.sln
dotnet build .\RimForge.sln --configuration Debug
dotnet test .\RimForge.sln --configuration Debug
powershell -ExecutionPolicy Bypass -File .\Tests\RepositoryHygiene-Test.ps1
```

Run targeted PowerShell regression gates relevant to the changed subsystem. UI and integration changes also require manual Windows validation.

## Contribution rules

- Preserve the dependency direction defined in `ARCHITECTURE.md`.
- Do not create independent evidence, sorting, profile, search, or background-task pipelines.
- Do not block the UI thread with filesystem, parsing, analysis, or process work.
- Route finite long-running operations through the shared background-task service.
- Use canonical runtime paths; do not write generated state into the repository.
- Use atomic persistence for user-owned state.
- Add executable tests for behavior changes. Source-text tests may supplement but should not replace behavioral coverage.
- Keep changes focused and avoid unrelated formatting churn.
- Update documentation and changelog entries when contracts or user-visible behavior change.

## UI contributions

- Reuse design tokens, typography, controls, and dialog infrastructure.
- Never introduce black text into the dark UI.
- Preserve keyboard focus, high-DPI behavior, virtualization, and empty/loading/error states.
- Use approved production assets only. The canonical compact star badge is immutable; the retained anvil is feature artwork, not the application identity.
- Avoid direct cross-feature control manipulation; use shared selection, navigation, search, and events.

## Curated database contributions

Every rule needs stable identity, target version, confidence, provenance, and rationale. Hard rules require reproducible evidence. Recommendations must not be encoded as dependencies. Run schema, contradiction, cycle, and replacement-loop validation.

## Pull requests

A pull request should include:

- Problem and user impact
- Technical approach
- Files/subsystems changed
- Tests added or run
- Manual validation performed
- Persistence or migration impact
- Screenshots for visible UI changes
- Follow-up work intentionally excluded

## Commit hygiene

Do not commit `bin`, `obj`, logs, caches, reports, local settings, generated diagnostics, patch backups, or machine-specific paths. Keep commits reviewable and describe the behavior changed rather than only the files touched.

# Repository Assessment and Refinement

## Scope

This pre-Pass-40 epic evaluates the uploaded alpha.39 root repository as the authoritative project baseline. It intentionally does not begin Pass 40 feature implementation.

## Baseline assessment

### Strengths

- The solution is separated into Core, Analysis, Infrastructure, UI, PowerShell bridge, and application-host projects.
- Completed capabilities are protected by a broad PowerShell acceptance-test suite, including dedicated Pass 39 gates.
- Cross-feature infrastructure already exists for commands, undo, background tasks, notifications, typed events, search, profiles, dependency intelligence, and ForgeView.
- Product, architecture, engineering, design-system, roadmap, release, and contribution documents are maintained at the repository root.
- Version and compiler policy are centralized through `Directory.Build.props`.

### Refinement findings

1. The uploaded baseline included generated `bin` and `obj` directories for five projects.
2. The uploaded baseline included `.patch-backups`, which is local recovery material rather than canonical source.
3. The root did not contain a `.gitignore`, even though repository policy requires it to be reviewed as part of significant passes.
4. The existing repository-hygiene gate detected `bin`, `obj`, and `.vs`, but did not protect against patch backups, test results, IDE user state, or loss of required ignore rules.
5. Presentation resources are partially centralized, but hardcoded colors and font declarations remain in shell and feature XAML. These are recorded as Pass 40 skin-readiness inputs, not changed in this refinement pass.
6. The application host remains composition-heavy. Shell extraction, command-bar hosting, centralized status projection, and settings-workspace decomposition remain the highest-value Pass 40 sequence.

## Refinements completed

- Added a root `.gitignore` covering build output, IDE state, runtime diagnostics, patch backups, test results, and operating-system metadata.
- Strengthened `RepositoryHygiene-Test.ps1` to reject local-only directories and user-state files and to certify required ignore rules.
- Removed generated build directories and `.patch-backups` from the refined canonical tree.
- Added explicit architectural decision governance to `ENGINEERING_PHILOSOPHY.md`.
- Recorded this assessment in roadmap, changelog, and release notes.
- Removed patch-delivery and restore-helper files from canonical source; self-contained patch archives retain their own installer and instructions.

## Delivery artifact policy

The canonical GitHub repository contains only source, maintained project documentation, tests, build tooling, and developer utilities. ChatGPT delivery mechanics are not repository assets.

- Lightweight patch archives may include `APPLY_PATCH.ps1`, a patch-specific README, and a deletion manifest.
- Full repository archives and Git commits must not include patch installers, restore notes, or temporary deletion manifests.
- Patch delivery artifacts are disposable after successful application and validation.

## Pass 40 implementation order

The recommended order remains:

1. Establish the engineering command-bar host and shell contracts.
2. Introduce centralized application-status projection using existing task and notification infrastructure rather than creating a competing status system.
3. Extract navigation-shell behavior behind explicit interfaces before adding browser-style history.
4. Create the tabbed Settings workspace foundation with independently scrolling tabs.
5. Complete a semantic-resource and template-readiness audit for shell components.
6. Implement Reforge only after preservation contracts for workspace, profile, selection, and pending work are explicit.

## Validation boundary

The refinement is designed to be validated on Windows with the pinned .NET SDK and PowerShell. A clean environment should run:

```powershell
dotnet restore .\RimForge.sln
dotnet build .\RimForge.sln --configuration Release
pwsh -NoProfile -File .\Tests\RepositoryHygiene-Test.ps1
pwsh -NoProfile -File .\Tests\Pass39Completion-Test.ps1
```

A clean boot remains a manual Windows acceptance check because RimForge is a native WPF application.

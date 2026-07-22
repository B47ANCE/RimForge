# Epic F Pass 1 — Unified Productivity Workflows

Version: 2.2.0-alpha.77

RimForge now owns a single typed productivity-action catalog for workspace destinations and discoverable commands. Global search projects that canonical catalog directly, so navigation names, aliases, destinations, and executable bulk actions no longer drift across independent lists.

The Mod Sorter exposes explicit **Enable selected** and **Disable selected** workflows for extended selections. Each workflow validates current profile policy, previews the affected mods and assistance behavior, requires confirmation, applies the group atomically from the user's perspective, and records one load-order snapshot undo. Failed bulk enable execution restores its pre-operation snapshot; bulk disable continues through the established dependency and orphan-cleanup policy engine.

## Verification

- The application solution builds with zero warnings and zero errors.
- `EpicFPass1UnifiedProductivity-Test.ps1` guards canonical catalog use, removal of the legacy duplicate workspace catalog, command discovery, preview, rollback, and undo integration.
- The repository-wide `Build-Test-All.ps1` gate remains the release criterion for this pass.

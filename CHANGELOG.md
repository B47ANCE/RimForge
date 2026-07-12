# Changelog

All notable RimForge changes are recorded here.

## 2.1.0-alpha.5 — Ember Phase 2

### Added
- Two-tier mod fingerprints: a fast top-level signature reuses the full recursive fingerprint when the mod is unchanged.
- Periodic full fingerprint verification, configurable through `Incremental.FullVerificationIntervalDays`.
- Consolidated evidence cache index at `Cache/Evidence/EvidenceIndex.json`.
- Trusted unchanged-mod evidence loading from the consolidated index.
- `VERSION` file for repository and release tooling.

### Fixed
- Removed the invalid `-DisableNameChecking` argument from the Output cleanup `Get-ChildItem` call.
- Hardened `Test-ModLibrary` for strict mode by normalizing scalar and null collections before counting.
- Corrected duplicate package-ID and Workshop-ID summary counts when no duplicates exist.
- Synchronized the displayed application version, documentation, release notes, and configuration.

### Changed
- Unchanged audits reuse cached `About.xml` metadata and evidence results.
- Evidence cache writes are atomic and produce a consolidated index after successful analysis.
- The public repository excludes runtime output, caches, generated databases, local review decisions, and downloaded third-party executables.

## 2.1.0-alpha.1 — Ember

### Added
- Unified metadata fingerprint service for installed mod folders.
- Persistent incremental mod state in `Cache/Incremental/ModState.json`.
- Change classification for added, changed, unchanged, and removed mods.
- Incremental `About.xml` cache with automatic source-file invalidation.
- Audit timing metrics and `Output/IncrementalAudit.json`.
- Fingerprint and incremental-state test suites.

## 2.0.0-alpha.3 — Anvil

### Changed
- Established the RimForge application name and PowerShell API namespace.
- Added GitHub workflows, issue templates, project documentation, and repository policies.

## 2.0.0-alpha.1 — Infrastructure

### Added
- Manifest-driven dependency management.
- Atomic JSON cache service with schema-aware invalidation.

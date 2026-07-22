# RimForge Release Plan

This document defines the path from the current alpha repository to RimForge 1.0.

## Release channels

- **Development** — repository builds used for active implementation.
- **Alpha** — incomplete builds for focused internal validation; data formats may change.
- **Beta** — feature-complete builds with migration support and known non-critical defects.
- **Release candidate** — production-intent build; changes limited to release blockers.
- **Stable** — supported public release.

## 1.0 readiness gates

### Build and packaging

- Clean restore/build/test on supported Windows environments
- Reproducible Release build
- Installer and uninstall validation
- Upgrade validation from the most recent supported prerelease
- Signed binaries and published checksums
- Correct version metadata in binaries, package, documentation, and database manifests

### Data safety

- Atomic profile/settings/rule/layout persistence
- Crash and interrupted-write recovery
- Backup and undo validation
- External `ModsConfig.xml` reconciliation
- Texture conversion output/revert validation
- No known profile-corruption or source-mod mutation defects

### Functional acceptance

- First run and Steam discovery
- Library refresh and full Forge
- Cancellation at each long-running phase
- Profile lifecycle
- Sort preview/apply/undo
- Issue navigation and repair planning
- Dependency Map interaction and persistence
- Texture Tools conversion and revert
- Launch Readiness Review
- RimWorld launch/shutdown state
- Player.log and Runtime Companion behavior

### Performance

Release thresholds must be measured for representative small, medium, and large libraries. At minimum capture:

- Cold and warm startup
- Incremental and full evidence generation
- Sort preview
- Dependency Map initial render and filtered updates
- Search latency
- Peak memory
- Cancellation latency

Threshold values should be recorded in the release checklist once canonical test libraries are established.

### Accessibility and presentation

- Keyboard traversal and focus visibility
- High-DPI and common display scaling
- Text contrast and no black-text regressions
- Screen-reader labels on critical controls
- Empty/loading/error states
- Approved production artwork and icon assets
- No placeholder, concept, rejected, or unlicensed assets in release packages

### Documentation and legal

- README and build instructions verified
- Roadmap and known issues current
- Release notes complete
- License and third-party notices complete
- Privacy behavior documented
- Support and security reporting instructions current
- Independence/non-affiliation statement included

## Release-candidate policy

After RC1, only defects that block safety, installation, startup, core workflows, accessibility, or supported compatibility should change production code. Every accepted fix requires a targeted regression test and RC revalidation.

## Rollback

Every release must retain:

- Previous stable installer
- Database content rollback package
- Documented profile/config backup location
- Migration reversal or forward-repair plan
- Known incompatible prerelease formats

## Release artifacts

- Installer
- Portable diagnostic build only when explicitly supported
- Checksums
- Release notes
- Third-party notices
- Curated database manifest and versions
- Symbol/debug package retained for support

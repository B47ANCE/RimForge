# Known Issues

This file tracks confirmed, user-relevant limitations in the current development baseline. Historical fixed defects belong in [CHANGELOG.md](CHANGELOG.md).

## Release blockers

### Post-Forge projection recovery requires Windows validation

The canonical baseline now publishes the authoritative library/evidence projection before profile-scoped analysis and preserves it when downstream Forge analysis fails. A clean Windows workflow pass is still required to validate Mod Sorter, active/inactive lists, Inspector, search, and Dependency Map behavior end to end.

### Windows revalidation is incomplete

The cumulative repository requires a clean Windows restore, build, launch, shutdown, and manual workflow pass after the latest UI, evidence, graph, texture, and profile changes.

### Sorting is not yet the complete 1.0 tri-hybrid pipeline

The repository now isolates sorting to the active profile, separates hard constraints from advisory curated rules, detects installed-but-inactive required dependencies, and publishes per-mod decision provenance into the Inspector. Community Rules ingestion, Use This Instead integration, contradiction minimization, user-lock support, preview generation tokens, and complete apply-time revalidation remain incomplete.

### Runtime Companion integration is incomplete

Desktop/runtime handshake, startup replay, version compatibility, bounded event storage, and end-to-end diagnostics are not yet release ready.

## Important limitations

### Dependency Map layout requires Windows validation

The default layout now separates disconnected components, collapses dependency cycles for deterministic layering, applies repeated barycentric ordering, dynamically widens busy layers, and packs independent islands across rows. A Windows workflow pass is still required to validate readability, persisted custom positions, minimap navigation, and rendering performance on large real profiles.

### Legacy terminology remains in source

The product-facing name is Dependency Map, but source files, tests, services, and persisted keys may still use `ForgeView`. Renaming must be migration-safe and should not be performed as a blind global replacement.

### Production artwork is incomplete

The compact application badge is now approved and integrated. Some workflow icons and loading/splash assets remain temporary or placeholder assets; only approved RF-DS production assets should ship.

### PowerShell tooling remains in the repository

Legacy modules and scripts remain useful for parity, migration, and developer validation. They are not intended to be required by the final native production runtime.

### External tools are optional and separately installed

DDS conversion requires DirectXTex/texconv when the requested output cannot be produced by built-in codecs. RimForge must report missing tooling clearly and leave unrelated workflows usable.

## Reporting a new issue

Include the RimForge version, RimWorld version, operation, expected result, actual result, and a minimal diagnostic bundle with private paths or unrelated personal data removed. See [SECURITY.md](SECURITY.md) for security-sensitive reports.

### Curated database content population

The version-aware Community Rules and Use This Instead pipelines are implemented, but the bundled replacement database intentionally ships empty until records have stable package IDs, provenance, review dates, and version scopes. Content ingestion, signing, remote update verification, and rollback UI remain release work.

- Locked-position solving and sort transaction persistence require Windows build and real-profile UI validation; the repository contains contract tests, but this environment did not provide the .NET SDK or PowerShell.
## Runtime startup replay requires Windows validation

The Player.log monitor now replays the bounded startup tail, including an unterminated final line, before transitioning to live monitoring. Validate against a real RimWorld startup log containing early duplicate-package and missing-dependency diagnostics.


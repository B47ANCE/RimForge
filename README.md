# RimForge

> We're not just managing your mods—we're forging a stable, optimized mod ecosystem.

RimForge is an advanced RimWorld mod analysis, optimization, repair, and profile-management platform for large mod collections.

## Current release

**2.1.0-alpha.5 “Ember Phase 2”** is the canonical first GitHub baseline and introduces incremental intelligence. RimForge fingerprints installed mods, remembers the previous state, reuses unchanged `About.xml` metadata and evidence results, and reports how much work was avoided.

## Features

- Dependency graph and missing-dependency detection
- Multi-profile validation and comparison
- Load-order optimization and blueprint scoring
- RimWorld version and Workshop status checks
- Evidence-based taxonomy and generated mod database
- DDS texture validation, conversion, and repair
- External dependency management for tools such as `texconv`
- Incremental mod fingerprints, cache invalidation, and timing metrics

## Quick start

1. Edit `Config.json` and set the RimWorld mod roots for your Steam installation. The committed defaults use the standard `C:` paths.
2. Place native `ModsConfig.xml` profiles in `Profiles`.
3. Run `Launch-RimForge.bat`, or:

```powershell
powershell -ExecutionPolicy Bypass -File .\Audit.ps1
```

The first Ember run establishes an incremental baseline. Later runs report changed, unchanged, added, and removed mods.

## Runtime data

- `Profiles` — source profiles; never cleared automatically.
- `Output` — reports from the current run; cleared at audit startup.
- `Cache` — persistent metadata, evidence, dependency, and incremental state.
- `Logs` — timestamped audit logs.
- `Database.Generated` — normalized derived records.
- `Database` — rules, taxonomy, blueprint, and dependency manifests.

Ember adds:

- `Cache\Incremental\ModState.json`
- `Cache\AboutMetadata\*.json`
- `Cache\Evidence\EvidenceIndex.json`
- `Output\IncrementalAudit.json`

Delete those cache folders to force a fresh baseline. Source mods and profiles are never modified by incremental analysis.

## Validation

```powershell
powershell -ExecutionPolicy Bypass -File .\Tests\Smoke-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\FingerprintService-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\IncrementalAudit-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\CacheService-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\DependencyManager-Test.ps1
```

## Safety model

RimForge does not execute mod assemblies during evidence analysis. Texture installation remains an explicit operation. Generated database records contain derived metadata, not Workshop payloads.

RimForge is an independent desktop project and is not affiliated with Ludeon Studios or any Steam Workshop mod sharing the same or a similar name.

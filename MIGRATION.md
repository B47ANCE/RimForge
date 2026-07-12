# RimForge migration guide

## Installing this release over an earlier development build

1. Back up or commit the current project folder.
2. Replace tracked source files with the contents of this repository.
3. Keep local `Profiles` and any curated rule changes you intentionally maintain.
4. Do not copy old `Cache`, `Output`, `Logs`, `TextureWork`, or `Database.Generated` folders into Git.
5. Edit `Config.json` for the local Steam and RimWorld installation paths.
6. Run the test suite before the first audit.

```powershell
powershell -ExecutionPolicy Bypass -File .\Tests\Smoke-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\CacheService-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\DependencyManager-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\FingerprintService-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\IncrementalAudit-Test.ps1
```

## Incremental-state migration

No generated state is required in the repository. The first local audit creates a new baseline under `Cache\Incremental`. Later unchanged runs reuse cached About metadata and evidence results.

To rebuild the baseline:

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\Reset-RimForgeIncrementalState.ps1 -IncludeEvidenceCache
powershell -ExecutionPolicy Bypass -File .\Audit.ps1
```

## External tools

Third-party executables are not version controlled. Run the dependency test to validate or install supported tools locally:

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\Test-RimForgeDependencies.ps1 -InstallMissing
```

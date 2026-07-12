# Contributing to RimForge

Thank you for helping improve RimForge.

## Development workflow

1. Create a focused branch from `main`.
2. Keep engine logic in `Modules` and thin orchestration in `Scripts`.
3. Add or update tests for behavior changes.
4. Run the smoke test and all directly affected tests.
5. Submit a pull request with the problem, approach, validation, and compatibility impact.

## PowerShell conventions

- Use approved PowerShell verbs for new public commands when practical.
- Prefix public project commands with `RimForge` in the noun portion.
- Use `Set-StrictMode -Version Latest` and terminating error behavior in entry scripts.
- Prefer `[PSCustomObject]` result objects over formatted strings from service functions.
- Keep filesystem writes explicit and use atomic writes for persistent JSON state.
- Analysis should be non-destructive; modification requires a separate, explicit action.

## Tests

At minimum, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tests\Smoke-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\CacheService-Test.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\DependencyManager-Test.ps1
```

Run texture tests whenever changing `TextureOptimizer.psm1` or texture scripts.

## Compatibility data

Curated compatibility and classification changes should include evidence and should not rely solely on a mod name. Prefer package IDs, Workshop IDs, declared dependencies, XML evidence, or documented upstream behavior.

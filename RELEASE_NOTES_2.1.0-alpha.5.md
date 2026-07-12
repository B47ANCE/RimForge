# RimForge 2.1.0-alpha.5 “Ember Phase 2”

> We're not just managing your mods—we're forging a stable, optimized mod ecosystem.

This release is the canonical first GitHub baseline for RimForge.

## Highlights

- Incremental mod-state detection for added, changed, unchanged, and removed mods.
- Cached `About.xml` parsing.
- Consolidated evidence cache index for unchanged audits.
- Two-tier fingerprints with periodic full verification.
- Strict-mode validator fixes and correct duplicate counters.
- Clean repository layout with CI, tests, policies, and no generated user data.

## First run

The first audit establishes the cache and fingerprint baseline. Every installed mod is expected to appear as added and changed.

## Later runs

When no mods changed, RimForge should report:

```text
changed=0, unchanged=<installed mod count>
About metadata: parsed=0, cached=<installed mod count>
Evidence scan: scanned=0, cached=<installed mod count>
```

## Required local setup

Edit `Config.json` if Steam or RimWorld is installed outside the default `C:` paths. Downloaded tools such as `texconv.exe` are intentionally not committed; RimForge's dependency tooling manages them locally.

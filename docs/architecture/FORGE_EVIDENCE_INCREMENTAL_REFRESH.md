# Forge Evidence Incremental Refresh

## Purpose

Epic 1 Pass 3 makes evidence refresh incremental, invalidation-safe, and continuously maintainable while RimForge is running. File-system callbacks never perform analysis. They record durable in-memory invalidations, and the refresh scheduler converts settled invalidations into one serialized evidence generation.

## Refresh lifecycle

1. Watchers observe relevant changes beneath installed mod roots.
2. Transient editor and temporary files are ignored.
3. Repeated events for one root are debounced.
4. The invalidation journal assigns a monotonic sequence and retains the strongest reason.
5. A refresh captures an immutable invalidation set.
6. Only invalidated, newly installed, target-version-changed, or explicitly forced mod roots bypass the per-mod cache.
7. Producers produce a complete projection for each source kind that participated.
8. Refresh-owned source projections replace their previous generation, removing stale facts for deleted mods and removed declarations.
9. Runtime and other append-oriented source kinds that did not participate remain intact.
10. The snapshot is atomically persisted and published.
11. Only captured invalidation sequence numbers are acknowledged. Events arriving during the refresh remain pending for the next generation.

## Scheduling

`ForgeEvidenceRefreshScheduler` owns the application-level auto-refresh policy. It stores the latest full installed-library request, listens for evidence invalidations, waits for the configured settle period, then invokes the same serialized `IForgeEvidenceService.RefreshAsync` path used by explicit refreshes. It does not create a parallel analysis system.

The scheduler is composed as an application singleton, configured when shared intelligence starts, and disposed before the evidence service.

## Watcher reliability

Each mod root receives one recursive watcher. Watchers use a bounded 64 KiB buffer, relevant extension filtering, and per-root debounce. Buffer overflow records a high-priority invalidation so the next generation performs a conservative rescan. Watcher failures do not terminate RimForge.

## Determinism and stale-evidence prevention

Refresh contribution reconciliation is source-projection based. When a producer source runs, its former projection is removed before the newly collected projection is merged. This ensures removed dependencies, incompatibilities, metadata, and uninstalled mods cannot survive as stale Forge Evidence.

Producer ordering, evidence identity, merge ordering, and snapshot publication remain deterministic.

## Metrics

Every generation records:

- pending invalidations captured by the generation
- reconciled contribution count
- active watcher count
- scanned and reused mod counts
- cache misses and corrupt-cache recovery
- coalesced requests and debounced invalidations
- watcher overflows and cache cleanup totals

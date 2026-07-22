# Epic B Pass 3 Incremental Analysis Reuse

Epic B Pass 3 adds bounded in-memory result reuse to the deterministic execution boundary established in Passes 1 and 2. The analysis algorithms remain unchanged.

## Cache identity and policy

`ModAnalysisRequest.CachePolicy` supports three explicit modes:

- `Use` reuses an unchanged result or executes and stores a cache miss.
- `Refresh` always executes and replaces the matching entry.
- `Bypass` executes without reading or writing the cache.

`ModAnalysisResult.Cache` reports the disposition, fingerprint key, and original generation time. A cache hit publishes typed cache-lookup and completion progress rather than pretending that graph stages ran again.

The fingerprint now includes Forge Evidence badges, capabilities, and notable findings in addition to mod identity, dependency and ordering metadata, compatibility inputs, profile order, target version, and user locks. This covers inputs used by load-order classification and prevents stale reuse after evidence changes.

## Lifetime and invalidation

The application owns one shared analysis engine, so its cache is shared by Forge DNA and direct client analysis. The cache retains at most eight least-recently-used results to bound memory. `InvalidateCache()` clears all entries; passing a fingerprint removes only the matching result.

Failed and canceled runs never reach the store boundary. Cache state is process-local and contains no user data on disk.

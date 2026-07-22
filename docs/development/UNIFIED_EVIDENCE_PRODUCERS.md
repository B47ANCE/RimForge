# Unified Evidence Producers

Epic A Pass 2 uses one collection contract: `IForgeEvidenceProducer`. Static metadata, dependencies, Harmony inspection, community knowledge, replacement guidance, runtime observations, and compatibility intelligence all enter the same deterministic pipeline through this boundary.

Each producer declares a stable `ProducerId`, one source kind, and an explicit order. It returns contributions, structured producer diagnostics, and elapsed time. The pipeline isolates producer failures, retries transient failures, validates source ownership and provenance, consolidates duplicates, and hands one immutable transaction to `ForgeEvidenceService`.

The earlier contributor abstraction has been removed rather than retained as an adapter. This prevents two extension contracts from evolving independently. Persisted snapshots retain the historical `contributorDiagnostics` JSON field name solely as a storage compatibility detail; application code exposes producer terminology throughout.

# Epic A Pass 2 Unified Evidence

Epic A Pass 2 is complete. Forge Evidence is the single evidence plane for desktop analysis, dependency metadata, Harmony inspection, community rules, replacement guidance, runtime observations, and compatibility intelligence.

## Collection and aggregation

Every source implements `IForgeEvidenceProducer`. `ForgeEvidencePipeline` executes producers deterministically, validates identity, source ownership, provenance, observation windows, and confidence, retries transient failures, isolates producer failures, and reconciles complete source projections. Contributions use deterministic identities and are consolidated before publication.

## Publication

`IForgeEvidenceBus` owns the current immutable generation. Refresh, direct ingestion, and restoration publish through the same bus with an explicit reason. The desktop subscribes to the bus; `IForgeEvidenceService` remains orchestration rather than a second publication channel.

## Durability

`IForgeEvidenceStore` writes the complete generation to the workspace cache using write-through temporary files and atomic replacement. Before replacement it retains the prior generation as a last-known-good backup. Loading reports missing, loaded, recovered, unsupported, or corrupt state; corrupt primary and backup files are quarantined rather than reused.

The historical `contributorDiagnostics` JSON property remains readable as a storage compatibility detail even though the application contract is now producer-based.

## Unified consumers

Runtime and compatibility stores remain acquisition ledgers, but their observations become authoritative application evidence only through their registered producers. Runtime changes invalidate Forge Evidence and trigger the normal scheduled refresh. ForgeView projects runtime and compatibility relationships from the published evidence generation; its former direct runtime-store graph path has been removed.

## Source coverage

- Static desktop metadata and scan findings
- Declared dependencies, ordering, and incompatibilities
- Harmony patch inspection
- Curated community load-order knowledge
- Maintained replacement guidance
- Runtime Companion observations
- Derived compatibility intelligence

## Verification

`tests/EpicAPass2UnifiedEvidence-Test.ps1` certifies the architectural boundaries. `RimForge.ExecutionTests` covers deterministic aggregation, ingestion, bus publication, persistence restoration, backup recovery, incremental reconciliation, and scheduled refresh.

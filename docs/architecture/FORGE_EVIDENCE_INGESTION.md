# Forge Evidence ingestion pipeline

## Purpose

The Forge Evidence ingestion pipeline is the only supported path for converting subsystem observations into authoritative Forge Evidence contributions. Producers collect observations; the pipeline validates and consolidates them; `ForgeEvidenceService` commits the resulting immutable snapshot atomically.

## Transaction lifecycle

1. `ForgeEvidenceService` completes mod inventory scanning for the current installed-library projection.
2. A `ForgeEvidenceCollectionContext` captures the target RimWorld version, prior snapshot descriptor, invalidated roots, force-rescan state, and stable start timestamp.
3. `ForgeEvidencePipeline` orders producers by explicit order, source kind, and producer ID.
4. Each producer returns a `ForgeEvidenceProducerResult` containing contributions, diagnostics, and elapsed time.
5. Transient `IOException` and `TimeoutException` failures are retried according to `ForgeEvidencePipelineOptions`.
6. Producer identity, source ownership, observation windows, confidence, provenance, and required fields are validated.
7. Duplicate observations are consolidated deterministically.
8. Evidence IDs missing from a source are generated from a stable SHA-256 identity rather than random GUIDs.
9. The service merges the transaction with the previous generation and persists it through `IForgeEvidenceStore` using an atomic temporary-file replacement and last-known-good backup.
10. The completed immutable generation is published through `IForgeEvidenceBus`; the service exposes no parallel snapshot event.
11. Cancellation or validation failure before persistence leaves the previously published generation unchanged.

## Built-in producers

### Static mod metadata

`StaticModMetadataEvidenceProducer` emits:

- mod inventory size and file count;
- detected technology/capability badges;
- parser and metadata errors;
- source, root path, target version, and inventory attributes.

### Dependency metadata

`DependencyMetadataEvidenceProducer` emits:

- required dependency relationships;
- load-before declarations;
- load-after declarations;
- declared incompatibilities.

Both producers consume the complete installed mod library supplied to the Forge operation. Active-profile projection remains a downstream concern.

## Query index

Each `ForgeEvidenceSnapshot` constructs a read-only `ForgeEvidenceIndex` with deterministic indexes for:

- subject;
- evidence type;
- source kind;
- bidirectional subject relationships.

Consumers should query the snapshot index rather than build private evidence maps.

## Storage and publication

`IForgeEvidenceStore` is the only durable snapshot boundary. It reports missing, loaded, backup-recovered, unsupported-schema, and corrupt states. Corrupt files are quarantined. `IForgeEvidenceBus` is the only in-memory publication boundary and holds the current immutable generation for consumers.

## Producer contract

A producer must:

- expose a stable, globally unique `ProducerId`;
- declare exactly one `ForgeEvidenceSourceKind`;
- use deterministic ordering internally;
- honor cancellation promptly;
- return only evidence owned by its declared source kind;
- provide provenance for every contribution;
- avoid persistence or publication side effects;
- report recoverable observations through diagnostics rather than throwing;
- throw only when its result cannot be trusted.

## Failure behavior

A failed producer is isolated and recorded as `RF-EVIDENCE-PRODUCER-FAILED`; other producers continue. Invalid evidence returned by a successful producer fails the transaction because publishing malformed evidence would compromise the shared source of truth. Cancellation always aborts the transaction.

## Schema compatibility

Schema version 2 adds producer collection contracts, producer diagnostics, deterministic generated evidence IDs, and query-ready indexes. Version 1 snapshots remain readable; missing diagnostics are migrated to an empty collection during restoration.

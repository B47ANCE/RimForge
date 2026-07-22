# Forge Evidence Platform

Forge Evidence is RimForge's authoritative, versioned evidence substrate. Static scans, dependency analysis, Runtime Companion observations, Harmony inspection, Community Rules, Use This Instead, user overrides, and compatibility intelligence all publish normalized contributions into the same model.

## Guarantees

- **Versioned contracts:** every persisted snapshot and ingestion batch declares a schema version.
- **Deterministic consolidation:** contributions are ordered and merged by subject, relationship, evidence type, source kind, and source identity.
- **Traceable provenance:** each contribution identifies its source, source version, observation time, and optional session/correlation metadata.
- **Explicit confidence:** confidence is normalized to `[0,1]` and classified into stable confidence bands.
- **Atomic persistence:** snapshots are written through a temporary file and atomically replaced.
- **Defensive recovery:** unreadable snapshots are quarantined and rebuilt rather than silently trusted.
- **Extensibility:** engines implement `IForgeEvidenceContributor`; the coordinator invokes contributors in deterministic order.

## Ingestion

`IForgeEvidenceService.IngestAsync` validates schema compatibility, required identities, confidence bounds, and source consistency before publication. Repeated observations with the same logical identity are consolidated. Observation counts are summed, timestamps are widened, attributes are merged deterministically, and confidence is weighted by observation count.

## Persistence

The authoritative snapshot is stored under the RimForge cache layout in `ForgeEvidence/snapshot.json`. `RestoreAsync` restores a compatible generation and republishes it to existing consumers. A future schema is rejected; malformed content is quarantined with a timestamped suffix.

## Integration Rule

New analysis systems must contribute to Forge Evidence instead of creating independent caches or parallel issue models. Presentation layers consume published generations and must not perform evidence collection themselves.

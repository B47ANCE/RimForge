# RimForge Curated Databases

RimForge bundles and may update structured knowledge used by sorting, analysis, repair, and recommendations. Curated data is code-adjacent product logic and requires the same rigor as executable source.

## Database families

### Classification overrides

Maps specific mods to canonical categories when metadata and heuristics are insufficient or misleading.

### Compatibility rules

Describes known incompatibilities, requirements, conditional compatibility, and replacement guidance.

### Load-order rules

Describes relative ordering, anchors, framework placement, patch relationships, and exceptions.

### Patch rules

Describes known patch targets and ordering requirements that cannot be derived reliably from static metadata.

### Community Rules

Versioned external or bundled knowledge contributed from maintained community sources. Community Rules may contain sorting, compatibility, classification, and diagnostic guidance.

### Use This Instead

Advisory replacement records for obsolete, duplicated, superseded, unsafe, or abandoned mods. These records are recommendations only.

## Required record metadata

Every non-trivial rule should include:

- Stable rule ID
- Schema version
- Rule type
- Subject package ID(s)
- Target package ID(s), when applicable
- Target RimWorld version range
- Confidence: `Hard`, `Recommended`, or `Experimental`
- Source/provenance
- Human-readable rationale
- Last reviewed date
- Optional upstream reference
- Optional replacement or deprecation metadata

## Governance

- Hard rules require reproducible evidence or authoritative metadata.
- Community popularity alone is not sufficient for Hard confidence.
- Recommendations must not be represented as dependencies.
- Rules should be as specific as possible and avoid broad author- or category-level assumptions.
- Conflicting sources are preserved as reviewable conflicts rather than silently selecting one.
- Every update is validated before publication and can be rolled back independently of the application.

## Versioning

Database schema version and content version are independent.

- **Schema version** changes when structure or interpretation changes.
- **Content version** changes when records are added, removed, or revised.
- Records target explicit RimWorld versions or ranges.
- Unknown future versions should degrade to advisory status unless a rule is proven version-independent.

## Validation

Validation must detect:

- Missing IDs or provenance
- Invalid package IDs
- Unknown confidence levels
- Duplicate rule IDs
- Self-referential ordering or replacement rules
- Hard-rule cycles
- Contradictory hard relationships
- Invalid version ranges
- Replacement loops
- References to unavailable schema fields

A database containing invalid hard constraints must fail closed. Advisory records may be quarantined individually when safe.

## Update model

RimForge should support:

1. Bundled, signed baseline data.
2. Optional remote updates with signature/checksum verification.
3. Local user overrides stored separately from bundled data.
4. Clear source and version display in explanations.
5. Rollback to the last valid database.

Remote data must never be required for core operation.

## Use This Instead behavior

A replacement recommendation may state that a mod is:

- Superseded
- Obsolete for the target game version
- Duplicated by another active mod
- Abandoned with a maintained fork
- Unsafe or incompatible in a known configuration

RimForge may offer navigation, comparison, and acquisition guidance. It must not automatically unsubscribe, delete, replace, activate, or reorder content solely because a replacement record exists.

## Repository layout

Bundled production data currently lives in `Database.Curated`. Root-level compatibility files are legacy and should not become a second authoritative source. Future consolidation should migrate all production reads to one versioned database root with schema fixtures and validation tests.

## Implemented baseline

The application now loads schema-versioned, target-version-scoped load-order records with stable IDs and provenance. Invalid hard records fail closed, direct contradictory relationships are quarantined and surfaced in Issue Viewer, and `Database.Curated/UseThisInstead.json` is loaded as advisory-only replacement knowledge. Replacement records never trigger automatic subscription, removal, activation, or profile mutation.

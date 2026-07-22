# Epic D Pass 2 — Canonical Graph Query and Selection

Status: complete  
Version: 2.2.0-alpha.71

## Outcome

ForgeView now has one Core-owned definition of visible graph topology. Canvas and outline submit the same query criteria for profile scope, structured-search matches, node health, relationship types, and focused-path isolation. Search and Issue Viewer navigation identify their selection origin through the same graph selection contract.

Selection is deterministic and profile-owned. The persisted ForgeView layout document now includes selection history, history position, and focused package ID alongside node positions, pins, zoom, pan, and filters. Moving backward and then selecting a new node truncates the stale forward branch.

Every relationship returned by the canonical query has a non-null provenance record. Static metadata declarations receive normalized manifest provenance; shared/runtime evidence relationships preserve their evidence source and evidence ID. Focused provenance is visible in the selection context and included in relationship exports.

## Ownership

- `RimForge.Core` owns query, result, provenance, navigation-origin, selection-snapshot, query-service, and selection-state contracts.
- `RimForge.Infrastructure` enriches projected relationships with evidence provenance.
- `RimForge.App` translates UI controls into canonical queries and renders the returned topology.

## Acceptance

- Release solution build succeeds with zero warnings.
- Execution tests cover deterministic filtering/order, an active empty search, provenance normalization, history navigation/branching, and profile-state restoration.
- `Tests/EpicDPass2CanonicalGraphQuery-Test.ps1` prevents canvas, outline, search, Issue Viewer, persistence, and projection from drifting away from the canonical contracts.

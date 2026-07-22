# RimForge Sorting Engine

This document defines the canonical load-order strategy for RimForge 1.0.

## Goals

The sorter must be deterministic, conservative, explainable, reversible, and valid for the active profile. It must distinguish facts from recommendations and preserve user intent whenever stronger constraints do not require movement.

## Inputs

- Installed library identities and evidence
- Active profile membership and current order
- Official Core/DLC state
- Mod-declared dependencies, `loadAfter`, and `loadBefore`
- Versioned curated Community Rules
- Use This Instead recommendations
- Category and framework policy
- User-locked or protected positions where supported
- Target RimWorld version

## Active profile and installed library

RimForge may use the entire installed library to resolve package identities, locate inactive dependencies, and surface recommendations. The final sort graph, however, contains only active-profile nodes plus official content required by the profile.

An unrelated inactive mod must never change the order of active mods. An inactive required dependency may produce an assistance finding, but it is not silently activated during sorting.

## Tri-hybrid pipeline

### Stage 1 — Authoritative constraints

Create hard graph edges from:

- Core and official DLC anchors
- Required dependencies
- Valid mod-declared `loadAfter` and `loadBefore`
- Explicit hard curated rules
- User locks that do not conflict with stronger requirements

Invalid references are reported, not converted into phantom nodes.

### Stage 2 — Curated community intelligence

Apply version-compatible rules with provenance:

- Known framework and library placement
- Compatibility-specific relative ordering
- Patch-after-target relationships
- Known exceptions to broad category policy
- Community-maintained ordering knowledge

Rules carry confidence (`Hard`, `Recommended`, or `Experimental`). Only Hard rules enter the mandatory graph. Recommended and Experimental rules influence preference and explanations unless the user explicitly opts into stronger enforcement.

Use This Instead data is advisory. It can flag obsolete, superseded, duplicate, or unsafe choices and recommend alternatives. It never removes, unsubscribes, downloads, or replaces a mod automatically.

### Stage 3 — Deterministic heuristic policy

For nodes not fully ordered by stronger constraints, apply stable policy:

- Canonical category bands
- Framework/library placement
- Content before patches that target it
- Compatibility patches after all known targets
- Late-load optimization tools where explicitly supported
- Original profile order as the primary stability tie-breaker
- Package ID as the final deterministic tie-breaker

Heuristics must not override a hard graph edge.

## Cycle and contradiction handling

Before producing an order, RimForge identifies:

- Dependency cycles
- Contradictory hard rules
- Hard rules conflicting with official anchors
- User locks that cannot be honored
- Mutually incompatible active mods
- Ambiguous package identity

RimForge must not silently drop hard edges. It should isolate the smallest useful conflict set, show provenance for each edge, and require resolution or explicit bounded override.

Recommended-rule cycles may be broken deterministically by confidence, specificity, source priority, and stability. The broken preference must be disclosed in the preview.

## Stable topological ordering

The final order is generated from the acyclic hard graph using a stable priority queue. Candidate priority is determined by:

1. Official-content anchor rank
2. Hard category band where valid
3. Recommended-rule score
4. Heuristic category score
5. Original active-profile index
6. Normalized package ID

This preserves as much valid user order as possible while remaining deterministic across runs.

## Explanation model

Every moved mod should expose:

- Previous and proposed position
- Primary reason
- Supporting rule/evidence
- Rule source and confidence
- Related mods
- Whether the movement is required or recommended

Every unchanged mod may expose why its position is already valid. Explanations are generated from the same decisions that produced the order; they are not reconstructed independently afterward.

## Preview and apply

Sorting is a two-step transaction:

1. **Preview** — calculate order, movements, unresolved findings, and explanations without mutation.
2. **Apply** — revalidate profile generation, persist atomically, publish one workspace update, and create one undo unit.

If the profile changed after preview, apply is rejected and a new preview is required.

## Official content policy

- Core is first.
- Available official DLC nodes are represented consistently in Dependency Map.
- Core connects to every available official DLC for presentation.
- DLC order follows supported canonical policy and declared relationships.
- Implicit Core-to-normal-mod graph clutter is suppressed unless a relationship is diagnostically meaningful.

## Rule precedence

From strongest to weakest:

1. Required dependency and official-content invariants
2. Valid hard mod metadata
3. Valid Hard curated rules
4. User locks that remain satisfiable
5. Recommended curated rules
6. Experimental curated rules when enabled
7. Category and patch heuristics
8. Existing profile order
9. Package ID tie-breaker

## Validation requirements

The sorting engine requires tests for:

- Determinism
- Stable preservation of unconstrained order
- Inactive-library isolation
- Core/DLC anchors
- Required dependencies
- Declared before/after metadata
- Curated rule precedence and version targeting
- Rule contradictions and cycles
- Community recommendation behavior
- Preview/apply generation mismatch
- Undo and atomic persistence
- Large-profile performance

## Current implementation checkpoint

The native analysis pipeline now enforces the first executable slice of the tri-hybrid design:

- the installed library remains available for identity and dependency discovery;
- the proposed order is calculated only from active-profile nodes;
- required dependencies and declared `loadBefore`/`loadAfter` metadata form mandatory edges;
- only curated rules marked `Hard` enter the mandatory graph;
- `Recommended` and `Experimental` curated relative rules influence deterministic eligible-node priority without creating hard cycles;
- installed-but-inactive required dependencies become explicit findings with assisted activation plans; and
- every proposed node receives decision provenance projected into Mod Inspector.

The remaining sorting work is rule-database ingestion/version targeting, contradiction-set diagnostics, user locks, generation-safe preview/apply transactions, Use This Instead recommendations, and broader behavioral tests on real profiles.

## User-locked positions

A profile may persist package-position locks in `WorkspacePreferences.json`. Locks are applied only after the mandatory dependency and hard-rule graph has produced a complete topological order. A lock that would cross a mandatory edge is not applied. RimForge emits a `UserLockConflict` finding with the blocking package and legal alternative positions.

## Preview and apply transactions

Sorting is split into preview and apply phases. The preview contains the original order, proposed order, lock conflicts, and a transaction identifier. Apply is permitted only for conflict-free previews. Persistence uses the existing atomic profile save path; a failed save returns the original order and leaves the profile unchanged.

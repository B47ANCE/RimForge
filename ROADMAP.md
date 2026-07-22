# RimForge Roadmap

RimForge is currently implementing **Epic C — Library and Profiles**. Work builds on the existing application; completed systems are improved rather than replaced.

## Epic A — Platform Foundation and Runtime Infrastructure

### Pass 1: infrastructure

- [x] Forge Session lifecycle, identity, cancellation, completion, and persistence
- [x] centralized Steam, RimWorld, Workshop, workspace, and configuration discovery
- [x] Companion Host lifecycle, IPC, Player.log watching, and process monitoring
- [x] shared diagnostics, health, timing, and session logging
- [x] migration of long-running work to the background-task framework
- [x] removal of hardcoded and scattered path discovery

### Pass 2: unified evidence

- [x] one `IForgeEvidenceProducer` contract
- [x] evidence aggregation and publication bus
- [x] durable evidence store
- [x] desktop, runtime, dependency, compatibility, and community knowledge as evidence

### Pass 3: resilience

- [x] self-validation and health checks
- [x] crash/session recovery
- [x] signed update channels and rollback
- [x] preservation of settings, output, and caches

## Epic B — Analysis Engine

### Pass 1: execution foundation

- [x] explicit analysis request, result, progress, diagnostics, and metrics contracts
- [x] deterministic full-installed-library indexing independent of discovery order
- [x] asynchronous cancellable analysis execution boundary
- [x] reproducible input fingerprint and library/profile scope metrics
- [x] Forge DNA integration through the canonical analysis execution contract

### Pass 2: observable analysis pipeline

- [x] typed indexing, relationship, rule, graph, profile, planning, finalization, and completion stages
- [x] live progress and per-stage execution metrics
- [x] complete structured diagnostic projection with package context
- [x] deterministic relationship, graph-map, cycle, and diagnostic output ordering
- [x] result fingerprint coverage for metadata, profile order, target version, and user locks
- [x] cancellation checks across graph traversal and load-order planning

### Pass 3: incremental analysis reuse

- [x] deterministic result reuse keyed by the complete analysis input fingerprint
- [x] explicit use, refresh, and bypass request policies
- [x] hit, miss, refreshed, and bypassed result provenance
- [x] bounded least-recently-used in-memory retention
- [x] targeted and full cache invalidation contract
- [x] evidence-sensitive invalidation and no storage of failed or canceled runs

### Pass 4: analysis explainability

- [x] canonical full-library analysis overview and status narrative
- [x] stable per-mod explanation catalog with case-insensitive lookup
- [x] combined diagnostics, dependency impact, and repair recommendations
- [x] incoming and outgoing relationship rationale with rule provenance and confidence
- [x] active-profile membership and load-order placement reasoning
- [x] explainability retained atomically with incremental cached results

### Pass 5: unified evidence analysis

- [x] Forge Evidence accepted by the canonical analysis request and fingerprint
- [x] runtime performance, integration, conflict, compatibility, and replacement classification
- [x] stable evidence-backed finding identity, provenance, confidence, and observation context
- [x] non-mandatory observed-conflict relationships between installed mods
- [x] Forge DNA and main-client analysis wired to the current evidence generation
- [x] removal of parallel runtime and Forge Evidence issue interpretation from the UI

## Epic C — Library and Profiles

### Pass 1: canonical library/profile projection

- [x] deterministic installed-library inventory independent of discovery order
- [x] canonical profile-to-library reconciliation
- [x] installed, missing, and ambiguous active-mod resolution
- [x] inactive installed inventory per profile
- [x] case-insensitive profile lookup and duplicate package-ID reporting
- [x] stable workspace fingerprint for downstream refresh and caching

### Pass 2: atomic profile editing

- [x] immutable profile edit drafts and explicit change sets
- [x] added, removed, and reordered package previews
- [x] Core, duplicate, missing, ambiguous, empty, and lock validation
- [x] stale-workspace detection before persistence
- [x] canonical save path with existing backup and rollback behavior

### Pass 3: external profile conflict resolution

- [x] explicit adopt, restore, and defer resolution outcomes
- [x] no-write defer behavior
- [x] locked-profile protection for external adoption
- [x] canonical backed-up save and activation recovery paths
- [x] monitor acknowledgement only after successful file-changing resolutions
- [x] main-client notification actions for every resolution choice

### Pass 4: durable profile catalog state

- [x] canonical typed favorite and lock metadata
- [x] deterministic case-insensitive normalization
- [x] atomic staged persistence
- [x] automatic migration from legacy shell-owned state
- [x] preservation through profile rename and removal workflows
- [x] removal of direct profile catalog JSON handling from the UI

## Later epics

- **Epic C:** Library and Profiles
- **Epic D:** ForgeView
- **Epic E:** Repair Engine
- **Epic F:** Productivity
- **Epic G:** Release engineering

Release gates include deterministic builds, automated tests, runtime protocol compatibility, recovery validation, installer/update safety, performance profiling, and complete user/developer documentation.

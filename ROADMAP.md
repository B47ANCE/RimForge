# RimForge Roadmap

RimForge is currently completing the **Companion Production-Readiness Gate**, then resumes **Epic D — ForgeView**. Work builds on the existing application; completed systems are improved rather than replaced.

## Delivery workflow

Work follows the product dependency chain: runtime evidence → analysis → profiles → visualization → repair → productivity → release. A pass should deliver one reviewable vertical capability, keep all three repositories buildable, and end with tests, documentation, version alignment, and a pushed commit.

- **Implementation gates** run continuously: deterministic build, unit/static tests, repository boundaries, protocol compatibility, and no regressions in existing client behavior.
- **Integration gates** run when a producer/consumer boundary changes: real RimWorld startup, IPC/offline replay, client ingestion, and UI projection.
- **Release gates** run after feature work stabilizes: performance, recovery, packaging, updates, security, and full acceptance matrices.
- Companion scope stays passive and minimal. Data collection belongs in `RimForge.Companion`; hosting, interpretation, display, and repair belong in `RimForge.Client`; controlled runtime fixtures belong in `RimForge.Companion.TestSuite`.

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

### Pass 5: verified profile portability

- [x] pre-import portable package inspection
- [x] manifest and ModsConfig checksum verification
- [x] manifest/config load-order consistency validation
- [x] archive entry count, path, and size safety limits
- [x] missing and target-version-incompatible mod preview
- [x] main-client rejection and compatibility-warning workflow

### Pass 6: profile workspace continuity

- [x] durable last-selected profile identity
- [x] durable full-library versus active-profile scope
- [x] startup restoration through canonical catalog state
- [x] deterministic fallback when the remembered profile no longer exists
- [x] rename and deletion continuity updates
- [x] no additional UI-owned persistence format

### Pass 7: canonical workspace adoption

- [x] live client ownership of one library/profile workspace snapshot
- [x] refresh after library discovery and profile catalog loading
- [x] refresh after in-place Mod Sorter and Issue Viewer profile saves
- [x] active profile rows sourced from canonical resolution
- [x] inactive installed inventory sourced from canonical projection
- [x] workspace fingerprint surfaced in client diagnostics

### Pass 8: canonical profile readiness

- [x] deterministic ready, warning, and blocked states
- [x] missing Core and unresolved installation detection
- [x] duplicate active entry detection
- [x] target RimWorld version compatibility warnings
- [x] readiness surfaced in profile management UI
- [x] unsafe activation blocked before RimWorld configuration changes

## Companion Production-Readiness Gate

### Gate 1: implementation and automation — do now

- [x] standalone Companion and Test Suite repositories with clean ownership boundaries
- [x] Companion and runtime harness build against RimWorld 1.6 managed assemblies
- [x] protocol serialization, fingerprint, severity, and mod-context tests
- [x] replace placeholder assembly metadata and align product/package naming
- [x] automate Companion package validation and cross-repository protocol compatibility in CI
- [x] add deterministic transport, spool recovery, truncation/rotation, malformed-envelope, and offline replay tests
- [x] convert the RF-AGENT-001 and RF-AGENT-002 acceptance documents into an executable or evidence-recording checklist

### Gate 2: real-runtime integration — do before expanding ForgeView

- [x] install the Companion and Test Harness into the development RimWorld environment
- [x] validate main-menu startup, session identity, hello, heartbeat, shutdown, and no-host behavior
- [x] validate live delivery, offline spooling, desktop restart, deduplication, and Player.log rotation
- [x] verify Alpha/Beta attribution, optional integration behavior, and false-positive controls
- [x] record acceptance evidence and resolve every runtime defect found

### Gate 3: product certification — finish after Epics D and E

- [ ] verify runtime evidence in Inspector and Issue Viewer
- [ ] verify ForgeView relationship types and provenance
- [ ] verify Repair Engine never performs an unsafe automatic runtime-evidence repair
- [ ] complete performance, soak, recovery, packaging, install/update, and security validation
- [ ] mark both Companion acceptance checklists complete with release evidence

## Epic D — ForgeView

### Pass 1: graph domain boundary

- [x] immutable graph projection contracts owned by Core
- [x] graph diff, cluster, intelligence, and metrics models owned by Core
- [x] projection service interface independent of Infrastructure
- [x] Infrastructure limited to graph execution and incremental caching
- [x] UI consumers compiled against domain contracts
- [x] architecture regression coverage

### Pass 2: canonical graph query and selection

- [x] one query/filter contract shared by canvas, outline, search, and issue navigation
- [x] deterministic selection, focus, history, and profile-owned layout state
- [x] evidence provenance available from every rendered relationship

### Pass 3: scalable graph rendering

- [x] incremental layout and rendering for large libraries
- [x] cancellation, stale-result suppression, and bounded caches
- [x] measurable performance budgets with representative fixtures

### Pass 4: cohesive ForgeView workflow

- [x] Inspector, Issue Viewer, profile, and graph navigation converge on one selected-mod context
- [x] empty, loading, degraded-evidence, and failure states are actionable
- [x] accessibility, keyboard operation, and visual consistency acceptance

## Epic E — Repair Engine

### Pass 1: deterministic repair planning

- [ ] immutable repair plans with evidence, confidence, safety class, and preview
- [ ] no-write analysis separated from explicit execution
- [ ] profile and filesystem preconditions validated before mutation

### Pass 2: transactional execution and recovery

- [ ] atomic execution, rollback, cancellation, and interrupted-session recovery
- [ ] complete audit trail and user-visible outcome reporting
- [ ] integration with profile readiness and ForgeView provenance

### Pass 3: repair certification

- [ ] safe automatic actions explicitly allowlisted
- [ ] destructive or uncertain actions require confirmation
- [ ] runtime-evidence repair hooks validated by Companion Gate 3

## Epic F — Productivity

### Pass 1: unified workflows

- [ ] command/search/navigation consolidation around canonical application state
- [ ] bulk profile and library operations with preview and undo

### Pass 2: responsiveness and polish

- [ ] background execution for remaining long-running work
- [ ] startup, memory, interaction, and large-library performance budgets
- [ ] accessibility and error-recovery polish

## Epic G — Release Engineering

### Pass 1: reproducible distribution

- [ ] deterministic Client and Companion artifacts with provenance and checksums
- [ ] clean install, upgrade, rollback, and uninstall validation
- [ ] version and protocol compatibility policy enforced in CI

### Pass 2: release qualification

- [ ] complete cross-repository test matrix and Companion Gate 3
- [ ] recovery, security, performance, and soak gates
- [ ] user, developer, troubleshooting, and release documentation complete

## Definition of done

An epic is complete only when its user-facing workflow functions end to end, architectural ownership is enforced, automated and applicable real-runtime tests pass, performance stays within documented budgets, failure/recovery behavior is verified, and documentation matches the shipped behavior.

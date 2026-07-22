# Unreleased — Epic C Pass 2 Atomic Profile Editing

## Added

- Added immutable profile edit drafts with explicit added, removed, and reordered package change sets.
- Added validation for locked profiles, empty orders, missing Core, duplicate entries, missing mods, and ambiguous package IDs.
- Added stale-workspace fingerprint rejection before any profile files are changed.
- Added a canonical commit path through the existing backed-up and rollback-safe profile persistence service.
- Added behavioral and static acceptance coverage for edit preview and validation.

## Changed

- Registered the profile edit service in canonical application composition.
- Advanced the client version to `2.2.0-alpha.62`.

# Previous Unreleased — Epic C Pass 1 Library/Profile Projection

## Added

- Added a canonical, deterministic projection joining the full installed library with every RimForge profile.
- Added explicit installed, missing, and ambiguous resolution for active profile entries.
- Added per-profile inactive installed inventory, duplicate package-ID reporting, and case-insensitive lookup.
- Added a stable workspace fingerprint independent of discovery order and observation time.
- Added execution and static acceptance coverage for the Epic C foundation.

## Changed

- Registered the library/profile projection in canonical application composition.
- Advanced the client version to `2.2.0-alpha.61`.

# Previous Unreleased — Three-Repository Ownership Correction

## Changed

- Separated the RimWorld Agent, Companion mod assets, and Companion packaging from the Client repository.
- Moved runtime harnesses, protocol tests, fixture mods, expected evidence, and runtime acceptance documentation into `RimForge.Companion.TestSuite`.
- Retained the desktop Companion Host/bootstrapper, protocol authority, evidence ingestion, analysis, and all data presentation in `RimForge.Client`.
- Added independent solutions, build configuration, documentation, and Git roots for Client, Companion, and TestSuite.
- Added a permanent repository-boundary regression gate.
- Fixed the Agent runtime-evidence publisher to use the correct typed protocol delegate.
- Advanced all three repositories to `2.2.0-alpha.60`.

# Previous Unreleased — Epic B Pass 5 Unified Evidence Analysis

## Added

- Added unified Forge Evidence input to the canonical analysis request and deterministic fingerprint.
- Added canonical classification for runtime performance, integration, conflict, compatibility, and replacement findings.
- Added stable source identities and non-mandatory observed-conflict relationships for evidence-backed findings.
- Added behavioral coverage for severity mapping, healthy-assessment suppression, deterministic evidence ordering, explanations, and Issue Viewer projection.

## Changed

- Wired Forge DNA, workspace refresh, library projection, and Native Forge to the current evidence generation.
- Moved runtime and compatibility interpretation out of the WPF client and into the native analysis engine.
- Issue Viewer now consumes only the canonical snapshot instead of concatenating parallel UI-local issue projections.
- Completed Epic B Pass 5 and advanced the repository version to `2.2.0-alpha.59`.

# Previous Unreleased — Epic B Pass 4 Analysis Explainability

## Added

- Added a canonical full-library overview covering scope, health, severity, relationship, cycle, and load-order status.
- Added case-insensitive per-mod explanations combining findings, impact, relationship rationale, repair recommendations, profile membership, and placement decisions.
- Added stable plain-language narratives for result-level and mod-level analysis presentation.
- Added behavioral and architecture coverage for explainability completeness, determinism, and cached-result ownership.

## Changed

- Incremental cache entries now retain the explanation catalog atomically with their canonical snapshot and diagnostics.
- Downstream consumers can query one engine-owned rationale projection instead of reconstructing interpretations independently.
- Completed Epic B Pass 4 and advanced the repository version to `2.2.0-alpha.58`.

# Previous Unreleased — Epic B Pass 3 Incremental Analysis Reuse

## Added

- Added bounded least-recently-used analysis result caching keyed by the canonical input fingerprint.
- Added explicit use, refresh, and bypass cache policies plus hit, miss, refreshed, and bypassed provenance.
- Added targeted and full cache invalidation through the analysis engine contract.
- Added behavioral coverage for result reuse, forced refresh, isolated bypass, and targeted invalidation.

## Changed

- Expanded fingerprint coverage to Forge Evidence badges, capabilities, and notable findings used by classification.
- Cache hits now publish only cache-lookup and completion stages, accurately reflecting work performed.
- Failed and canceled analysis runs remain outside the cache store boundary.
- Completed Epic B Pass 3 and advanced the repository version to `2.2.0-alpha.57`.

# Previous Unreleased — Epic B Pass 2 Observable Analysis Pipeline

## Added

- Added typed analysis stages with live progress and per-stage execution timings.
- Added complete structured diagnostic projection with owning and related package context.
- Added regression coverage for stage order, diagnostic completeness, lock-sensitive fingerprints, and cancellation.

## Changed

- Stabilized relationship, dependency-map, dependent-map, and cycle output ordering.
- Expanded the input fingerprint to cover dependency and ordering metadata, compatibility inputs, active order, target version, and user locks.
- Added cancellation checkpoints to graph traversal and deterministic load-order planning.
- Completed Epic B Pass 2 and advanced the repository version to `2.2.0-alpha.56`.

# Previous Unreleased — Epic B Pass 1 Analysis Execution Foundation

## Added

- Added explicit `ModAnalysisRequest`, `ModAnalysisResult`, progress, diagnostic, and execution-metrics contracts.
- Added reproducible SHA-256 analysis input fingerprints and separate installed-library versus active-profile scope metrics.
- Added execution coverage for full-library analysis, discovery-order determinism, and cancellation.

## Changed

- Added the canonical asynchronous, cancellable `IModAnalysisEngine.AnalyzeAsync` boundary while retaining the synchronous compatibility entry point for existing non-interactive callers.
- Normalized installed-library input order before analysis so relationships, issues, sorting, and fingerprints do not depend on discovery order.
- Migrated Forge DNA orchestration to the canonical analysis execution contract.
- Completed Epic B Pass 1 and advanced the repository version to `2.2.0-alpha.55`.

# Previous Unreleased — Epic A Pass 3 Resilience Completion

## Added

- Added coordinated platform self-validation for required configuration and writable workspace, cache, session, and diagnostic storage.
- Added durable active-run markers that identify interrupted application sessions and are removed only after orderly service shutdown.
- Added signed update manifests with RSA-PSS verification, pinned channel trust, package SHA-256 verification, safe relative-path validation, transactional staging, rollback capture, and atomic rollback restoration.
- Added protected-state manifests and install-boundary enforcement covering settings, profiles, output, caches, sessions, and diagnostics.
- Added execution and architecture certification for every Epic A Pass 3 resilience boundary.

## Changed

- Inserted health validation, recovery detection, and state capture into coordinated application startup.
- Integrated clean-run completion into application service shutdown while preserving the marker when startup or shutdown is interrupted.
- Completed Epic A Pass 3 and advanced the repository version to `2.2.0-alpha.54`.

# Previous Unreleased — Epic A Pass 2 Unified Evidence Completion

## Added

- Added `IForgeEvidenceBus` as the authoritative immutable-generation publication boundary, including publication reasons and monotonic state.
- Added the public `IForgeEvidenceStore` durable-storage boundary with atomic replacement, last-known-good backup, corrupt-file quarantine, and recovery status.
- Added Pass 2 certification covering producers, aggregation, publication, durability, source coverage, and removal of parallel runtime graph projection.

## Changed

- Migrated the main client from service-owned snapshot events to the shared evidence bus.
- Routed runtime evidence changes back through the evidence invalidation and producer pipeline.
- Projected runtime and compatibility relationships into ForgeView from unified evidence contributions instead of directly reading the runtime store.
- Completed every Epic A Pass 2 roadmap outcome and advanced the repository version to `2.2.0-alpha.53`.

# Previous Unreleased — Epic A Pass 2 Unified Evidence Producers

## Changed

- Replaced the legacy Forge Evidence contributor abstraction with the single `IForgeEvidenceProducer` contract across the core model, ingestion pipeline, built-in producers, runtime integration, diagnostics, tests, and documentation.
- Renamed producer implementations and source files consistently and removed the obsolete contributor contract and terminology.
- Preserved the existing persisted JSON diagnostics field name so evidence snapshots written before the contract migration remain readable.
- Advanced the repository version to `2.2.0-alpha.52`.

# Previous Unreleased — Epic A Pass 1 Infrastructure Completion

## Added

- Added a shared hosted-background-work lifecycle for named, concurrent service loops with duplicate prevention, state publication, cancellation, and deterministic shutdown.
- Added execution and architecture boundary coverage for hosted work and centralized path ownership.

## Changed

- Migrated the runtime sensor pipe listener away from its isolated `Task.Run` loop and removed duplicate fire-and-forget startup from application composition.
- Routed texture exports, texture scratch files, repository discovery, curated database discovery, Companion state, Steam metadata cache, and Agent session spooling through explicit path boundaries.
- Removed the legacy Forge Session entry point that silently treated the process working directory as its workspace.
- Completed Epic A Pass 1 and advanced the repository version to `2.2.0-alpha.51`.

# Previous Unreleased — Epic 1 Pass 5: Forge Evidence Platform Completion

- Added deterministic Forge Evidence query contracts with subject, relationship, type, source, confidence, observation-window, text, paging, and facet filters.
- Expanded the immutable evidence index with reverse relationship lookup and subject/type metrics.
- Added `IForgeEvidenceQueryService` for indexed retrieval, stable result ordering, facets, query timing, and platform diagnostics.
- Integrated unified Forge Evidence into Mod Inspector selection context and Issue Viewer compatibility/replacement projections.
- Added generation diagnostics covering contributions, subjects, relationships, producer diagnostics, invalidations, watchers, source distribution, and confidence distribution.
- Added Epic 1 completion, serialization, protocol, and architecture certification contracts.
- Added `EPIC1_FINAL_VALIDATION.md` with Windows unpack, clean, restore, build, full-suite, startup-smoke, Companion, and failure-capture commands.
- Epic 1 implementation is complete; executable build/test certification and resulting regression hotfixes are the next final-pass activity.

## Epic 1 Pass 3 — Incremental Evidence Refresh

- Added sequence-safe evidence invalidation journaling so changes arriving during a refresh are never lost.
- Added configurable watcher debounce, buffer sizing, scan concurrency, and transient-file filtering.
- Added application-level automatic refresh scheduling over the existing serialized evidence service.
- Reconciled producer source projections to remove stale evidence after declarations or installed mods are removed.
- Added generation metrics for captured invalidations, reconciled contributions, and active watchers.
- Added architecture documentation and Epic certification coverage for incremental refresh.

# Unreleased — Epic 1 Forge Evidence Ingestion Pipeline

## Added

- Added a deterministic `ForgeEvidencePipeline` with explicit producer ordering, cancellation, transient retry handling, producer diagnostics, and atomic transaction boundaries.
- Added first-party static metadata and dependency metadata producers for inventory, capability, parse-error, dependency, load-order, and incompatibility evidence.
- Added `ForgeEvidenceIndex` for read-only subject, evidence-type, source-kind, and bidirectional relationship queries.
- Added producer collection context, progress, result, diagnostic, and snapshot descriptor contracts.
- Added execution coverage for producer orchestration, deterministic SHA-256 evidence IDs, relationship projection, and query indexes.
- Added the Forge Evidence ingestion architecture document.

## Changed

- Advanced the Forge Evidence schema to version 2.
- Routed Forge refresh contribution collection through the unified ingestion pipeline.
- Persisted producer diagnostics with snapshots while retaining version 1 snapshot compatibility.
- Replaced nondeterministic generated evidence GUIDs with stable SHA-256 identifiers.
- Centralized batch and contribution validation in the ingestion pipeline.

# Unreleased — External Profile Change Detection

## Added

- Added a content-aware `ModsConfig.xml` monitor with SHA-256 baselines, debounced file-system notifications, rename/delete handling, and transient file-replacement tolerance.
- Added an explicit acknowledgement API so successful RimForge writes can advance the baseline without being reported as external edits.
- Added application-owned monitor lifetime and a focused regression contract.

﻿# Test Suite Reliability Pass

## Added

- Added `Tests/Run-AllTests.ps1` as the canonical Windows PowerShell 5.1-compatible repository test entry point.
- Added deterministic build, per-test execution, startup smoke validation, nonzero failure exit behavior, and `Reports/AllTests.json` output.
- Added `TestRunnerContract-Test.ps1` to prevent orchestration scripts from being recursively executed and to ensure success is never printed after a failed suite.

## Changed

- Excluded aggregate and focused recovery runners from the individual test discovery set.
- Replaced the copy/paste full-suite loop with a single truthful command suitable for local validation and CI.

## Focused Error Recovery Pass 3

- Corrected stale test paths for `LoadOrderRules.cs` and `NativeForgeRunner.cs`.
- Aligned the Commit 11 readiness regression with the current compatibility gate while retaining historical test-file coverage.
- Updated unified-search completion validation to inspect the current search presentation state owner.
- Added `Tests/Run-FocusedErrorRecovery3.ps1`.


### Fixed

- Corrected `SortTransactionService` to consume `LoadOrderSaveResult.UpdatedProfile`, restoring compilation after the locked-sort transaction pass.
- Updated testing instructions to work from Windows PowerShell 5.1 when `pwsh` is not installed.

## Unreleased — locked positions and sort transactions

- Added persisted per-profile load-order locks and workspace preference state.
- Added a constraint-aware locked-position solver that rejects dependency-violating positions and reports legal alternatives.
- Added preview/apply sort transactions that leave the profile unchanged when validation or persistence fails.
- Added load-order decision provenance and Issue Viewer-compatible `UserLockConflict` findings.
- Added PowerShell contract tests for lock handling, transaction rollback, and workspace preference persistence.
## Unreleased

### Added

- Added first-class load-order decision provenance with previous/proposed positions, primary reason, rule source, confidence, mandatory/advisory status, and related package IDs.
- Added active-profile detection and assisted repair planning for required dependencies that are installed but inactive.
- Added a tri-hybrid sorting regression contract covering active-profile isolation, hard/advisory rule separation, provenance publication, and Inspector projection.

### Changed

- Restricted the proposed sort graph to active-profile nodes while continuing to use the full installed library for identity and dependency discovery.
- Changed curated `Recommended` and `Experimental` relative rules from mandatory graph edges into deterministic queue preferences; only `Hard` curated rules can block or cycle a sort.
- Added explainable load-order decisions to Mod Sorter/Inspector projections, including whether a movement is required or recommended and which source produced it.
- Updated native Forge dependency counts so installed-but-inactive required dependencies are included in the dependency failure total.

### Fixed

- Replaced the legacy anvil application icon with the supplied canonical circular star badge across the executable, window chrome, taskbar identity, and compact command-menu slots.
- Reworked the Dependency Map default layout with connected-component packing, cycle-aware layered placement, repeated barycentric ordering, adaptive spacing, and deterministic Core/DLC prioritization.
- Ensured every installed official DLC receives an explicit canonical dependency edge to Core even when its metadata omits the declaration.
- Added compact-brand and Dependency Map layout regression contracts.
- Preserved every successfully discovered mod in Mod Sorter and profile list projections when Forge analysis is unavailable or fails.
- Published authoritative Forge Evidence before downstream profile-scoped analysis so Dependency Map and Inspector consumers retain the latest valid generation.
- Changed explicit Forge runs to force a fresh evidence generation while retaining incremental caching for background intelligence refreshes.
- Added an authoritative Forge projection regression contract.

# Unreleased — Repository consolidation

## Documentation

- Replaced the pass-log roadmap with a capability-driven 1.0 roadmap and explicit release criteria.
- Added canonical sorting-engine, curated-database, Runtime Companion, release-plan, and development-history documents.
- Rewrote README, architecture, contribution, competitive-context, and known-issues documentation around the current product model.
- Archived the previous pass-oriented roadmap for historical reference.
- Standardized the product position as **The RimWorld Modpack Engineering Platform** and the product-facing graph name as **Dependency Map**.

# Chrome+ — Compact Navigation & Unified Search

## Added

- Added a compact sandwich navigation menu to the top-left command bar, backed by the existing workspace navigation coordinator.
- Added viewport-aware idle search examples for Mod Sorter, Issue Viewer, ForgeView, Texture Tools, Settings, and Console.
- Added feature aliases and feature-first search ranking, including direct navigation to Texture Conversion Tools, Issue Viewer, ForgeView, Console, and Settings.
- Added a complete no-results search dropdown and single-result selection synchronization across Mod Sorter and Mod Inspector.
- Added `ChromePlusTopBarUnifiedSearch-Test.ps1` covering shell, search, ForgeView, identity, and binding-safety contracts.

## Changed

- Left-aligned every sandwich-menu destination through the shared command-button alignment contract, replaced the transient location subtitle with the canonical RimForge slogan, and reserved a consistent icon column using the approved logo as the temporary section icon.
- Removed the hosted left navigation rail and reclaimed its application width.
- Centered the search host against the complete command bar so variable application-status content cannot move it.
- Wrapped Reforge and Undo in the same raised control family as Back/Forward while preserving the approved arrow presentation.
- Joined search and result chrome: identical widths, square shared corners while open, rounded outer corners, and no visual gap.
- Replaced the generic result glyph for mods with canonical source identity and the shared health anvil.
- Extended the authoritative search query into ForgeView Graph and Outline node filtering while preserving the shared evidence model.

## Fixed

- Prevented a retained search query or unavailable profile projection from making a successfully discovered library appear empty: search now has an explicit clear control and Escape recovery, Forge republishes both list projections, and Mod Sorter/ForgeView fall back to the installed library when no profile is available.
- Corrected the unified search editor's vertically clipped text by removing template-conflicting vertical inset, and prevented deferred or inactive text selection from remaining visible after the field loses focus.
- Moved the shared visibility converter to application scope so independently compiled feature views, including the unified command bar, can resolve it during startup instead of failing after a clean build.
- Aligned idle search text with the actual caret start position and removed it whenever the field is focused.
- Prevented read-only search flyout state from using an unsafe TwoWay WPF binding.
- Corrected Mod Sorter feature navigation so it targets the load-order workspace rather than the Issue Viewer anchor.

# Pass 45 — Feature Completion

## Added

- Recreated `MainWindow.FeatureTasks.cs` with common generic/non-generic execution helpers and shared cancellation.
- Added discovery detail and current-file fields to the background progress contract, plus computed percentage, counts, and live elapsed-time projection.
- Added a shared global feature-progress surface and complete Texture Tools lifecycle detail.
- Added `Pass45TextureExecutionIntegration-Test.ps1`, a package-free executable background-lifecycle harness, and aggregate-gate coverage.

## Changed

- Centralized worker scheduling in `BackgroundTaskService`; no feature or analysis implementation retains an isolated `Task.Run` pipeline.
- Migrated library scan/projection, Forge Evidence, native Forge analysis, shared intelligence, Texture Tools, profile operations, repair persistence, Inspector metadata, Console history, settings load/discovery/save, and launch preparation to `RunFeatureTaskAsync`.
- Bridged common feature cancellation into the coalesced Shared Evidence generation while preserving per-caller cancellation isolation, so explicit feature cancellation cannot leave an orphan scan that later publishes.
- Standardized lifecycle logging, status transitions, failure notification, busy/idle recovery, and cancellation races.
- Completed Texture Tool presets with BC7 as the default, explicit BC3 compatibility and BC1 performance modes, per-file analysis/conversion/revert reporting, atomic manifest replacement/corruption recovery, and texconv process termination on cancellation.
- Redesigned ForgeView edge presentation with four-side node routing, directional arrowheads, selection-relative orange/cyan semantics, relationship-specific solid/dashed/dotted patterns, and implicit Core-to-normal-mod edge suppression while retaining Core-to-DLC and DLC dependency links.
- Completed Mod Sorter multi-select dragging across activation, reordering, and deactivation; extended-selection gestures now survive drag initiation, group mutations roll back on failure, destination selection is retained, and a successful group move creates one undo unit.
- Replaced remaining raw WPF dependency confirmation dialogs with the RimForge dialog framework.

## Fixed

- Prevented late progress from overwriting the Cancelling state.
- Prevented an operation that returns after cancellation from being reported as Completed.
- Prevented redirected texconv output from deadlocking and ensured the process tree is stopped on cancellation.
- Removed the remaining UI-thread sync-over-async analysis fallbacks and coalesced stale profile analysis refreshes.
- Prevented reported task cancellation/failure from escaping WPF `async void` command dispatch.
- Removed the duplicate Player.log restart after launch; the launch pipeline now opens the already-active stream.
- Corrected corrupted bullet, arrow, and em-dash glyphs in dependency dialogs.
- Corrected DDS codec fallback and explicit selection so BC7 is the true default, BC3/DXT5 always selects BC3, and BC1/DXT1 always selects BC1.

# Pass 48 — Pre-Polish Feature Completion Epic

## Checkpoint 48.1 — Shared Forge execution contract

- Added immediate cache-validation progress before evidence preparation.
- Projected per-mod evidence names, paths, counts, and percentages through the shared background-task framework.
- Projected native Forge analysis phases and technical details through the same framework.
- Added separate evidence, analysis, and total pipeline timing records.
- Preserved incremental evidence reuse and cancellation.
- Added the Pass 48 foundation gate and aggregate coverage.

- Restored the revised first-boot onboarding overlay once for existing installations through a versioned guide revision, and returned native Forge evidence analysis to incremental cache reuse instead of forcing a full-library rescan on every ignition.

### Pass 47B.2 — ForgeView Graph Query & Filtering Foundation

- Added health and relationship filters to the ForgeView graph workspace.
- Added selected-path isolation for focused dependency investigation.
- Added live filtered topology counts without rebuilding Shared Evidence.
- Persisted graph-query state in each profile-owned ForgeView layout document.
- Added a dedicated regression gate and aggregate-gate coverage.


- Fixed a regression where Ignite the Forge skipped deep evidence scanning, completed in fractions of a second, and left technology badges empty. Forge now performs an authoritative forced evidence generation before analysis while startup intelligence remains cache-aware.
### Pass 47B.1 — ForgeView Profile-Owned Forged Layout Editing

- Added an opt-in ForgeView Edit Layout mode with direct node movement.
- Added profile-owned persisted node coordinates, pins, zoom, and pan.
- Added brass-rivet pin indicators and selected-node pin/unpin controls.
- Added deterministic Reset Layout behavior that returns to the Forged Layout.
- Added atomic best-effort `ForgeView.layout.json` persistence and a dedicated regression gate.


## Pass 47A.3 — ForgeView Interactive Intelligence Integration
- Added live hover context and explicit Inspector synchronization reporting.
- Connected published graph intelligence to focused dependency, dependent, conflict, and cycle state.

# Commit 12 — Pass 46A.4 Scheduler & Watcher Reliability

- Coalesced identical concurrent Shared Evidence refresh requests into a single generation.
- Preserved serialized execution for distinct refresh requests.
- Decoupled individual caller cancellation from the shared underlying generation.
- Added explicit generation cancellation through `CancelCurrent`.
- Added per-mod file watcher debounce and immediate watcher-overflow invalidation.
- Added scheduler, debounce, and overflow metrics to the UI report.
- Added Pass46A4 scheduler/watcher reliability validation.

# Commit 12 — Pass 46A.3 Persistent Evidence Cache and Incremental Rescan

- Added durable Forge Evidence cache validation with schema, target-version, and per-mod fingerprint checks.
- Removed blind previous-generation reuse so every refresh verifies the installed mod tree before reusing evidence.
- Added atomic write-through cache replacement with temporary-file cleanup.
- Added corrupt-cache quarantine and automatic recovery.
- Added cache miss and corrupt recovery metrics to the shared evidence generation report.
- Added Pass46A3PersistentEvidenceCache-Test and expanded the Commit 12 completion gate.

# Commit 12 — Pass 46A.2 Shared Evidence Consumer Migration

- Replaced the independent background evidence enrichment path with `IForgeEvidenceService.RefreshAsync`.
- Published evidence generations are applied once to live mod records before dependent UI projections rebuild.
- Issue Viewer, Mod Sorter, Inspector bindings, profile load-order health, and Launch Readiness now converge on the same published generation.
- Added file-watch invalidation reporting without performing analysis on watcher callbacks.
- Added generation metrics to `IntelligenceMetrics.json`.
- Added Pass46A2 and expanded the Commit 12 completion gate.

# Commit 12 — Shared Evidence Engine foundation

### Added
- `ForgeEvidenceService`, `IForgeEvidenceService`, immutable evidence snapshots, scan metrics, invalidation reasons, background coordination, cancellation, and file watching.
- Pass 46A.1 Shared Evidence foundation validation.

### Changed
- Application composition now owns and disposes the Shared Evidence service.

- Finalized Commit 11 launch-readiness scoping so reviews use the exact profile being launched while remaining cache-only; hardened ignore persistence against partial in-memory updates and temporary-file residue.
## Commit 11 remaining stabilization — Pass 45A.16
- Launch Readiness now consumes the cached ignored-aware Issue Viewer projection, so ignored findings do not reappear in advisory error/warning totals.
- Ignore-state persistence now uses temporary-file replacement and no-op writes are skipped.
- Ignore/Unignore refreshes load-order health-anvil projections immediately.
- Fix All validates automatic-ready plans and performs the shared canonical reorder only once.
- Commit11Completion-Test now includes Passes 45A.14–45A.16.

## Commit 11 load-order knowledge engine — Pass 45A.13
- Replaced the single hardcoded category classifier with a JSON-backed category-first policy pack and built-in fallback.
- Added Hard, Recommended, and Experimental rule confidence levels.
- Added multi-category evidence, candidate-band tracking, and per-mod classification explanations.
- Applied curated relative-order rules as graph edges while preserving required dependencies and mod-declared ordering.
- Preserved the modernized top block, EdB Prepare Carefully placement, and RocketMan / Missile Girl bottom anchors.
- Added Pass 45A.13 and included it in the Commit 11 completion gate.


## Commit 11 final stabilization — Pass 45A.9
- Completed the full-window overlay workspace: page content scrolls behind the fixed command and launch bars with safe content insets.
- Unified the Mod Sorter and Issue Viewer surface and removed the legacy Issue Viewer percentage-height floor.
- Preserved ForgeView as a dedicated Chrome+ card and placed Selection Context beneath the graph/map.
- Completed the Texture Conversion Tools UI/backend integration, queue, conversion, validation, cancellation, backup, and activity reporting paths.
- Preserved the top-level Settings card.
- Repaired Pass 45A.6 component-aware validation, restored the missing Pass 45A.7 gate, and added one aggregate Commit 11 completion test.
## 2.2.0-alpha.45a.8 — Commit 11 Texture Tools Backend & ForgeView Context Layout

- Implemented an end-to-end Texture Conversion Tools backend with profile, selected-mod, and folder discovery.
- Added a live conversion queue, cancellation, progress, per-file state, output selection, resizing, backups, skip-current behavior, and Console activity reporting.
- Added functional PNG, JPEG, BMP, and TIFF conversion through WPF codecs.
- Added DDS header validation and optional DirectXTex/texconv-based DDS export when texconv.exe is installed.
- Moved ForgeView Selection Context below the graph/map so the map owns the full card width.
- Preserved the overlay viewport hotfix and Commit 11 unified workspace surfaces.

## 2.2.0-alpha.45a.6 — Commit 11 Unified Workspace Surface Completion

- Extended the unified darkest Mod Sorter / Issue Viewer card behind the viewport-locked lists and issue workflow.
- Removed the forced Issue Viewer percentage-height floor that left unused space beneath short issue sets.
- Added an explicit ForgeView Chrome+ parent card with an inset graph workspace surface.
- Kept Engineering Metrics directly below ForgeView as its compact footer strip.
- Replaced the Texture Conversion Tools placeholder with a complete visible workspace shell for analysis, queueing, presets, validation, backup recovery, conversion settings, progress, and execution.
- Added an explicit top-level Settings Chrome+ card while preserving its internal tab and card layout.
- Preserved exact viewport locking, internal scrolling, virtualization, selection, and drag/drop for both Mod Sorter lists.

## 2.2.0-alpha.45a.5 — Commit 11 Completion: Strict Sorter Viewport Lock

- Locked the inactive installed-mods list and active profile load-order list to one exact visible workspace viewport.
- Prevented either list from growing with item count by capping both the Mod Sorter host and feature view.
- Preserved independent internal scrolling, virtualization, drag/drop, selection, and visible scrollbars.
- Kept the statistics bar between Mod Sorter and Issue Viewer, ForgeView on its Chrome+ card, and Engineering Metrics directly beneath ForgeView.
- Strengthened the Pass 45A.4 acceptance gate for exact viewport height, maximum-height caps, clipping, and shrinkable list rows.

# Changelog

## 2.2.0-alpha.50 — 2026-07-21

- Added shared `RuntimeHealth`, `HealthStatus`, `DiagnosticEvent`, `PerformanceMeasurement`, `ILogSink`, and `ISessionLog` contracts.
- Added bounded in-memory diagnostics, durable JSONL output, health publication, and disposable performance scopes.
- Bridged the existing `RimForgeLogger` stream into the shared structured pipeline.
- Added Forge Session correlation and per-session `session-log.jsonl` persistence.
- Integrated Companion Host process health and the main-client global health projection.
- Added execution coverage for structured events, logger bridging, timing, health, global persistence, and session persistence.

## 2.2.0-alpha.49 — 2026-07-21

- Replaced the Bridge Host console loop with lifecycle-managed Companion Host components.
- Added validated named-pipe IPC, durable session bridging, Player.log tailing, RimWorld process monitoring, and health reporting.
- Added a RimForge-side process controller with session-bound arguments and deterministic shutdown.
- Forced the external host to run without a visible console and projected all host state into the main RimForge UI.
- Added integration coverage for host health, malformed-envelope rejection, accepted-envelope persistence, and clean cancellation.

## 2.2.0-alpha.48 — 2026-07-21

- Added centralized platform discovery contracts for Steam libraries, RimWorld installations, RimWorld user data, and RimForge workspace paths.
- Added one immutable discovery snapshot with deterministic preferred-installation selection and multi-library Workshop projection.
- Migrated ModsConfig and Player.log consumers away from duplicated environment-folder probing.
- Composed one shared discovery/workspace service graph for application services.
- Added isolated filesystem fixtures for Steam `libraryfolders.vdf`, app manifest, install directory, Workshop, user-data, and workspace resolution.

## 2.2.0-alpha.47 — 2026-07-21

- Added the durable Epic A Forge Session framework with stable session identities and explicit lifecycle states.
- Captured workspace, profile, game version, mod count, runtime status, progress, errors, and timestamps in every session.
- Added manager-owned cancellation, concurrent-session rejection, atomic per-session persistence, current-session recovery, and interrupted-session detection.
- Integrated the manager with the existing Forge pipeline, WPF snapshot events, application shutdown, and runtime-data path layout.
- Added execution coverage for lifecycle, metadata, progress, cancellation, concurrency, persistence, restore, and interruption recovery.

## 2.2.0-alpha.46 — 2026-07-21

- Consolidated the desktop, Companion, and runtime test-suite repositories into one canonical tree.
- Unified build metadata, package versions, solution membership, and product versioning.
- Merged desktop evidence and runtime inventory contracts into the single shared protocol project.
- Removed generated build outputs, hotfix backups, obsolete nested repositories, the retired PowerShell bridge, legacy pipeline modules, and superseded milestone documentation.
- Replaced build, packaging, launch, repository-layout, and documentation entry points for the consolidated workflow.

## Unreleased — Runtime Startup Replay Reliability

- Player.log startup replay now preserves a valid final line even when RimWorld has not newline-terminated it yet.
- Added bounded replay telemetry covering file size, replayed entries, warning/error counts, and partial-final-line preservation.
- Surfaced replay completion in the RimForge activity stream and added a regression contract for subscription lifecycle and replay bounds.


## 2.2.0-alpha.45a.3 — Active Workspace Navigation & Unified Cards

### Added
- Viewport-aware active navigation using 51% activation and 45% retention thresholds.
- Dynamic parent/child location pill with click-to-jump navigation.
- Child workspace anchors for Mod Sorter, Issue Viewer, engineering metrics, ForgeView, Texture Tools, Settings, and Console.

### Changed
- Issue Viewer is now a child of Mod Sorter instead of a top-level navigation destination.
- Mod Sorter and Issue Viewer now share one unified Chrome+ workspace card.
- Settings, Console, and Texture Conversion Tools now use unified top-level workspace cards.
- Console mode tabs now live inside the Console card.

## 2.2.0-alpha.45a — Commit 11: Chrome+ Foundations & Layout

### Added
- Chrome+ active-state treatment for ForgeView Graph and Outline mode selectors.
- Compact Issue Viewer success state when the current scope has no issues.
- Pass 45A structural acceptance gate.

### Changed
- Moved the ForgeView toolbar beneath the page header and aligned it to the left edge of the visualization.
- Preserved left-aligned hierarchical Outline content with horizontal overflow for deep trees.
- Made the Issue Viewer content-reactive: small result sets size to content, while large result sets cap at the viewport limit and scroll internally.
- Removed the fixed Issue Viewer workspace minimum height that reserved unused main-page space.

## 2.2.0-alpha.42 — Unified Search & Discovery

- Added ranked command-bar discovery results for mods, issues, and workstation destinations.
- Added keyboard and mouse result activation with selection-preserving navigation.
- Reused the authoritative structured-query and mod-filtering contracts rather than duplicating search logic.
- Added Pass 42 acceptance coverage and implementation documentation.

### 2.2.0-alpha.41 — UI Consolidation & Polish
- Hotfix: wired the visible Ctrl+F command-bar affordance to a shell-wide shortcut that focuses the global search box and selects its current contents.

- Completed the UI Consolidation & Feature Decomposition epic.
- Added semantic application-scoped workstation chrome resources.
- Refined the Engineering Command Bar while preserving navigation, Undo, Reforge, search, and status contracts.
- Moved Settings-owned bindable state into the Settings feature boundary.
- Added Pass 41 static architecture and XAML validation.


## Pass 40 Runtime Storage Isolation Hotfix

- Expanded `.gitignore` to exclude build, test, IDE, runtime, cache, log, export, snapshot, session, diagnostic, crash-dump, and machine-local output.
- Centralized generated application paths through `RimForgePathLayout`.
- Relative output settings now resolve beneath `%LOCALAPPDATA%\RimForge` rather than inside the Git repository.
- Absolute user-configured output paths remain supported.
- Redirected profile shell state, default profile output, caches, reports, logs, temporary files, exports, sessions, and diagnostics to the centralized runtime layout.
- Retained compatibility reads for legacy audit reports under the repository `Output` folder.
- Added runtime-storage isolation acceptance coverage and strengthened repository-hygiene requirements.


## 2.2.0-alpha.40 — Pass 40 Workstation Shell Foundation

- Completed the unified Engineering Command Bar.
- Added browser-style global navigation history with Ctrl+Left and Ctrl+Right.
- Separated Inspector Previous/Next collection navigation with Ctrl+Up and Ctrl+Down.
- Added centralized application-status projection.
- Implemented Reforge with page, profile, search, and selection preservation.
- Completed the independently scrolling tabbed Settings workspace.
- Added Pass 40 skin-readiness documentation and acceptance coverage.
- Preserved transaction-based Undo as a system independent from navigation.

## Pre-Pass-40 Repository Refinement — Delivery Artifact Separation

### Changed
- Removed `APPLY_PATCH.ps1`, `PATCH_README.md`, `RESTORE_README.md`, and `DELETE_THESE_FILES.txt` from the canonical repository.
- Patch installers and patch-specific instructions now ship only inside disposable patch archives.
- Full repository snapshots now contain only source, development tooling, tests, and maintained project documentation.

## Pre-Pass-40 repository assessment and refinement

### Added
- Root `.gitignore` policy for generated output, local IDE state, runtime diagnostics, test results, and patch-recovery material.
- Repository assessment and refinement record under `docs/development`.
- Explicit architectural decision-governance states in the engineering philosophy.

### Changed
- Strengthened repository hygiene validation to reject local-only directories and user-state files and to certify required ignore rules.

### Removed
- Generated `bin`/`obj` trees and `.patch-backups` from the refined canonical baseline.

# 2.2.0-alpha.38 — Profile Management Suite

### Added
- One authoritative profile workspace service for create, duplicate, rename, delete, import, export, restore, activation recovery, and profile comparison.
- Portable `.rfprofile.zip` backups with embedded `ModsConfig.xml`, profile metadata, and SHA-256 integrity verification.
- Transaction staging, rollback, stale-transaction cleanup, and verified destructive operations.
- Unsaved-profile switching protection and Control Center recovery actions.
- Duplicate-safe profile comparison with added, removed, and reordered mod reporting.
- A unified `ProfileManagementSuite-Test.ps1` acceptance gate.

### Changed
- Profile operations now use atomic staged writes and canonical load-order normalization through the service boundary.
- Locked and built-in profile protections are enforced below the UI layer.
- Favorite and lock state are synchronized across rename and delete operations.
- Profile activation preserves a recoverable copy of the previous native RimWorld configuration.

# 2.2.0-alpha.37 — Dependency Management Suite

### Added
- One authoritative dependency-management service for recursive activation planning, missing requirements, cycle reporting, removal impact, cascaded disable, orphan discovery, and profile dependency-health summaries.
- Persisted Automatic, Ask, and Manual orphan-cleanup modes.
- Shared dependency-health summaries in Mod Inspector and ForgeView.
- A single `DependencyManagementSuite-Test.ps1` acceptance gate.

### Changed
- Automatic dependency assistance and dependency-removal safety now consume the same engine instead of maintaining separate graph traversal logic in the UI.
- Automatic orphan cleanup is included in the original removal undo boundary.
- Legacy alpha.35/36 and Undo tests are aligned with the consolidated workflow contract.

# 2.2.0-alpha.36 — Dependency Removal Safety & Orphan Cleanup

### Added
- Dependency-aware removal blocking when active mods still rely on the selected mod group.
- One-click **Disable All** action through the Control Center for the complete impacted dependent set.
- Post-removal orphan detection with an optional **Remove Orphans** action.
- Atomic undo coverage for cascaded removals and orphan cleanup.

### Changed
- The alpha.35 dependency-intelligence regression test now validates public report contracts in the model and executable traversal behavior in the service.

# 2.2.0-alpha.35 — Dependency Intelligence Engine

### Added
- Explainable dependency intelligence with direct and transitive dependency/dependent paths.
- Removal-impact analysis, orphan-candidate detection, deduplicated graph traversal, and confidence reporting.
- Mod Inspector summaries for why a mod is enabled and what active mods would be affected by its removal.

# 2.2.0-alpha.34 — Mod Library Direct Manipulation

### Added
- Multi-selected mods now drag as one ordered group between Active and Inactive lists and within the active load order.
- Drag previews visibly lift the selected group while translucent ghost rows preserve the original positions.
- Live insertion markers show the exact prospective drop location.
- Escape cancels a drag without changing the working profile.

### Changed
- One group drop creates one undo snapshot, one dependency-closure evaluation, and one consolidated Control Center notification.
- Canonical load-order anchors and locked-profile rules reject invalid group drops before mutating the list.

# 2.2.0-alpha.33 — Control Center Workflow Integration

### Added
- Real Scan, Forge, load-order, Settings, and launch workflows now publish through the Forge Notification Bar.
- Contextual Undo, View Details, and View Log actions route through the shared notification action contract.
- Notification details can navigate directly to the RimForge Console activity feed.

### Changed
- Routine completion and failure feedback now uses the Control Center communication surface while preserving Activity Feed records and existing status bindings.
- Active background progress continues to reserve the shared surface; queued workflow notifications appear when progress releases it.

# 2.2.0-alpha.32 — Canonical Load-Order Anchors

### Added
- One hardcoded canonical load-order rules engine shared by profiles and the Mod Sorter.
- Immutable top ordering for Harmony, Core, and official DLC in release order.
- Immutable bottom ordering for RocketMan and MissileGirl.

### Changed
- First-run imports, profile saves, workspace creation, Auto-Sort, drag/drop, and undo restoration normalize through the same rules.
- Canonical anchors cannot be manually dragged, disabled by drag/drop, or used as insertion targets.

## 2.2.0-alpha.31 — Phase 2 Pass 31: Automatic Dependency Assistance

- Added Automatic, Ask, and Manual dependency-assistance modes with Automatic as the default.
- Added recursive installed-dependency closure when activating mods from the Mod Sorter.
- Groups dependency activation with the originating user action as one undoable load-order change.
- Reports automatic additions through the Forge Notification Bar with an Undo action.
- Reports required dependencies that are not installed through a non-modal warning.
- Persists dependency-assistance mode in Config.json and exposes it in Settings.
- Added regression coverage for settings, recursive resolution, notification, undo, and activation integration.

## 2.2.0-alpha.30 — Phase 2 Pass 30: Control Center Notification Foundation

- Added the centralized non-modal notification service and priority queue.
- Added Information, Success, Warning, and Error notification severities.
- Reserved the shared surface above the Control Center for active background/Forge progress before queued notifications.
- Added contextual notification action contracts and event-bus action invocation.
- Added the first Forge Notification Bar surface above the renamed Control Center host.
- Added regression coverage for composition, queueing, priority, progress reservation, actions, and non-modal presentation.

## 2.2.0-alpha.28 — Phase 1 Pass 28: First-Run Profile Safety

- Added automatic creation of an editable `My First Profile` when no editable profiles exist.
- Imports the current native RimWorld `ModsConfig.xml` into the starter profile when it is valid.
- Preserves the native configuration file and keeps the locked `Vanilla` profile as a recovery baseline.
- Falls back to Core plus detected official DLC when the native configuration is missing or invalid.
- Normalizes Core to the first load-order position.
- Updated legacy shared-context and background-task tests for typed event-bus constructor injection.
- Added first-run profile safety regression coverage.

## 2.2.0-alpha.27 — Phase 1 Pass 27: Typed Application Event Bus

- Added one typed process-local application event bus for cross-feature notifications.
- Added stable events for search, selection, profile workspace, navigation, Forge session, background tasks, library, issues, and settings.
- Updated shared contexts and runtime services to publish typed events while preserving their existing domain events for compatibility.
- Migrated MainWindow coordination for search, navigation, Forge session, and background-task changes to disposable event-bus subscriptions.
- Added lifecycle, composition, publisher, and subscription regression coverage.

## 2.2.0-alpha.26 — Phase 1 Pass 26: Background Task Framework

- Added one application-wide foreground task service with authoritative running, cancelling, completed, cancelled, and failed lifecycle state.
- Standardized progress snapshots with stage, user-facing message, technical detail, percentage/count data, and elapsed time.
- Integrated native library scanning and Forge analysis with the shared task pipeline.
- Routed cancellation through the shared service while preserving existing scan and Forge cancellation behavior.
- Added regression coverage for service composition, lifecycle state, progress reporting, and operation integration.

## 2.2.0-alpha.25 — Phase 1 Pass 25: Undo Engine

- Added a centralized single-level undo service and wired Ctrl+Z through the unified command framework.
- Added undo support for unsaved Mod Sorter enable, disable, reorder, drag/drop, official DLC toggle, and auto-sort operations.
- Undo restores the previous in-memory load-order state without reverting to the last saved profile.
- Undo history clears on profile changes, save, rebuild, and explicit revert boundaries.

# Changelog

## 2.2.0-alpha.24 — Phase 1 Pass 24: Unified Command Framework

- Added a centralized RimForge command catalog and registry.
- Routed Ctrl+A, Ctrl+S, Delete, F2, and Escape through shared command bindings.
- Registered Ctrl+Z with a stable command identity for the Pass 25 undo engine.
- Enabled extended selection in both Mod Sorter lists.
- Removed the need for future features to wire duplicate keyboard handlers directly to views.

## 2.2.0-alpha.23 — Shared Context Infrastructure

- Added authoritative shared search and navigation contexts.
- Moved global query ownership and selection-history ownership out of `MainWindow`.
- Preserved existing profile and Forge state services as the canonical owners of those domains.
- Wired shared contexts through the application composition root.
- Added shared-context lifecycle and duplicate-state regression coverage.

## 2.2.0-alpha.22 — Launch Bar and Profile Status Polish

### Changed
- Reworked Profile Health into a compact informational status indicator instead of a button-like card.
- Show and Fix actions appear only when the active profile has issues.
- Grouped load-order actions under a clear `LOAD ORDER` label.
- Grouped Ignite, Refresh, and Launch actions under a clear `FORGE` label.
- Preserved all existing routed commands, bindings, and one-click actions.

### Added
- `Tests/LaunchBarProfileStatusPolish-Test.ps1` to protect the approved grouping and compact-health structure.

### Validation required
- Run the Launch Bar polish regression test.
- Build and launch on Windows.
- Verify healthy, warning, and error profile states.


## 2.2.0-alpha.19 — Mod Sorter / ForgeView Selection Sync

- Selecting a mod in either Mod Sorter list now focuses matching incoming and outgoing ForgeView relationships.
- Non-connected relationships fade while the selected mod remains active.
- Added focused-relationship counts and a synchronized selection context panel.
- Search filtering remains composable with selection focus.
- Added regression coverage for cross-feature selection synchronization.


## 2.2.0-alpha.18 — Unified Search Completion

- Completed the hybrid search language with identity-only plain text, field aliases, repeated-field OR semantics, cross-field AND semantics, explicit AND/OR/NOT, parentheses, negation prefixes, wildcards, quoted values, version comparisons, issue-count comparisons, inline filter chips, suggestions, and extensible placeholders for profile/favorite registration.
- Updated all search-aware views to evaluate the same expression tree.
- Added completed-search regression coverage and aligned the original foundation test with the finalized behavior.
# Changelog

## 2.2.0-alpha.16 — Unified Search Presentation, Pass 2

### Added
- Live filtered-result counts for Active mods, Inactive mods, Issue Viewer entries, and ForgeView relationships.
- A compact global search summary below the shared search field.
- Visible match emphasis in Mod Sorter, Issue Viewer, and ForgeView relationship rows.
- `Tests/UnifiedSearchPresentation-Test.ps1` regression coverage.

### Changed
- Mod Sorter headers now show `matching of total` counts while a search is active.
- Issue Viewer reports the number of matching issues without changing its existing severity/category grouping.
- ForgeView exposes a dedicated search-match metric while retaining graph-wide node, relationship, and cycle totals.

### Validation required
- Run `Tests/UnifiedSearchPresentation-Test.ps1` and `Tests/UnifiedSearchFoundation-Test.ps1`.
- Build and launch on Windows.
- Confirm search counts and emphasis update immediately while typing and return to normal when the query is cleared.

## 2.2.0-alpha.15 — Unified Search Foundation, Pass 1

### Added
- Shared search-aware collection views for both Active and Inactive Mod Sorter lists.
- Issue Viewer filtering through the global search context.
- Source-type and evidence-badge search support through the central mod filtering service.
- Multiple-token search where every token must match the same result.
- `Tests/UnifiedSearchFoundation-Test.ps1` regression coverage.

### Changed
- The global search field now refreshes the Mod Sorter, Issue Viewer, ForgeView relationships, and existing library views together as the user types.
- Mod Sorter list bindings now use filtered collection views while preserving their underlying active/inactive collections and drag-and-drop data.

### Validation required
- Run `Tests/UnifiedSearchFoundation-Test.ps1`.
- Build the solution on Windows.
- Search by mod name, package ID, author, source (`Steam`, `Local`), and evidence badge (`DLL`, `XML`, `Textures`).
- Confirm Active and Inactive lists update together, matching Issue Viewer cards remain visible, and ForgeView relationships filter.

## 2.2.0-alpha.14 — UI Consolidation Pass 6

- Extracted the complete Settings workspace into `Features/Settings`.
- Moved Settings-specific path discovery, persistence, validation, and routed interaction coordination out of `MainWindow.xaml.cs`.
- Reduced `MainWindow.xaml` to hosting the Settings feature boundary.
- Preserved Steam library discovery, configuration persistence, navigation behavior, and all existing bindings.
- Added Settings decomposition and resource-scope regression validation.

## 2.2.0-alpha.13 — UI Consolidation Pass 5

- Fixed the runtime startup crash caused by `DynamicResource` being used on `Style.BasedOn` in Mod Sorter scrollbar resources.
- Extracted ForgeView presentation into `Features/ForgeView/ForgeViewView.xaml`.
- Extracted Console and RimWorld Game Log presentation into `Features/Console/ConsoleView.xaml`.
- Preserved graph refresh, dependency metrics, activity output, game-log watching, clear/stop actions, auto-follow, and incremental older-log loading.
- Added runtime-safety validation rejecting dynamic resources on `Style.BasedOn`.
- Added feature-decomposition validation for ForgeView and Console.

## 2.2.0-alpha.12 — UI Consolidation Pass 4

- Extracted the complete Issue Viewer presentation into `Features/IssueViewer/IssueViewerView.xaml`.
- Moved Issue Viewer rebuild and repair-preview coordination into `Features/IssueViewer/MainWindow.IssueViewer.cs`.
- Preserved issue grouping, virtualization, Full Library scope, repair previews, repair-plan summaries, and nested smooth scrolling.
- Reduced `MainWindow.xaml` and `MainWindow.xaml.cs` ownership of feature-specific UI and behavior.
- Added architecture validation preventing Issue Viewer markup and handlers from drifting back into the shell.

## 2.2.0-alpha.10 — UI Consolidation Pass 2

## 2.2.0-alpha.11 — UI Consolidation Pass 3

- Extracted the Mod Sorter presentation into `Features/ModSorter/ModSorterView.xaml`.
- Moved Mod Sorter-specific interaction and load-order code into `Features/ModSorter/MainWindow.ModSorter.cs`.
- Reduced `MainWindow.xaml` and `MainWindow.xaml.cs` ownership of feature-specific UI and behavior.
- Preserved the approved Mod Sorter visuals, drag-and-drop behavior, virtualization, evidence badges, and empty state.
- Added architecture validation preventing Mod Sorter markup and handlers from drifting back into the shell.

- Extracted the complete Mod Inspector presentation into `Features/ModInspector/ModInspectorView.xaml` with feature-local interaction forwarding in its code-behind.
- Reduced `MainWindow.xaml` to hosting the inspector feature instead of owning its full markup.
- Centralized all production UI artwork under `src/RimForge.UI/Assets`.
- Removed the obsolete `src/RimForge.App/Assets` tree and unused legacy source-icon PNGs.
- Reorganized action, state, source, and branding assets into canonical folders and updated every runtime reference.
- Expanded the asset manifest and added architecture tests preventing assets from drifting outside the canonical root.

## 2.2.0-alpha.9 — UI Consolidation Compile & Repository Hygiene Hotfix

- Restored the `Settings_Click` shell adapter used by the Mod Sorter empty-state action.
- Removed obsolete branding concept artwork from the production repository.
- Established repository hygiene rules: production UI assets must be canonical and in use; concept artwork stays outside the repository; unused/deprecated code and assets are removed rather than archived in-tree.

## 2.2.0-alpha.8 — UI Consolidation Pass 1

- Began the feature-oriented UI decomposition epic.
- Extracted the complete Navigation Rail markup and presentation behavior from `MainWindow` into `Features/Navigation`.
- Moved launch-specific event handling and Launch Readiness orchestration into `Features/LaunchBar/MainWindow.LaunchBar.cs`.
- Reduced direct feature ownership in `MainWindow.xaml` and `MainWindow.xaml.cs` without intentionally changing visual behavior.
- Added an architecture regression test that prevents navigation and launch logic from drifting back into the shell.

## 2.2.0-alpha.7 — Canonical Logo & Launch Readiness Consolidation

### Added
- Integrated the approved RimForge anvil logo as the canonical production brand asset.
- Added a compact PNG runtime derivative for WPF window and navigation chrome.

### Changed
- Combined Saved Profile and Workspace Revision into one Active Profile readiness card.
- Unsaved workspace changes now appear as a non-blocking WARNING on the Active Profile card.
- Missing latest Forge state now appears as WARNING rather than BLOCKED.
- Corrected PASS, WARNING, and BLOCKED card semantics.

## 2.2.0-alpha.6 — Launch Readiness Logic Hotfix

- Blocking analysis errors now produce `NOT READY FOR LAUNCH`; profiles with errors can no longer pass readiness review.
- Unsaved active-profile load-order changes now open Launch Readiness Review instead of silently cancelling the launch action.
- The review explicitly warns that only the last saved profile revision can launch.
- Added a workspace-revision readiness check to the dialog and strengthened regression coverage.

## 2.2.0-alpha.5 — Launch Readiness Compile Hotfix

- Corrected the Launch Readiness Review activity entry to use the existing `ActivitySeverity.Info` enum member.
- Restored compilation of `RimForge.App` after the alpha.4 readiness-review integration.
- No Launch Readiness Review behavior or UI styling changed.

## 2.2.0-alpha.4

- Added the first native Launch Readiness Review workflow using RimForge dialog chrome.
- Launch now summarizes the saved profile, active mod count, errors, warnings, executable readiness, and latest Forge state before starting RimWorld.
- Preserved the saved-profile-only launch rule; unsaved workspace changes are never launched automatically.
- Added explicit GO FOR LAUNCH, GO FOR LAUNCH (WITH WARNINGS), and NOT READY FOR LAUNCH states.

## 2.2.0-alpha.3 — Dialog Framework Compile Hotfix

- Corrected the Interactive Issue Viewer call to pass the native mod library and active-profile package IDs to `IssueEngine.Build`.
- Restored compilation after the Issue Engine human-name resolution contract change.
- No Issue Viewer card layout or dialog styling was changed.

## 2.2.0-alpha.2 — RimForge Dialog Framework

- Replaced the stock Windows Fix All Issues message box with a RimForge-styled repair-plan dialog.
- Migrated individual repair previews and profile-import failures to the shared Forge dialog system.
- Added reusable rich dialog content support for workflow summaries and previews.
- Added a dedicated repair/wrench vector icon to the shared icon host.
- Removed all remaining production `MessageBox.Show` calls from RimForge source.
- Added dialog-framework regression coverage.

## 2.1.0-alpha.32 — Native .NET Conversion Pass 4

- Retired the PowerShell bridge from the production solution and normal build.
- Added a runtime-boundary regression test that rejects legacy PowerShell invocation from production projects.
- Added native report parity validation for engine identity, load-order models, normalized cycles, and official-content metadata.
- Kept `RimForge.PowerShellBridge` in the repository as an explicit developer/parity utility only.

## 2.1.0-alpha.31 — Native .NET Conversion Pass 3.1

- Restored native Evidence and compatibility report generation that was accidentally dropped while merging Pass 3.
- Official Core and DLC content no longer report the expected generic-parser `Missing name` finding as a compatibility error.
- Native Forge reports now expose complete library issues separately from active-profile issues.
- Added explicit issues for mods whose load-order placement is blocked by a dependency cycle.
- Corrected and expanded native conversion validation coverage.

## 2.1.0-alpha.30 — Native .NET Conversion Pass 3

- Added centralized `IModNameResolver` / `ModNameResolver` services for consistent human-first mod names.
- Normalized package IDs before native dependency-graph construction, eliminating case-only duplicate graph nodes.
- Added native `LoadOrderPlan` and `LoadOrderEntry` models with human-readable ordered mods, blocked mods, and cycle groups.
- Native Forge reports now serialize the structured load-order plan instead of exposing only raw package-ID arrays.
- Official Core and DLC metadata no longer report expected `Missing name` parser findings as compatibility errors.
- Added native conversion Pass 3 regression coverage.

## 2.1.0-alpha.28 — Native Forge Compile Hotfix

### Fixed
- Added the missing `System.IO` import to `NativeForgeRunner`.
- Restored compilation of native Forge report directory, path, and atomic file-write operations.

## 2.1.0-alpha.27 — Native .NET Forge Conversion, Pass 1

### Added
- Added `NativeForgeRunner` as the authoritative application-facing Forge orchestrator.
- Added native atomic `NativeForgeReport.json` and `ForgeSummary.json` output.
- Added typed native Forge progress across configuration, graph analysis, profile analysis, report writing, and completion.
- Added `Tests/NativeForgeConversion-Test.ps1`.

### Changed
- `RimForge.App` no longer references or launches `RimForge.PowerShellBridge`.
- Ignite the Forge now consumes the in-memory canonical mod library and updates the Issue Viewer from the native analysis snapshot.
- PowerShell is now explicitly classified as a temporary parity/regression harness rather than the application runtime.

### Conversion boundary
- Evidence specializations, taxonomy, blueprint scoring, generated databases, and several legacy reports remain scheduled for subsequent native conversion passes.

## 2.1.0-alpha.26 — Unified Visual Language Foundation

### Added
- Canonical anvil-and-accessory workflow visual language.
- Branding asset manifest and production/reference directory structure.
- Reference logo and accessory-family concept artwork.
- Manifest validation test.

### Changed
- Expanded DESIGN_SYSTEM.md, ARCHITECTURE.md, ROADMAP.md, and README.md with visual-language rules.

### Notes
- Production workflow SVGs remain pending final asset delivery; concept art is intentionally not wired into runtime controls.

## 2.1.0-alpha.25

### Added
- Added the ForgeRepair Engine foundation with deterministic `RepairPlan` and `RepairPlanStep` contracts.
- Added repair planning for missing dependencies, load-order violations, duplicate installations, dependency cycles, and manual metadata review.
- Dependency-cycle repair now requires the user to choose which mod loads first before RimForge can produce a deterministic order.
- Added a preview-only repair-plan surface to per-issue and bulk Issue Viewer actions.
- Added an atomic JSON `RepairHistoryStore` contract for future executed repair records.
- Added `Tests/ForgeRepairEngine-Smoke-Test.ps1`.
- Added `DESIGN_SYSTEM.md` with the Human-First Communication standard.

### Changed
- Repair previews communicate mod display names wherever they can be resolved; package IDs remain fallback technical identifiers only.
- `Fix Issue` and `Fix All Issues` now summarize deterministic plans instead of placeholder implementation messages.

### Safety
- This release remains preview-only. It does not subscribe, reorder, delete, move, or rewrite files.

## 2.1.0-alpha.24

### Fixed
- Replaced the stale `ModSorterGrid` code-behind reference with the canonical `IssueViewerGrid` control after the Issue Viewer rename.
- Restored successful compilation of the Issue Viewer UI release.


## 2.1.0-alpha.23 — Issue Viewer UI Foundation

### Added
- Bound the Issue Viewer directly to structured `IssueWorkItem` records produced by the Issue Engine.
- Added active-profile and full-library issue scopes.
- Added canonical issue summaries such as `5 Errors • 2 Warnings`.
- Added per-issue explanation, impact, recommendation, resolution type, and repair-action surfaces.
- Added `Fix Issue` and `Fix All Issues` command placeholders that clearly describe the future ForgeRepair Engine boundary.
- Added `Tests/IssueViewerBinding-Test.ps1`.

### Changed
- Replaced the former generic mod table in the Issue Viewer workspace with an issue-focused, virtualized grid.
- Selecting an issue now selects the associated mod in the Mod Inspector.
- The Launch Bar remains the only control that changes the active profile.

### Known limitations
- Repair buttons explain the planned action but do not modify files yet; execution belongs to the upcoming ForgeRepair Engine pass.

## 2.1.0-alpha.22 — Startup Instrumentation Build Hotfix

### Fixed
- Restored compilation of `StartupTimeline.cs` by importing `System.IO` for `Path`, `Directory`, and `File`.

### Validation
- Clean-build the full solution.
- Run `Tests/StartupTimeline-Test.ps1`.
- Launch RimForge and confirm `Output/Reports/StartupTimeline.json` is generated.

## 2.1.0-alpha.21 — Responsive Architecture Sprint 3, Pass 2

### Added
- End-to-end startup timeline instrumentation beginning at managed module initialization and continuing through App construction, WPF startup, MainWindow XAML initialization, service composition, first render, coordinated discovery, and usable UI.
- `Output/Reports/StartupTimeline.json` with process-relative timestamps for each startup milestone.
- `Tests/StartupTimeline-Test.ps1` regression coverage for the instrumentation contract.

### Changed
- Reordered Sprint 3 so startup instrumentation precedes further Issue Viewer implementation work.
- Preserved the validated progressive-discovery and structured Issue Engine foundations while making startup performance measurable from process entry to usability.

### Validation
- Run the startup timeline instrumentation test.
- Clean-build and launch RimForge on Windows.
- Confirm `Output/Reports/StartupTimeline.json` is generated after the UI becomes usable.

## 2.1.0-alpha.20 — Responsive Architecture Sprint 3, Pass 2

### Added
- Structured `IssueWorkItem` objects for the Issue Viewer, including severity, explanation, why-it-matters guidance, recommended action, repair strategy, resolution type, and automatic-repair capability.
- Profile and full-library issue scopes with canonical `X Errors • Y Warnings` status summaries.
- `IssueEngine` projection from Forge analysis results into deterministic, sortable Issue Viewer work items.
- `Tests/IssueEngine-Smoke-Test.ps1`.

### Changed
- Established the Issue Viewer as the user-facing issue workspace and kept repair execution isolated behind the future ForgeRepair Engine.

### Validation
- Run the Issue Engine smoke test.
- Clean-build and launch RimForge on Windows.

## 2.1.0-alpha.19 — Responsive Architecture Sprint 3, Pass 1.0.1

### Fixed
- Restored compilation in `ModLibraryService.ScanAsync` by reporting newly parsed metadata records through `discoveredModProgress` instead of the background-intelligence-only `enrichedModProgress` parameter.

### Validation
- Run the XAML resource and progressive-intelligence tests.
- Clean-build and launch RimForge on Windows.

## 2.1.0-alpha.18 — Responsive Architecture Sprint 3, Pass 1

### Added
- Per-mod background-intelligence progress from the native library service to the WPF shell.
- Incremental Evidence-row notifications that update only the affected Mod Sorter, Issue Viewer, and profile rows.
- Startup metrics for time to usable UI and first-render-to-usable UI.
- Intelligence metrics for first enrichment result and incremental UI update count.
- `Tests/ProgressiveIntelligence-Test.ps1`.

### Changed
- Removed full `ModsView` and Issue Viewer refreshes at the end of background intelligence.
- Adopted **Issue Viewer** as the canonical user-facing workspace name.

### Documentation
- Updated roadmap, architecture, README, release notes, and version metadata.

## 2.1.0-alpha.17 — Core Stabilization Sprint 2, Pass 5.1

### Fixed
- Restored application startup by replacing undefined `SurfaceRaisedBrush` and `BorderBrush` XAML resources in the ForgeFixer scope header with existing `Bg3Brush` and `Bg4Brush` theme resources.
- Added a XAML resource smoke test to prevent this startup regression from returning.

### Validation
- Run `Tests/XamlResourceSmoke-Test.ps1`.
- Build and launch RimForge on Windows.

## 2.1.0-alpha.16 — Core Stabilization Sprint 2, Pass 5

### Added
- Progressive discovery architecture separating fast metadata discovery from deep background intelligence.
- Cancellable background native Evidence enrichment.
- `Output/Reports/IntelligenceMetrics.json` for non-blocking intelligence timing.

### Changed
- Startup no longer scans assemblies, Defs, patches, textures, and capability evidence before the Mod Sorter becomes usable.
- Native Evidence is enriched after discovery without blocking profile selection, navigation, or load-order work.
- Forge remains the explicit user-initiated engineering analysis stage.

### Documentation
- Consolidated the canonical workflow, workspace terminology, Launch Readiness Review, temporal badges, ForgeFixer/ForgeRepair, Strip Mods, and competitive-parity plans into `ROADMAP.md`.
- Updated architecture, README, contributing guidance, known issues, and current release notes.

## 2.1.0-alpha.15 — Core Stabilization Sprint 2, Pass 4

### Added
- Atomic WPF collection replacement through `BulkObservableCollection<T>`.
- Issue-first lazy ForgeFixer projection: only the current scope and active issues are materialized by default.
- Read-only scope status using the canonical `5 Errors • 2 Warnings` language.

### Changed
- The former Dashboard is now labeled **Mod Sorter**.
- The former Mod Sorter workspace is now labeled **ForgeFixer**.
- The Launch Bar remains the only control that changes the active profile.
- `Show Issues` is enabled by default; `Full Library` changes scope independently.
- Active, inactive, and ForgeFixer lists use WPF recycling virtualization and content scrolling.
- Profile and sorter projections are built in memory and applied with one collection reset.

### Performance
- Removed per-row collection notifications and redundant collection-view refreshes from startup projection.
- Avoided eagerly constructing the full ForgeFixer library during the default issue-first view.

### Validation
- Build the Windows solution.
- Launch twice with an unchanged cache and compare `UiProjection` in `Output/Reports/StartupMetrics.json`.
- Confirm profile switching occurs only through the Launch Bar and the ForgeFixer scope text follows it.

## 2.1.0-alpha.14 — Core Stabilization Sprint 2, Pass 3

### Added
- Fine-grained native-library startup timings for discovery, materialization, validation, dependency graph construction, and total service time.
- UI projection timings for preliminary Dashboard publication, analysis, Mod Sorter construction, bound collection updates, dependency edges, and profile loading.
- `Tests/StartupProjectionMetrics-Test.ps1`.
- Profile-specific `New` and recently-downloaded `Updated` temporal badge plans in the canonical roadmap.

### Changed
- `StartupMetrics.json` now separates cache work from downstream library and WPF projection work.

### Validation
- Build and launch RimForge with an unchanged 326-entry cache.
- Run `Tests/StartupProjectionMetrics-Test.ps1`.
- Use the timing breakdown to select the next optimization target.


## 2.1.0-alpha.13 — Core Stabilization Sprint 2, Pass 2.1

### Added
- Native-library cache diagnostics embedded in `Output/Reports/StartupMetrics.json`.
- Cache load status, load errors, hit/miss counts, added/removed/reparsed counts, timing breakdowns, and grouped miss reasons.

### Changed
- Native-library cache paths and target versions are normalized before comparison.
- Cache signatures no longer depend on the volatile mod-root directory timestamp.
- Native-library cache schema advanced to version 2.

### Fixed
- A cache could be written successfully while silently loading as empty or invalidating every record on the next startup. The reason is now visible and stable signatures avoid false invalidation from root-folder timestamp changes.

### Validation
- Added `Tests/NativeLibraryCacheMetrics-Test.ps1` to validate the runtime report shape and optional cache-hit requirement.
- Launch once to create the schema-2 cache, close RimForge, then launch a second time without changing the library.
- Confirm `NativeLibraryCache.LoadStatus` is `Loaded`, cache hits are high, and `MissReasons` identifies any remaining misses.

## 2.1.0-alpha.12 — Core Stabilization Sprint 2, Pass 2

### Added
- Persistent incremental native-library cache at `Output/Cache/NativeLibrary.json`.
- Version-aware cache signatures covering About.xml and key content-directory timestamps.
- Atomic cache writes and tolerant cache loading.

### Changed
- Cached mod records are published immediately during startup.
- Only added or changed mods are reparsed and passed through Forge Evidence scanning.
- Removed mods are dropped when the cache is rewritten.
- Native scan completion now reports both library-cache and Evidence-cache usage.

### Fixed
- Unchanged libraries no longer require complete About.xml and Evidence reconstruction on every launch.
- Invalid or obsolete native-library cache files no longer block startup.

### Validation
- Build on Windows.
- Launch twice without changing the mod library and compare `StartupMetrics.json`.
- Add, modify, and remove a test mod and confirm only the affected record is refreshed.

## 2.1.0-alpha.11 — Core Stabilization Sprint 2, Pass 1.0.1

### Fixed
- Added the missing `System.IO` namespace import required by `StartupCoordinator`.
- Updated startup metrics serialization to use the existing shared `RimForgeJson.Indented` options.
- Restored a clean compile for the responsive-startup pass.

## 2.1.0-alpha.10 — Core Stabilization Sprint 2, Pass 1

### Added
- `StartupCoordinator` as the single orchestration point for first-paint startup stages.
- `Output/Reports/StartupMetrics.json` with per-stage status and elapsed time.
- Plain-language startup stage messages for feature configuration, settings, path resolution, and native library construction.

### Changed
- The main shell now completes its first render before configuration, path detection, or native discovery begins.
- Startup work begins from `ContentRendered` instead of performing asynchronous initialization directly from `Loaded`.
- Startup failures identify the exact failed stage and preserve the responsive application shell.

### Validation
- Build the solution on Windows.
- Confirm the window paints before native discovery begins.
- Confirm the window remains movable and responsive during startup.
- Confirm `Output/Reports/StartupMetrics.json` is written.


## 2.1.0-alpha.9 — Core Stabilization Sprint 1, Pass 1.4

### Added
- `Output/Reports/ForgeSummary.json` with the final Forge result, duration, analyzed-mod count, stage counts, severity totals, and concise condition summaries.
- A user-facing end-of-run console summary that lists the first actionable audit conditions without requiring users to open the raw pipeline report.

### Changed
- Recoverable audit stages now use plain-language progress messages such as `Generating taxonomy reports...` and `Taxonomy Reports complete`.
- Profile taxonomy coverage and blueprint scores remain in generated reports instead of cluttering the action-oriented console.
- Cross-profile comparison skip messages now explain why the stage was not applicable.
- Successful audits no longer append the developer-facing `Audit exited with code 0` message in the application console.
- Incremental-cache and timing messages now use clearer user-facing language.

### Validation
- Build the solution on Windows.
- Run all existing audit regression tests.
- Ignite the Forge and verify `ForgeSummary.json` is generated and the console ends with the structured completion summary.

## 2.1.0-alpha.8 — Core Stabilization Sprint 1, Pass 1.3

### Fixed
- `Get-MinimalLoadOrderActions` now accepts an empty `CycleGroups` collection, so profiles with no dependency cycles complete load-order analysis normally.
- `Get-DatabaseSha256` now accepts an empty string and returns the canonical SHA-256 for an empty payload instead of failing parameter binding.
- The two unexpected recoverable conditions observed in the `2.1.0-alpha.7` runtime report are addressed at their source.

### Added
- `Tests/AuditRemainingEmptyResults-Test.ps1` covering empty cycle groups and empty generated-database hash input.

### Validation
- Build the solution on Windows.
- Run all three audit regression tests.
- Ignite the Forge and verify profile analysis and generated-database construction no longer emit empty-input recoverable conditions.

## 2.1.0-alpha.7 — Core Stabilization Sprint 1, Pass 1.2.1

### Fixed
- `Get-ProfileTaxonomySummary` no longer reads a nonexistent `Count` property from the profile object under PowerShell strict mode.
- Taxonomy coverage and active-mod totals now derive from `Profile.ActiveMods`, matching the actual profile contract.
- `Tests/AuditEmptyCollections-Test.ps1` can now exercise the intended empty-family-rule scenario instead of failing before the assertions run.

### Validation
- The previous synchronized root built successfully on Windows.
- Run `Tests/AuditEmptyCollections-Test.ps1` and `Tests/AuditPipeline-Test.ps1` against this release.
- Ignite the Forge after both tests pass.

## 2.1.0-alpha.6 — Core Stabilization Sprint 1, Pass 1.2

### Added
- `Modules/AuditPipeline.psm1` with a reusable recoverable-stage execution contract.
- `Output/Reports/AuditStages.json` stage status and timing report.
- Stage outcomes in `ProfilePipeline.json`.
- `Tests/AuditPipeline-Test.ps1` regression coverage.
- `ARCHITECTURE.md` as a living technical reference.

### Changed
- Version-status, taxonomy, blueprint, profile-comparison, and global audit report generation now execute as isolated stages.
- A recoverable report-stage failure no longer prevents later reports or Forge completion.
- Project version advanced to `2.1.0-alpha.6`.

### Fixed
- Downstream report-generation errors can no longer cascade through the remaining audit output pipeline.

### Validation required
- Build on Windows.
- Run `Tests/AuditEmptyCollections-Test.ps1`.
- Run `Tests/AuditPipeline-Test.ps1`.
- Ignite the Forge and inspect `Output/Reports/AuditStages.json`.

## Core Stabilization — Sprint 1, Pass 1.1 — Empty Collection Hardening

### Fixed
- Empty taxonomy `FamilyRules` no longer cause profile analysis to fail.
- Empty profile-version summaries no longer prevent `VersionStatus.json` from being written.
- Empty taxonomy, blueprint, and profile-set summary collections no longer cause PowerShell parameter-binding failures.
- Report writers now preserve valid empty-state outputs when no profiles complete an optional analysis stage.

### Added
- `Tests/AuditEmptyCollections-Test.ps1` covering empty taxonomy rules and all profile-summary report writers.
- The roadmap engineering rule that an empty result is a valid pipeline outcome unless a stage explicitly requires data.

### Validation required
- Build the solution on Windows.
- Run `powershell -ExecutionPolicy Bypass -File .\Tests\AuditEmptyCollections-Test.ps1`.
- Ignite the Forge and confirm it reaches completion with zero taxonomy family rules and zero successful profile summaries.

## Core Stabilization — Sprint 1, Audit Reliability Pass 1 — Pipeline Hardening

### Added
- Explicit audit severities for information, warning, recoverable, and fatal conditions.
- Structured audit-condition collection with subsystem, timestamp, message, and detail.
- `Output/Reports/AuditConditions.json`.
- Condition counts and full condition records in `ProfilePipeline.json`.

### Changed
- Invalid optional compatibility, Evidence, load-order, taxonomy, blueprint, version-status, and generated-database data now degrade gracefully.
- Per-profile analysis failures no longer prevent remaining profiles from being processed.
- RimForge's activity feed recognizes recoverable audit messages as warnings and fatal messages as errors.

### Fixed
- Optional curated-data corruption can no longer terminate an otherwise valid audit.
- Generated-database failures no longer discard core audit results.

### Validation required
- Build the solution on Windows.
- Run a normal Forge audit.
- Temporarily remove or invalidate optional curated files and confirm Forge completes with recoverable conditions.
- Confirm `AuditConditions.json` and `ProfilePipeline.json` contain the expected structured results.

## Core Stabilization — Sprint 1, Audit Reliability Pass 1

### Changed
- Forge now falls back to RimWorld's live `ModsConfig.xml` when no RimForge profile XML is available.
- Forge continues in Library Analysis mode when no usable profile exists.
- Library Analysis mode clearly reports which global stages completed and which profile-specific stages were skipped.
- `ProfilePipeline.json` now records the profile analysis mode and profile source.

### Fixed
- Removed the terminating `No valid ModsConfig profiles were found` audit failure.

### Validation required
- Build the solution.
- Run Forge with profiles present.
- Run Forge with `Output\\Profiles` empty but live RimWorld `ModsConfig.xml` present.
- Run Forge with neither profile source available and confirm a successful library-only audit.


# 2.2.0-alpha.39 — ForgeView & Load-Order Safety Suite

### Added
- Interactive ForgeView graph and synchronized outline with selection/navigation synchronization, search highlighting, profile/full-library scope, path focus, conflict/cycle rendering, pan/zoom, Fit, and DOT/CSV export.
- Approved blueprint drafting-surface background for the ForgeView canvas.
- Shared engineering metrics strip between Mod Sorter and Issue Viewer, with ForgeView-local topology metrics beneath the graph.
- Persisted Auto-Sort mode and unified Pass 39 acceptance coverage.

### Changed
- Core is the only mandatory, non-removable package.
- Harmony, official DLC, RocketMan, and MissileGirl are position anchors while active, not mandatory packages.
- Official DLC receives authoritative built-in dependency evidence requiring Core.
- Required-dependency removal now permits only cascade deactivation or abort; invalid profiles cannot be forced through manual mode.
- Sort applies the same canonical calculation used by its enabled state, disables when no work is required, and uses stable Chrome+ warning feedback.
- Drag preview initialization and ForgeView node bindings are hardened against the regressions found during Pass 39 validation.



## Pass 39 closeout correction

- Confirmed profile load-order persistence across application restarts.
- Added the complete ForgeView Forged Layout and Weight Maps initiative to the canonical roadmap.
- Added transactional multi-selection drag-and-drop, Engineering Evidence, Hybrid Auto-Sort, dynamic Auto-Sort help, and Engineering Academy tooltip controls to the roadmap.
- Corrected the Pass 39 persistence certification test so roadmap validation checks the initiative semantically rather than relying on one brittle exact string.

## Pass 40 — Engineering Workstation Shell Foundation

- Added the unified Engineering Command Bar shell host.
- Surfaced transaction undo with an exact pending-operation preview.
- Projected background work into the shell application-status area.
- Converted Settings into independently scrolling General, Appearance, Profiles, Automation, and Advanced tabs.
- Added semantic command-bar resources and the default-skin management foundation.
- Reserved, but did not implement, browser navigation history and Reforge behavior pending explicit architectural approval.

### Pass 40 validation repair
- Corrected repository hygiene validation so normal ignored `bin` and `obj` directories produced by local builds do not fail the gate; tracked generated paths still fail.
- Reissued Pass 40 as a cumulative patch containing the complete workstation-shell epic rather than requiring the earlier foundation patch.
- Restored the Pass 40 workstation-shell foundation acceptance test to the cumulative delivery.

### Pass 43 ForgeView Outline Hierarchy Hotfix
- Reworked Outline mode into a left-aligned dependency tree.
- Added deterministic depth indentation and branch markers for XML/JSON-like scanning.
- Enabled horizontal overflow for deeply nested dependency chains.


## 2.2.0-alpha.45a.1

Started the app-wide visual standardization pass. Added canonical semantic visual tokens, unified active/hover/focus/disabled states, standardized navigation selection, and migrated ForgeView mode selectors to the shared state family.

## 2.2.0-alpha.45a.2 — Commit 11 Chrome+ completion
- Wrapped ForgeView header, toolbar, graph, inspector, minimap, and status region in one unified workspace card.
- Made the ForgeView minimap clickable and draggable for direct viewport navigation.
- Added ForgeView previous/next selection history controls.
- Relocated profile administration from the persistent Launch Bar to Settings → Profiles.
- Replaced the Launch Bar profile action cluster with a single Profile Management entry point.
- Corrected the visual-standardization black-foreground acceptance gate false positive.

## Commit 11 — Integrated Texture Tools workflow
- Moved DDS validation into asynchronous Active Profile texture analysis.
- Analyze button now changes to Re-Analyze Active Profile after a successful pass.
- Removed Add Folder, standalone Validate DDS, backup creation, and backup restore controls.
- Added Convert Selected, Convert All Eligible, and manifest-backed Revert Conversion.
- DDS output now normalizes dimensions to the nearest multiple of four while preserving aspect ratio with transparent padding.
- Source textures remain untouched; only generated outputs are eligible for reversion.

## Commit 11 — Category-First Load Order Policy
- Added category-band-first ordering to the dependency-safe topological sorter.
- Added modernized top anchors for SRTS, HugsLib, JecsTools, Humanoid Alien Races, EdB Prepare Carefully, and Character Editor.
- Anchored EdB Prepare Carefully between Humanoid Alien Races and Character Editor.
- Preserved RocketMan and Missile Girl / Performance Optimizer as canonical bottom anchors.
- Added evidence-based category classification with the guide's later-category-overrides-earlier rule.
- Added a data-readable curated load-order policy and Commit 11 validation coverage.
- Updated the stale Texture Tools surface gate to the integrated conversion workflow.

## Commit 11 end-to-end pass
- Connected automatic Fix Issue execution for load-order repairs and removed false auto-fix affordances.
- Added persistent Ignore/Unignore with ghosted in-place findings excluded from readiness counts.
- Added cached health-anvil tooltip states and category-first sorting policy settings/rule-pack groundwork.

## Commit 12 — Pass 46A.5

- Prevented active-generation cancellation from racing partial evidence publication.
- Added bounded Forge Evidence cache lifecycle cleanup and cleanup metrics.
- Added the Pass 46A.5 cancellation/cache lifecycle regression gate.

### Pass 47A.1 — Shared Evidence ForgeView Graph Projection
- Added `ForgeGraphProjectionService` as the deterministic bridge between Shared Evidence generations and ForgeView.
- ForgeView now republishes graph nodes and edges atomically after completed evidence generations.
- Added incremental node reuse, evidence-aware health projection, topology signatures, and projection metrics.
- Added `Pass47A1SharedEvidenceGraphProjection-Test.ps1` and connected it to the aggregate completion gate.
- Fixed badge rows not refreshing after startup Shared Evidence publication by notifying existing sorter and profile view models without rebuilding their collections.

### Fixed
- Restored technology badges in the Active Profile Load Order by projecting evidence badges directly through each load-order row view model and explicitly notifying recycled WPF rows after Shared Evidence publication.

### Pass 47B.5 validation alignment hotfix
- Updated the Pass 47B.1 authoritative Forge evidence regression gate to accept incremental cached evidence publication (`forceRescan: false`) while retaining the requirement that evidence publication executes before native Forge analysis.

## Curated Database Pipeline Pass

- Added RimWorld-version-scoped load-order rules with stable rule IDs, provenance, review metadata, schema validation, and fail-closed behavior for invalid hard constraints.
- Added contradiction quarantine so opposing curated relationships are surfaced rather than silently resolved.
- Added the bundled `UseThisInstead.json` advisory database and Issue Viewer findings without automatic unsubscribe, deletion, activation, or replacement behavior.
- Threaded the selected target RimWorld version through library analysis, Forge DNA, native Forge reports, and compatibility reports.

### Focused Windows test-recovery pass

- Restored `.gitignore` to packaged repository output so repository-hygiene and runtime-storage-isolation checks can execute.
- Made PowerShell contract tests Windows PowerShell 5.1-safe by preserving UTF-8 punctuation with BOM encoding.
- Updated decomposed-feature tests to inspect the current feature partials and views instead of stale monolithic `MainWindow` locations.
- Updated authoritative Forge contracts to require `forceRescan: true` for explicit Forge runs.
- Corrected startup replay contract wording for valid unterminated final `Player.log` lines.
- Converted generated-report and retired PowerShell-module tests into explicit prerequisite skips when their runtime fixtures are absent.
- Added `Tests/Run-FocusedErrorRecovery.ps1` for deterministic validation of the failures reported from the Windows test run.

## Focused Error Recovery Pass 2
- Rebased stale historical PowerShell acceptance contracts onto the decomposed feature architecture.
- Corrected test ownership for dependency assistance, ForgeView, native Forge, launch readiness, progressive intelligence, and unified search.
- Replaced retired PowerShell audit smoke expectations with the native .NET application contract.

## External profile reconciliation pass
- Added content-aware comparison between RimWorld's active `ModsConfig.xml` and the selected RimForge profile.
- Added notification actions to import the external order into the selected workspace or restore the RimForge profile to RimWorld.
- Added self-write acknowledgement after reconciliation actions to prevent watcher feedback loops.
- Fixed `Run-AllTests.ps1` output pollution so one failed child test can no longer be followed by a false all-tests-passed result.

## Pass 49 — Runtime Evidence & Compatibility Intelligence

- Added the protocol-v2 shared runtime evidence contract to the desktop solution.
- Added persistent runtime sessions and a deduplicating Forge Evidence Lake.
- Added named-pipe ingestion for session, single-evidence and evidence-batch messages.
- Added first compatibility/conflict/integration/performance/repair-confidence projections.
- Composed and lifecycle-managed the Runtime Sensor host in the desktop application.
- Added Runtime Sensor startup, persistent session projection, runtime conflict counts, and selected-mod evidence inspection.
- Projected observed runtime incompatibilities into Issue Viewer and ForgeView relationship edges.
- Added runtime-assisted repair hooks without unsafe automatic mutation.
- Added active-profile scope filtering and runtime evidence contract coverage.

## Epic 1 — Forge Evidence Platform Completion

- Added versioned Forge Evidence platform contracts, source classification, provenance, confidence bands, ingestion batches, and producer extensibility.
- Added deterministic cross-source contribution consolidation with weighted confidence and observation aggregation.
- Added atomic persistent snapshot storage, restore support, schema gating, and corrupt snapshot quarantine.
- Extended the authoritative evidence service with validated ingestion and producer collection while preserving existing static scan consumers.
- Added protocol contracts for structured Forge Evidence contribution batches.
- Added executable persistence/merge/schema tests and Epic certification checks.

## Epic 1 Pass 4 — Evidence Producer Integration

- Integrated Harmony ownership hints, curated community load-order rules, Use This Instead replacements, Runtime Companion observations, and derived compatibility intelligence into the unified Forge Evidence pipeline.
- Added deterministic producer composition so runtime-backed producers share the same transactional publication boundary as static and dependency analysis.
- Added provenance, confidence, correlation IDs, observation windows, relationship subjects, and source-specific attributes for every integrated producer.
- Added static certification coverage for producer registration and application composition.

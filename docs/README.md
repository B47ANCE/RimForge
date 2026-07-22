# RimForge Documentation

This directory contains current engineering references. Product-level entry points remain at the repository root.

## Architecture

- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — system boundaries and dependency direction
- [`architecture/CANONICAL_PATHS.md`](architecture/CANONICAL_PATHS.md) — runtime storage policy
- [`architecture/CODING_STANDARDS.md`](architecture/CODING_STANDARDS.md) — code conventions
- [`architecture/REPOSITORY_BOUNDARIES.md`](architecture/REPOSITORY_BOUNDARIES.md) — Client, Companion, and TestSuite ownership
- [`architecture/FORGE_EVIDENCE_INGESTION.md`](architecture/FORGE_EVIDENCE_INGESTION.md) — evidence ingestion
- [`architecture/FORGE_EVIDENCE_INCREMENTAL_REFRESH.md`](architecture/FORGE_EVIDENCE_INCREMENTAL_REFRESH.md) — refresh and invalidation

## Runtime companion

- [`../RUNTIME_COMPANION.md`](../RUNTIME_COMPANION.md) — product-level runtime architecture
- [`companion/PROTOCOL.md`](companion/PROTOCOL.md) — protocol contract
- [`../../RimForge.Companion/docs/companion/SECURITY.md`](../../RimForge.Companion/docs/companion/SECURITY.md) — runtime mod threat model and constraints
- `../../RimForge.Companion.TestSuite/docs/testing` — fixture-driven runtime validation and acceptance matrix

## Product engineering

- [`../DESIGN_SYSTEM.md`](../DESIGN_SYSTEM.md)
- [`../DATABASES.md`](../DATABASES.md)
- [`../SORTING_ENGINE.md`](../SORTING_ENGINE.md)
- [`../ENGINEERING_PHILOSOPHY.md`](../ENGINEERING_PHILOSOPHY.md)
- [`../ROADMAP.md`](../ROADMAP.md)

## Current platform foundations

- [`development/EPIC_A_PASS1_INFRASTRUCTURE.md`](development/EPIC_A_PASS1_INFRASTRUCTURE.md) — background execution and path ownership
- [`development/UNIFIED_EVIDENCE_PRODUCERS.md`](development/UNIFIED_EVIDENCE_PRODUCERS.md) — the single Pass 2 evidence producer contract
- [`development/EPIC_A_PASS2_UNIFIED_EVIDENCE.md`](development/EPIC_A_PASS2_UNIFIED_EVIDENCE.md) — aggregation, publication, durability, and unified consumers
- [`development/EPIC_A_PASS3_RESILIENCE.md`](development/EPIC_A_PASS3_RESILIENCE.md) — health validation, recovery, signed updates, rollback, and state preservation
- [`development/EPIC_B_PASS1_ANALYSIS_FOUNDATION.md`](development/EPIC_B_PASS1_ANALYSIS_FOUNDATION.md) — deterministic full-library analysis execution
- [`development/EPIC_B_PASS2_OBSERVABLE_PIPELINE.md`](development/EPIC_B_PASS2_OBSERVABLE_PIPELINE.md) — typed stages, metrics, diagnostics, and stable analysis output
- [`development/EPIC_B_PASS3_INCREMENTAL_ANALYSIS.md`](development/EPIC_B_PASS3_INCREMENTAL_ANALYSIS.md) — bounded deterministic result reuse and invalidation
- [`development/EPIC_B_PASS4_EXPLAINABILITY.md`](development/EPIC_B_PASS4_EXPLAINABILITY.md) — canonical overview and per-mod analysis rationale
- [`development/EPIC_B_PASS5_EVIDENCE_CONVERGENCE.md`](development/EPIC_B_PASS5_EVIDENCE_CONVERGENCE.md) — runtime and compatibility evidence in canonical analysis
- [`development/FORGE_SESSION_FOUNDATION.md`](development/FORGE_SESSION_FOUNDATION.md) — Forge lifecycle and persistence
- [`development/PLATFORM_DISCOVERY_FOUNDATION.md`](development/PLATFORM_DISCOVERY_FOUNDATION.md) — Steam, RimWorld, and workspace discovery
- [`development/COMPANION_HOST_FOUNDATION.md`](development/COMPANION_HOST_FOUNDATION.md) — hidden Companion process and client control surface
- [`development/DIAGNOSTICS_PLATFORM.md`](development/DIAGNOSTICS_PLATFORM.md) — shared logging, health, and timing

Development notes document active systems only. Historical pass-by-pass reports are intentionally excluded from the canonical repository.

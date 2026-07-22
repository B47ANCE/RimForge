# Pass 44 — Forge DNA & Analysis Engine

## Purpose

Epic 44 establishes a single analysis projection that can be consumed by the Dashboard, Mod Sorter, Inspector, Issue Viewer, ForgeView, reports, and future optimization systems.

## Pipeline

1. Mod discovery and evidence collection remain owned by the native library service.
2. `IForgeDnaService` receives discovered `ModRecord` instances and the active load order.
3. The existing dependency analysis engine produces relationships, cycles, issues, and ordering guidance.
4. Forge DNA projects each mod into a canonical record combining identity, source, versions, dependencies, dependents, technology evidence, capabilities, findings, health, and issues.
5. Deterministic fingerprints allow unchanged records to be reused while dynamic dependency and issue projections are refreshed.
6. Consumers receive one `ForgeDnaSnapshot`, including the underlying `ModAnalysisSnapshot` and performance metrics.

## Incremental behavior

A fingerprint includes source modification time, evidence inventory, dependency metadata, load constraints, and evidence badge counts. An unchanged fingerprint reuses the expensive static projection. Dependency relationships and issues are refreshed on every run because active profile state may have changed.

`Invalidate()` clears the full cache; `Invalidate(packageId)` clears one record.

## Native reports

Native Forge now writes `ForgeDnaReport.json` beside the existing analysis, evidence, compatibility, and summary reports.

## Scope boundary

This foundation intentionally preserves the current UI. Full visual presentation of Forge DNA belongs to Epic 45 Chrome+, while later Epic 44 increments may expand providers, persistence, and diagnostics.

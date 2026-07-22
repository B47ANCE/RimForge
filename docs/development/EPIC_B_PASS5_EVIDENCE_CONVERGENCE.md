# Epic B Pass 5 Unified Evidence Analysis

Epic B Pass 5 closes the remaining split between the unified Forge Evidence plane and native analysis. Actionable runtime, compatibility, declared-incompatibility, and replacement evidence now enters `ModAnalysisRequest` and is classified by the canonical engine.

## Canonical findings

The engine emits runtime performance regressions, runtime integration failures, observed runtime conflicts, declared compatibility concerns, and replacement recommendations as ordinary `ModAnalysisIssue` values. Evidence-backed findings retain stable source identity, provenance, confidence, observation count, owning package, and related packages.

High-confidence or explicitly warning/error evidence maps to the corresponding analysis severity. Compatibility assessments with a conflict score below `0.5` are treated as healthy evidence and do not become issues. Evidence referring to a package outside the installed library is retained by the evidence platform but does not create a library analysis finding.

When an actionable contribution relates two installed mods, the snapshot also receives a non-mandatory `ObservedConflict` relationship. It informs explanations and graph consumers without becoming a dependency or load-order constraint.

## One consumer path

Forge DNA and every main-client analysis call pass the current evidence generation into the engine. Evidence content participates in the deterministic input fingerprint, including type, subjects, confidence, observation count, provenance, and attributes.

Issue Viewer now consumes only `IssueEngine.Build` over the canonical snapshot. The former UI-local runtime and Forge Evidence issue builders were removed, eliminating duplicate classification, inconsistent codes, and divergent ignore identities.

# Epic B Pass 4 Analysis Explainability

Epic B Pass 4 adds a canonical explainability projection to every analysis result. Dashboard, Inspector, Issue Viewer, ForgeView, sorting, and reporting consumers can now present the same rationale without independently joining snapshot data or inventing feature-specific explanations.

## Full-library overview

`AnalysisOverview` reports installed and active scope, healthy and affected mod counts, severity totals, mandatory and advisory relationship counts, cycle count, load-order completeness, canonical status, and a plain-language narrative.

The overview is derived from the same immutable snapshot and diagnostics returned by the engine. It does not run a parallel analysis or alter health and ordering decisions.

## Per-mod explanations

`AnalysisExplanationCatalog.GetMod` performs case-insensitive lookup of a stable `ModAnalysisExplanation`. Each explanation combines:

- full-library dependency and impact summary;
- active-profile membership;
- structured diagnostics;
- incoming and outgoing relationship rationale with source, confidence, and mandatory status;
- repair recommendations;
- load-order placement decision; and
- a concise deterministic narrative.

Explanations are ordered by package identity and built once at the finalization boundary. Incremental cache entries retain the explanation catalog alongside the snapshot, so cache hits do not rebuild or drift from the result they explain.

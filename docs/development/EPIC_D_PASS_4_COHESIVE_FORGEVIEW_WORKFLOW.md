# Epic D Pass 4 — Cohesive ForgeView Workflow

Status: complete
Version: 2.2.0-alpha.73

## Outcome

ForgeView now participates in one canonical selected-mod context shared with search, Issue Viewer, profiles, the graph, the outline, and Mod Inspector. The selection service retains the navigation origin even when a workflow re-selects the same mod, while ForgeView keeps one branch-safe history for graph navigation.

## Actionable workspace states

- Loading explains that discovery is rebuilding the graph and prevents a redundant refresh.
- Empty results direct the user to confirm library paths and refresh.
- Discovery failure offers a retry without changing profile data.
- Missing Companion evidence is explicitly described as degraded metadata-only operation and offers an evidence refresh.

These states do not replace the dependency model or fabricate runtime conclusions.

## Accessibility acceptance

- The workspace, graph canvas, and graph/outline mode controls expose automation names and help text.
- Every node remains available through the keyboard-operable outline.
- The canvas documents and retains arrow-key panning, plus/minus zoom, fit, and selected-node centering.
- Status changes use a polite live region and consistent existing button, surface, border, and text resources.

## Verification

`Tests/EpicDPass4CohesiveForgeWorkflow-Test.ps1` protects the integration and accessibility contracts and runs executable selected-context behavior coverage. The full repository validation remains the release gate.

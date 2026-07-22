# Commit 11 — Issue Repair, Ignore State, and Sorting Policy

- `IssueEngine` projects immutable analysis findings into UI work items and applies persisted ignore IDs without removing findings.
- Only `RepairExecutionMode.Automatic` plans with `RepairPlanStatus.Ready` expose an enabled **Fix Issue** action.
- Automatic load-order repair writes the profile and refreshes the shared analysis snapshot, Issue Viewer, Mod Sorter, and load-order projection.
- Ignored findings remain visible at reduced opacity and are excluded from readiness/error/warning totals.
- Health-anvil tooltip text is computed from cached UI state only; hover performs no analysis, disk scan, or network work.
- Category-first policy uses later-band precedence. Non-anchor dependency reconciliation is downward-only; curated anchors are bidirectional.
- Sorting settings and rule-pack identity are persisted in `Config.json`.

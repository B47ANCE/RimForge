# Pass 42 — Unified Search & Discovery

Pass 42 turns the Engineering Command Bar search field into an actionable discovery surface while retaining the mature structured-query engine delivered by earlier search passes.

## Delivered

- One live query continues to filter Mod Sorter, Issue Viewer, and ForgeView relationship data.
- Ranked cross-surface discovery results now include mods, issues, and workstation destinations.
- Result ranking favors exact matches, then prefix matches, then substring matches.
- Results are bounded to sixteen entries to keep command-bar interaction predictable.
- Down Arrow moves from the search field into results.
- Enter opens the selected result.
- Escape dismisses results and returns focus to search.
- Double-click activates a result.
- Mod results open Mod Sorter and synchronize Inspector selection.
- Issue results open Issue Viewer and synchronize the selected issue/mod context.
- Workspace results navigate directly to Dashboard, Mod Sorter, Issue Viewer, ForgeView, Texture Tools, Console, or Settings.
- Search results rebuild when the query, active profile membership, installed library, or issue set changes.

## Architectural contract

The existing `ISearchContext`, `StructuredSearchQuery`, and `IModFilteringService` remain authoritative. Pass 42 adds a presentation-oriented discovery projection rather than introducing a competing parser or filter path.

The visible Ctrl+F affordance, live filtering, result presentation, keyboard activation, mouse activation, and navigation are covered by acceptance tests so no nonfunctional search UI is shipped.

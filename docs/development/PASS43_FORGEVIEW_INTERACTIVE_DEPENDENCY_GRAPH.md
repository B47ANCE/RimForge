# Pass 43 — ForgeView Interactive Dependency Graph

## Objective

Complete ForgeView as a first-class dependency engineering workspace without replacing the graph, outline, search, profile-scope, export, or shared selection contracts delivered in earlier passes.

## Interaction contract

- Drag empty space with the left or right mouse button to pan.
- Use the mouse wheel, `+`, or `-` to zoom around the pointer or viewport center.
- Press `F` or choose **Fit** to fit the complete visible topology.
- Press `Home` or choose **Center** to center the synchronized selected mod.
- Click a node to synchronize Mod Sorter and Mod Inspector selection.
- Double-click a node to select and center it.
- Right-click a node to inspect it, center it, or toggle dependency-path focus.
- Arrow keys pan the graph while it owns keyboard focus.

## Layout and performance

ForgeView computes a deterministic dependency-aware layered layout and caches it by a stable topology signature. Repaints caused by selection, search, panning, zooming, or path emphasis reuse the cached coordinates. The layout is invalidated only when the visible topology changes.

## Path focus

When a mod is selected, ForgeView computes both its transitive required direction and reverse-dependent direction across the visible graph. Nodes and edges participating in that complete relationship path remain prominent; unrelated topology is reduced without being removed.

## Synchronization

`SelectedMod.PackageId` remains the shared selection contract. Selections arriving from Unified Search, Mod Sorter, Mod Inspector, or Issue Viewer automatically center ForgeView when the graph is loaded. Node invocation continues to route through `ModNavigationRequested`.

## Scope preservation

Pass 43 preserves profile-only/full-library graph scope, global search highlighting, graph/outline modes, DOT/CSV export, relationship colors, and dependency health context.

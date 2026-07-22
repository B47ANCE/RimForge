# Epic D Pass 3 — Scalable Graph Rendering

Status: complete  
Version: 2.2.0-alpha.72

## Outcome

ForgeView no longer computes large topology layouts synchronously on the WPF render path. Graphs with at least 160 visible nodes are snapshotted and laid out on background work. Each request owns a cancellation token and monotonically increasing generation; only the current signature and generation may publish positions back to the canvas.

Small topology changes reuse positions for unchanged nodes and place additions without overlapping retained nodes. Rendering remains viewport-culled above 120 nodes, so panning and zooming draw only the nodes and relationships intersecting the logical viewport.

Completed layouts are held in an eight-entry least-recently-used cache. Cache entries contain immutable position copies and measured layout duration. The ForgeView status line reports pending/completed layout state, cache hits, cache occupancy, generation changes, and render-budget overruns.

## Performance budgets

| Work | Representative fixture | Budget |
|---|---|---:|
| Canonical query | 1,000 nodes / 1,197 edges | 250 ms |
| Deterministic layout | 1,000 nodes / 1,197 edges | 2,000 ms |
| Viewport render | Live WPF frame | 33 ms |

The acceptance fixture is deliberately larger than a normal RimWorld library and is generated deterministically in `Tests/RimForge.ForgeViewPerformanceTests`. The initial development run completed the query in approximately 5 ms and layout in approximately 55 ms. CI enforces the budgets while allowing normal hosted-runner variance.

## Acceptance

- Cancellation is checked throughout connected-component discovery, strongly connected component analysis, layer ordering, component placement, and publication.
- Superseded generations cannot update canvas state.
- Layout cache occupancy cannot exceed eight topology signatures.
- Representative query and layout work stay within their explicit budgets and position every fixture node.
- The complete Client build and acceptance suite remain authoritative.

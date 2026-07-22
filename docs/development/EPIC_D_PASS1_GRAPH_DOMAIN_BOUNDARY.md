# Epic D Pass 1 ForgeView Graph Domain Boundary

Epic D begins by making ForgeView's graph contract independent of its implementation.

`ForgeGraphProjection`, topology metrics, graph diffs, strongly connected clusters, and intelligence metrics now belong to `RimForge.Core.Models`. `IForgeGraphProjectionService` belongs to `RimForge.Core.Services`. Infrastructure retains dependency/evidence projection, incremental node reuse, topology signatures, and graph algorithms.

This boundary lets ForgeView UI, future graph queries, exports, tests, and alternate renderers consume immutable domain state without importing concrete Infrastructure types. It is an architectural move only; existing graph behavior and cache semantics are preserved.

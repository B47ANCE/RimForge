# RimForge Development History

This file summarizes completed eras of the project. Detailed chronological changes remain in [CHANGELOG.md](CHANGELOG.md), while the original pass-oriented roadmap is preserved in [docs/archive/ROADMAP_LEGACY.md](docs/archive/ROADMAP_LEGACY.md).

## PowerShell audit foundation

RimForge began as a PowerShell-based RimWorld mod audit and reporting tool. The early system established discovery, About.xml parsing, dependency modeling, compatibility checks, reports, texture utilities, and database tooling.

## Native WPF/.NET conversion

The project evolved into a native WPF/.NET application split into Core, Infrastructure, Analysis, UI, App, and a temporary PowerShell bridge. Native services progressively replaced runtime PowerShell responsibilities while parity scripts remained as development and validation tools.

## Application infrastructure

Major completed foundations include:

- Startup lifecycle and instrumentation
- Incremental native library cache
- Shared background-task execution
- Typed application event bus
- Unified command framework
- Profile workspace state
- Notifications, activity, navigation, search, and selection context
- Explicit shutdown and runtime-storage isolation

## Engineering workflows

The application gained:

- Profile management
- Dependency assistance and removal safety
- Mod Sorter direct manipulation
- Issue Viewer and repair planning
- Launch Readiness Review
- Texture Tools
- Console and launch integration
- Unified search and cross-feature selection

## Shared Forge Evidence

Independent evidence consumers were consolidated behind a shared evidence service with persistent cache validation, incremental rescans, invalidation, watcher reliability, cancellation, metrics, and graph projection.

## Dependency Map evolution

ForgeView developed from a static dependency display into an interactive graph/outline workspace with direction-aware relationships, filtering, path isolation, selection synchronization, persisted profile-owned layouts, and incremental rendering. The product-facing name is now **Dependency Map**; legacy class and test names may still use ForgeView until source migration is safe.

## Pre-1.0 consolidation

The current era is focused on converting the broad alpha feature set into a coherent 1.0 product: explainable sorting, Community Rules, Runtime Companion integration, reliable post-Forge projection, production artwork, release engineering, and end-to-end safety validation.

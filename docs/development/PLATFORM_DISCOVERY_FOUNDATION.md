# Platform Discovery Foundation

Epic A centralizes machine-specific discovery behind a single service graph.

## Contracts

- `ISteamLibraryService` discovers Steam roots and RimWorld candidates.
- `IRimWorldInstallationService` projects valid RimWorld installations.
- `IWorkspaceService` owns the canonical `RimForgePathLayout`.
- `IPlatformDiscoveryService` publishes an immutable `PlatformDiscoverySnapshot`.

The snapshot contains every discovered installation, a deterministic preferred installation, Workshop roots, canonical RimWorld user paths, and the RimForge workspace layout.

## Discovery policy

Steam discovery considers explicit roots, registry roots, standard installation folders, and every library declared in `libraryfolders.vdf`. RimWorld's app manifest controls the installation directory name. Each candidate projects game, local-mod, official-content, Workshop, Steam executable, and game executable paths.

Preferred installations are ordered by direct-launch readiness, Workshop availability, and normalized library path. UI selection may override that preference without changing discovery truth.

## User paths

`ModsConfig.xml` and `Player.log` now resolve once from the RimWorld user-data root:

```text
LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/
  Config/ModsConfig.xml
  Player.log
```

Profile activation and game-log startup consume this shared projection. Mutable RimForge data remains governed by `IWorkspaceService` and never defaults to the repository.

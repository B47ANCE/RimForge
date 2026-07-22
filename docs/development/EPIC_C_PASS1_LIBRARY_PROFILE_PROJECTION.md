# Epic C Pass 1 Library/Profile Projection

Epic C begins with a canonical read model for the installed library and all RimForge profiles. The projection does not replace scanning, profile persistence, activation, or analysis. It gives those systems and their UI consumers one deterministic view of library membership.

## Contract

`ILibraryProfileProjectionService.Create` accepts the current installed-mod inventory and profile catalog. It returns a `LibraryProfileWorkspaceSnapshot` containing:

- the complete installed library in stable package/path order;
- profiles in stable name/workspace order;
- active entries resolved as installed, missing, or ambiguous;
- installed mods that are inactive in each profile;
- normalized duplicate package IDs;
- a SHA-256 fingerprint covering library identity, profile order, game version, and resolution state.

Generation time is observational metadata and is excluded from the fingerprint. Input discovery order therefore cannot create a false workspace change.

## Ownership

The service is part of the Client because it interprets and presents collected library data. It does not touch RimWorld and does not belong in the Companion mod.

## Next pass

Later Epic C passes can consume this snapshot for atomic profile editing, external-change reconciliation, UI refresh, and persistent workspace selection without recreating profile/library joins.

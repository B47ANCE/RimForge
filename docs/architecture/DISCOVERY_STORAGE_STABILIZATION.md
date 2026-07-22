# Discovery and Storage Stabilization

This pass establishes one authoritative path layout for the PowerShell audit pipeline.

## Shipped, read-only data

- `Database.Curated`

Curated assets are resolved from `Database.Curated` first, with legacy root and `Database` locations supported only as compatibility fallbacks.

## Generated and user-owned data

- `Output/Profiles`
- `Output/Cache`
- `Output/Database/Generated`
- `Output/Database/User`
- `Output/Reports`
- `Output/Logs`

Audits no longer clear the entire `Output` directory. Only replaceable report subfolders are refreshed.

## Discovery

RimForge combines optional configured roots with automatically discovered Steam libraries. Workshop content, local mods, and official `Data` content are resolved independently. Missing roots and individual unreadable mod folders are warnings rather than fatal errors.

## Optional curated systems

Evidence, taxonomy, and blueprint data improve the audit but are not prerequisites for discovery or dependency analysis. Missing optional curated files disable only their own stage and no longer abort the audit.

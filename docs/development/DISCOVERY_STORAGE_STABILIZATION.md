# Discovery & Storage Stabilization

## Root cause

The native scanner depended primarily on `Config.json` root folders. In the affected installation, those paths pointed to an old Steam library while RimWorld was installed under another library. Workshop content can also live in a third library. Additionally, Windows Steam registry paths were escaped incorrectly, preventing reliable automatic recovery through the registry.

## New discovery rules

1. Read configured roots as optional hints.
2. Discover the Steam installation from the Windows registry.
3. Parse `steamapps/libraryfolders.vdf` and enumerate every library.
4. Inspect each library independently for:
   - `steamapps/workshop/content/294100`
   - RimWorld's app manifest and actual `installdir`
   - `<RimWorld>/Mods`
   - `<RimWorld>/Data`
5. Deduplicate all valid roots.
6. Skip unavailable roots and continue.
7. Convert individual mod failures into item-level diagnostics rather than aborting the complete scan.

## Generated storage

Generated data belongs beneath `Output`. The application creates the standard directory layout on demand. `Database.Curated` remains at the repository root because it is shipped, read-only knowledge. An empty generated database directory is normal until database generation runs.

## Validation

- Start with stale or empty `RootFolders` in `Config.json`.
- Confirm official content is discovered from the installed RimWorld `Data` folder.
- Confirm Workshop mods are discovered when the Workshop library differs from the game library.
- Confirm missing optional roots produce progress warnings rather than `Native scan failed`.
- Confirm one malformed/inaccessible mod does not abort the remaining audit.
- Confirm corrected paths are saved after automatic detection.

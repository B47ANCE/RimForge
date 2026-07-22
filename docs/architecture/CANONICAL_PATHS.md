# Canonical RimForge Paths

`Database.Curated` is the only shipped read-only database root.

Generated and user-owned state lives under `Output`:

- `Output/Profiles`
- `Output/Cache`
- `Output/Database/Generated`
- `Output/Database/User`
- `Output/Reports`
- `Output/Logs`

Steam Workshop, local Mods, and official RimWorld Data content are discovered independently across all Steam libraries. Configured roots are optional hints and missing roots are non-fatal.

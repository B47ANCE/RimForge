# Ember migration guide

RimForge 2.1.0-alpha.5 adds persistent incremental state. No profile or generated database migration is required.

## New runtime files

- `Cache/Incremental/ModState.json`
- `Cache/AboutMetadata/*.json`
- `Output/IncrementalAudit.json`

The first run establishes the baseline and will still scan every mod. On later runs, unchanged `About.xml` metadata and evidence results are loaded from cache. A changed source file invalidates its own cache entry automatically.

## Resetting incremental state

Delete `Cache/Incremental` and `Cache/AboutMetadata`, or delete the entire `Cache` folder. The next audit performs a complete baseline run.

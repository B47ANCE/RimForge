# Epic C Pass 4 Durable Profile Catalog State

Pass 4 moves profile catalog metadata out of `MainWindow` file handling and into `IProfileCatalogStateStore`.

The canonical `ProfileCatalogState.json` stores favorite and user-locked profile names. Values are trimmed, case-insensitively deduplicated, and sorted before an atomic staged write. Loading automatically recognizes the former `ProfileShellState.json` format, converts it to the typed model, and writes the canonical file without discarding existing preferences.

The client continues to update catalog names during profile rename and remove entries during deletion or refresh. UI collections remain projections of the persisted service state rather than owning its serialization format.

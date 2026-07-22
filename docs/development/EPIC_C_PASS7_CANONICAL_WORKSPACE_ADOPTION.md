# Epic C Pass 7 Canonical Workspace Adoption

Pass 7 makes the deterministic library/profile projection from Pass 1 the live client source of truth.

After installed library discovery and profile loading, `MainWindow` creates one `LibraryProfileWorkspaceSnapshot`. Active load-order rows use its resolved profile references, while inactive rows use its per-profile installed inventory. The established presentation logic still chooses the preferred display record for ambiguous duplicates and applies official-content rules for the built-in profile.

In-place saves from Mod Sorter and Issue Viewer refresh the snapshot before selecting the updated profile. Full profile reloads rebuild it after catalog ordering. The snapshot fingerprint is written to activity diagnostics, making unexpected workspace churn observable.

This closes the parallel join path between the library and profile UI while preserving existing editing and analysis behavior.

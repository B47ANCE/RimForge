# Epic C Pass 8 Canonical Profile Readiness

Pass 8 gives every `ProfileLibraryProjection` a deterministic readiness summary.

A profile is **blocked** when Core is missing or unresolved, an active package is not installed, an active package resolves ambiguously, or the active order contains duplicate package IDs. It receives a **warning** when all references resolve but one or more mods do not declare support for the profile's target RimWorld version. Otherwise it is **ready**.

The summary carries counts and user-facing reasons. Profile management presents readiness next to editable, locked, or official state. The activation command checks the canonical summary before invoking `IProfileWorkspaceService`, ensuring a blocked profile cannot modify RimWorld's active configuration.

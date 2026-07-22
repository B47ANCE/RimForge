# Epic C Pass 2 Atomic Profile Editing

Pass 2 introduces a transaction boundary between interactive load-order changes and profile persistence.

`IProfileEditService.CreateDraft` normalizes a proposed order against the canonical workspace snapshot and returns an immutable draft. Its change set identifies added, removed, and moved packages before any files change. Validation blocks locked profiles, empty orders, removal of Core, duplicate entries, missing installations, and ambiguous package IDs.

`CommitAsync` compares the draft's base workspace fingerprint with the latest projection. A mismatch is reported as stale and performs no write. Valid drafts use `IProfileWorkspaceService.SaveLoadOrderAsync`, retaining the existing atomic source/workspace writes, backups, and rollback behavior.

This layer is UI-independent so Mod Sorter, profile management, undo, and future bulk operations can share the same validation and concurrency rules.

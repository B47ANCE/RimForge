# Epic C Pass 3 External Profile Conflict Resolution

Pass 3 turns external `ModsConfig.xml` changes into an explicit conflict-resolution workflow.

The existing file monitor and comparison service still detect and describe changes. `IExternalProfileConflictService` now owns the resolution boundary:

- **Adopt External** persists RimWorld's active order into the selected RimForge profile using the existing atomic backup and rollback path.
- **Restore RimForge** activates the selected RimForge profile using the existing recovery-file path.
- **Defer** records a no-write outcome and clears the immediate prompt without acknowledging or modifying the watched file.

Locked profiles cannot adopt external changes. The monitor is acknowledged only after a successful file-changing resolution, preventing failed or deferred operations from being mistaken for synchronized state.

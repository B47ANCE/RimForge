# Epic E Pass 2 — Transactional Repair Execution and Recovery

Status: complete
Version: 2.2.0-alpha.75

## Outcome

Ready repair plans now execute through a single serialized transaction boundary. Before the mutation delegate runs, RimForge atomically persists a journal containing the plan, issue, deterministic planning key, timestamps, state, and audit trail. A transaction completes only as committed, rolled back, cancelled, failed, or recovery-required.

## Failure and recovery behavior

- Failed mutations enter rollback with a non-cancellable recovery token.
- Cancellation also enters rollback; a successful restore produces a terminal cancelled outcome.
- Rollback failure leaves a durable `RecoveryRequired` journal rather than claiming success.
- Startup inspection promotes journals left in non-terminal states to `RecoveryRequired` and surfaces them to the user.
- `RecoverAsync` accepts an operation-specific restore delegate and durably records the recovered rollback.
- Journal updates use staged atomic replacement and execution is serialized to prevent overlapping repair mutations.

The profile workspace service remains the owner of atomic `ModsConfig.xml`/workspace writes and backup restoration. The transaction executor owns orchestration, state, recovery discovery, and auditability.

## Product integration

Issue Viewer calculates the canonical load order without first mutating the visible profile, executes the atomic profile save inside the transaction, projects the committed profile only after success, and restores the visible profile projection on non-success. Outcomes appear in the Issue Viewer live region, activity stream, and notification queue with an audit action.

Successful execution refreshes shared analysis, profile readiness, the Issue Viewer, Mod Sorter, and ForgeView selection/provenance context.

## Verification

`Tests/EpicEPass2TransactionalRepairExecution-Test.ps1` protects the transaction and UI integration contracts. Executable coverage verifies committed audit order, journal persistence, failure rollback, cancellation rollback, interrupted-journal discovery, and explicit recovery completion.

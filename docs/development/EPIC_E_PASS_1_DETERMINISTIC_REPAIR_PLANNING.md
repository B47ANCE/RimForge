# Epic E Pass 1 — Deterministic Repair Planning

Status: complete
Version: 2.2.0-alpha.74

## Outcome

Repair analysis now produces an immutable preview contract before any execution path can mutate profile or filesystem state. Equivalent issues, mod inventories, and profile contexts produce the same deterministic key regardless of input ordering.

Every plan carries:

- canonical analysis and recommendation evidence;
- explicit confidence and safety classifications;
- ordered repair steps and an expected result;
- affected package and filesystem preview scopes;
- captured active-profile, lock, workspace, configuration-directory, and affected-mod preconditions.

## Mutation boundary

`RepairPlanner` performs read-only planning. Issue Viewer captures the current profile/filesystem context and rebuilds the plan immediately before automatic execution. A plan whose preconditions are missing or stale becomes `BlockedByPreconditions`; the workflow shows the failed conditions and returns before load-order or filesystem mutation.

The preview identifies planned paths but declares `PerformsWrites = false`. Transactional writes, rollback, and recovery remain Epic E Pass 2 responsibilities.

## Verification

`Tests/EpicEPass1DeterministicRepairPlanning-Test.ps1` protects plan metadata, deterministic ordering, preview/no-write semantics, and the execution boundary. Executable tests cover blocked planning without a profile, ready planning with satisfied preconditions, evidence/safety/confidence projection, and stable keys across reordered inputs.

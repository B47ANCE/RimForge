# Epic A Pass 3 Resilience

Epic A Pass 3 is complete. RimForge now validates its platform before discovery, detects interrupted runs, preserves durable state, and provides authenticated update staging and rollback boundaries.

## Self-validation

`IPlatformValidationService` verifies required configuration and performs cancellable write probes against the workspace, cache, sessions, and diagnostics directories. It publishes the consolidated outcome through shared runtime health. A blocking validation failure stops coordinated startup before library analysis begins.

## Crash and session recovery

`IApplicationRecoveryService` atomically writes `active-run.json` when coordinated startup begins. A marker found on the next launch identifies the interrupted run. The marker is removed only after composed services shut down successfully; failures retain it for the next launch. Existing Forge Session interrupted-state restoration and durable Evidence backup recovery remain complementary subsystem recovery mechanisms.

## Signed update channels

`ISignedUpdateService` accepts updates only for channels with explicitly pinned RSA public keys. The exact manifest bytes are verified with RSA-PSS/SHA-256, followed by package SHA-256 verification. Unknown channels, altered manifests, altered packages, unsupported schemas, absolute paths, and traversal paths fail closed.

Verified packages are copied into application-data staging with a transaction document. Installation remains the responsibility of an external updater so the running client never overwrites itself.

## Rollback

Before installation, listed application files can be captured beneath the rollback root. Restoration uses path containment checks and atomic per-file replacement. The update contract never deletes unrelated installation files and cannot address protected state roots.

## State preservation

`IStatePreservationService` records protected roots and SHA-256 hashes for critical configuration. Install/rollback roots are rejected if they contain or are contained by output, profiles, caches, sessions, or diagnostics. Update transactions carry the protected-root manifest so an external updater can enforce the same boundary.

## Trust provisioning

The application composes the update service with no trusted keys by default, which disables update staging safely. Release engineering must provision reviewed channel public keys; private signing keys must never be stored in this repository.

## Verification

Execution coverage validates healthy and unhealthy lifecycle behavior, interrupted-run detection, clean completion, protected-root enforcement, manifest tamper rejection, untrusted-channel rejection, package hashing, transactional staging, rollback capture, and rollback restoration. `tests/EpicAPass3Resilience-Test.ps1` certifies composition and architectural boundaries.

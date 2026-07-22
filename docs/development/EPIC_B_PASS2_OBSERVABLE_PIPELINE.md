# Epic B Pass 2 Observable Analysis Pipeline

Epic B Pass 2 exposes the existing analysis engine as a typed sequence of observable stages. It preserves the established dependency, curated-rule, cycle, profile-validation, locking, and tri-hybrid ordering algorithms.

## Pipeline stages

Every asynchronous run publishes the same ordered stages: indexing, relationship resolution, rule evaluation, graph validation, profile validation, load-order planning, finalization, and completion. `ModAnalysisResult.Stages` records elapsed time for each stage, while `IProgress<AnalysisProgress>` provides live typed progress without coupling the analysis project to WPF.

Cancellation is checked at stage boundaries, while traversing dependency cycles, and while constructing load-order plans. The synchronous compatibility entry point continues to use the same core pipeline without creating a second implementation.

## Diagnostics and deterministic output

The result diagnostic stream now projects every snapshot issue with its severity, owning package, and related packages. Consumers no longer need a special path for curated-rule failures.

Relationships, dependency maps, dependent maps, and cycles are emitted in stable order. The SHA-256 input fingerprint now covers dependency and ordering metadata, incompatibilities, supported versions, active order, target version, and user locks, so every input capable of changing the result invalidates the analysis identity.

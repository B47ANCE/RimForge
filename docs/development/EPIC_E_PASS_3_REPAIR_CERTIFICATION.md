# Epic E Pass 3 — Repair Engine Certification

Status: complete
Version: 2.2.0-alpha.76

## Certified policy

RimForge now applies `rimforge.repair-safety.v1` at plan creation and again at the transaction boundary. The only automatic tuple on the initial allowlist is a canonical `LoadOrderViolation` repaired by deterministic `ReorderProfile`. It must have high planning confidence, satisfied preconditions, a non-destructive preview, and an explicit user confirmation from the Issue Viewer workflow.

Anything absent from the allowlist cannot enter automatic execution. Destructive plans are classified separately and the executor rejects them without explicit confirmation. Assisted, uncertain, unsupported, and uncertified plans remain preview/manual workflows.

## Companion runtime-evidence rule

All three runtime-derived issue classes are certified advisory-only:

- `RuntimeObservedConflict`
- `RuntimePerformanceRegression`
- `RuntimeIntegrationFailure`

They map to inspection, never advertise auto-fix, never enter the automatic allowlist, and are rejected by the transaction executor if a caller attempts to treat runtime evidence as mutation authority. Runtime evidence can explain and prioritize a repair; it cannot independently perform one.

## Audit and enforcement

Transaction journals persist the certification policy ID and safety class. The first audit event records the policy and certification reason. UI bulk repair selection admits only certified allowlisted automatic plans, and the confirmed execution call carries explicit authorization.

## Verification

`Tests/EpicEPass3RepairCertification-Test.ps1` protects the allowlist, runtime deny rules, executor enforcement, and UI authorization boundary. Executable coverage proves allowlisted profile repair, uncertified automatic rejection, destructive rejection without confirmation, confirmed manual destructive authorization, and advisory-only certification for every runtime-derived issue class.

This completes Epic E. Companion Gate 3’s unsafe-runtime-repair check is complete; its remaining real-runtime, packaging, performance, and security certification work stays scheduled after Epic E.

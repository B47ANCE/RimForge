# Epic B Pass 1 Analysis Execution Foundation

Epic B Pass 1 strengthens the existing native analysis engine without replacing its dependency, issue, cycle, curated-rule, replacement, locking, and tri-hybrid ordering logic.

## Canonical request

`ModAnalysisRequest` separates the complete installed library from the optional active profile load order, target RimWorld version, and user position locks. The installed library is always indexed in deterministic package/path/identity order. Active profile data scopes active-dependency and current-order findings; it does not remove inactive installed mods from the authoritative dependency model.

## Execution result

`ModAnalysisResult` carries the immutable `ModAnalysisSnapshot`, structured diagnostics, and `AnalysisExecutionMetrics`: installed-library count, active-profile count, relationship/issue/cycle counts, elapsed time, and a deterministic SHA-256 input fingerprint.

The fingerprint includes normalized installed-mod identity, path, modification time, active order, and target version. Equivalent discovery results therefore produce the same fingerprint regardless of enumeration order.

## Cancellation and integration

`IModAnalysisEngine.AnalyzeAsync` checks cancellation before indexing and throughout major analysis phases. Forge DNA now calls this boundary and continues to project its existing cached per-mod records from the returned canonical snapshot. UI execution remains owned by the shared background-task service.

The synchronous `Analyze` entry point remains temporarily for non-interactive internal compatibility, but delegates to the same analysis core and does not form a second engine.

## Verification

Execution tests verify complete-library scope, active-profile metrics, deterministic fingerprints under reversed discovery order, relationship preservation, and cancellation. `tests/EpicBPass1AnalysisFoundation-Test.ps1` certifies contract and integration ownership.

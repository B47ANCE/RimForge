# RimForge Engineering Philosophy

## Central Commands, Not View-Specific Shortcuts

User actions must have one stable command identity. Keyboard shortcuts, buttons, context menus, and future command-palette entries should invoke the same command rather than duplicating feature logic in individual views. Command availability must be context-aware and owned by the command framework.

RimForge is built as a professional desktop engineering platform. This document defines how development passes are planned, implemented, validated, packaged, and handed off. It complements `ARCHITECTURE.md` and `DESIGN_SYSTEM.md`.

## 1. Build on reality

Every pass begins from the latest validated repository snapshot. The repository is the source of truth; patches must never be generated from assumed, reconstructed, or remembered code when the current tree is available.

A newer validated snapshot supersedes every older working copy.

## 2. Preserve the last good build

A working build is the baseline. If a pass breaks compilation, tests, startup, or a validated workflow, correct and reissue that pass before beginning another feature. Do not stack new work on a broken baseline.

## 3. Inspect before changing

Before implementing a feature:

- inspect the real APIs and ownership boundaries;
- locate every affected call site;
- identify cross-feature state and resource dependencies;
- review the existing tests and packaging manifest.

Do not introduce helper calls, files, resources, events, or namespaces that do not exist in the current tree.

## 4. Validate before packaging

A release package is not ready until it has been checked for:

- compile-consistent method and type references;
- required namespace imports;
- XAML and resource-scope safety;
- complete file inclusion;
- tests that reference real files and avoid reserved shell variables;
- documentation and version consistency;
- archive integrity.

Packaging defects are release defects.

## 5. Runtime is the final gate

Static tests and successful compilation are necessary but not sufficient. WPF resource resolution and view construction can fail only at runtime. A pass is considered validated only after:

1. required regression tests pass;
2. the solution builds successfully;
3. RimForge launches successfully;
4. the changed workflow is exercised manually.

## 6. Keep the shell thin

`MainWindow` is an application shell and cross-feature coordinator. Features own their local presentation, interaction forwarding, and focused behavior. Shared application concepts belong in authoritative shared contexts or services.

## 7. One authoritative state

Search, selection, profiles, forge activity, and other global concepts must have one source of truth. Features subscribe to that state rather than maintaining competing copies.

## 8. Integrate instead of duplicate

When adding a capability, identify every existing workspace that should understand it. Search, selection, Issue Viewer, Mod Sorter, ForgeView, Inspector, Dashboard, and future tools should cooperate through shared models and contracts.

## 9. Complete user workflows

Feature passes should deliver coherent, discoverable workflows rather than disconnected controls. Partial infrastructure is acceptable only when the user-facing boundary is explicit and the next dependency is documented.

## 10. Protect the user's time

Patch delivery must be predictable:

- lightweight changed-file packages by default;
- structural packages only when files move, are deleted, or the tree must be resynchronized;
- full PowerShell apply commands with the real expected extraction path, never placeholders;
- no requirement to extract patch contents into the repository;
- all required tests listed accurately;
- known corrections folded into one replacement pass whenever practical.

## 11. Documentation follows the implementation

Every significant pass updates `ROADMAP.md`, `CHANGELOG.md`, and `RELEASE_NOTES.md`. Architectural or process changes also review `ARCHITECTURE.md`, `CONTRIBUTING.md`, `DESIGN_SYSTEM.md`, `README.md`, configuration files, and `.gitignore` as applicable.

Documentation describes the current implementation. Code is never changed merely to preserve outdated documentation.

## 12. Production repository hygiene

Files in the repository must support the production runtime, tests, an active migration, documentation, or a documented developer tool. Rejected concepts, backups, obsolete implementations, duplicate assets, and dead code do not remain in-tree.

## 13. Engineering tool first

RimForge should communicate precision, transparency, reliability, and confidence. Features that appear impressive but reduce predictability, clarity, or control must be redesigned.

## Release-pass checklist

Before handing off a patch:

- [ ] Work began from the latest validated repository snapshot.
- [ ] Existing APIs and call sites were inspected directly.
- [ ] New and changed files are present in the package manifest.
- [ ] Referenced tests exist in the package or validated baseline.
- [ ] PowerShell tests avoid reserved automatic variables such as `$Host`.
- [ ] Modified XAML is structurally valid.
- [ ] WPF resources used by extracted views are in valid scope.
- [ ] Version and documentation agree.
- [ ] Apply instructions contain a complete path with no placeholders.
- [ ] Windows build and runtime launch are identified as required gates when unavailable in the packaging environment.


## Feature Completeness Before Polish

RimForge prioritizes complete, functional capabilities before visual or wording refinement. During feature implementation, UI changes are made only when required to expose or operate a capability. Layout tuning, terminology refinement, animation, spacing, and aesthetic polish are intentionally deferred until the planned feature surface is substantially complete.

## Shared Context Ownership

Application-wide state has exactly one authoritative owner. Search query state belongs to `ISearchContext`; mod selection belongs to `ISelectionService`; selection navigation history belongs to `INavigationContext`; active profile scope belongs to `IProfileWorkspaceStateService`; and Forge lifecycle state belongs to `IForgeSessionService`. Feature views consume these contexts and must not create competing local copies.

## One finite execution lifecycle

Every finite long-running feature operation enters through `RunFeatureTaskAsync` and executes through `IBackgroundTaskService`. Feature code must not create isolated `Task.Run`, thread, cancellation-source, progress, busy-state, or error-recovery pipelines. Cancellation is cooperative and token-based; threads and child processes are never abandoned. Progress includes the current operation, technical detail, discovery detail, current file, processed/total counts, percentage, and elapsed time. UI state must recover after success, cancellation, and failure.

Service-owned continuous streams and watcher debounce are allowed scoped cancellation lifecycles because they coexist with finite work. They remain application-owned, explicitly stoppable, and excluded from the single finite-operation slot.
## Single Communication Surface

Routine application feedback should use the Forge Notification Bar above the Control Center whenever practical. Features publish facts or notification requests; they do not create their own popups or transient banners. Active progress has first claim on the shared surface, and notification priority is deterministic. Modal dialogs remain reserved for destructive actions, security-sensitive decisions, or cases where work cannot safely continue without explicit confirmation.



## Proactive, Reversible Assistance

When RimForge can safely prevent an invalid working configuration, it should do so proactively. Automatic changes must be transparent, recorded in the Activity Feed, communicated through the Control Center, and reversible as one user-level undo operation. Settings must preserve an explicit Ask or Manual mode for users who prefer confirmation or direct control.

## Cohesive Feature Delivery

Capabilities that form one user workflow should be designed, implemented, tested, documented, and released together. A pass should normally include the authoritative engine, required UI, shared-service integration, commands, undo, notifications, persistence, and acceptance tests. Avoid exposing partial workflows across many releases when a complete vertical slice can be delivered safely in one pass.
## Cohesive Feature Suites

Related capabilities are implemented and accepted as one complete vertical slice: domain contracts, authoritative service behavior, UI integration, persistence, recovery, notifications, tests, and documentation. A suite is not considered complete while its workflow depends on future passes for core safety or usability. This reduces contract drift and avoids exposing partially integrated subsystems.



## Natural Discovery
RimForge introduces capabilities when users naturally reach the workflow where they become useful. Every major feature must define how it is discovered and which Engineering Academy overlay teaches it. Lessons explain what the capability does, why it matters, demonstrate a real example, and invite immediate use. Progressive discovery is onboarding rather than restriction: tutorials are replayable and users can expose all advanced features immediately.

## Architectural Decision Governance

Architectural proposals, design explorations, and implementation options remain provisional until the project owner explicitly accepts them. Discussion alone does not make a direction canonical.

Once explicitly approved, an architectural decision becomes part of RimForge's design language and must be preserved across later passes unless the project owner explicitly revises or retires it. Implementation plans and reviews must distinguish clearly between:

- **Proposed** — under discussion and safe to revise.
- **Approved** — canonical direction that implementations must preserve.
- **Implemented** — approved direction present in the repository.
- **Validated** — implemented direction covered by build, tests, and acceptance review.

# Pass 40 — Engineering Workstation Shell Completion

## Status

Implemented. Local Windows build, boot, and interactive acceptance remain required before the pass is marked Validated.

## Delivered architecture

### Engineering Command Bar

The permanent shell now hosts global Back, Forward, transaction Undo, Reforge, unified search, and application status. Command placement is stable and behavior is separated from presentation through services and semantic resources.

### Global navigation

`IGlobalNavigationService` owns browser-style workstation history independently of Undo and Inspector collection navigation. History snapshots preserve the active workspace page, selected package, search query, and profile name. Ctrl+Left and Ctrl+Right navigate this history.

### Inspector collection navigation

Inspector Previous and Next now move through the currently active collection. Search results take precedence while a query is active; otherwise the active workspace determines the collection. Ctrl+Up and Ctrl+Down use the same behavior.

### Application status

`IApplicationStatusService` is the canonical status projection. It exposes Ready, Loading, Scanning, Forging, Cancelling, and Error states. Existing background-task and manual status changes are projected into this service, and the command bar consumes only the centralized snapshot.

### Reforge

Reforge performs an in-process library and profile refresh without restarting the executable. It captures and restores page, selected profile, search query, and selected mod when those entities remain available. A warning is shown only when unsaved load-order edits cannot be reconstructed.

### Settings workspace

Settings is a five-tab workspace: General, Appearance, Profiles, Automation, and Advanced. Each tab owns its scrolling surface. Skin Manager has a stable home under Appearance.

### Skin readiness

The command bar uses semantic dynamic resources and template-based control styles. Pass 40 does not introduce additional skins; it establishes the shell boundary and documents remaining legacy hardcoded presentation values for later feature-by-feature migration.

## Deferred by design

- Additional community skins and external skin package loading.
- Chrome+ and Chrome++ presentation enhancements.
- Persisting navigation history across application restarts.
- Reconstructing unsaved load-order transactions across a Reforge operation.

These are separate approved or proposed initiatives and are not required for Pass 40 completion.

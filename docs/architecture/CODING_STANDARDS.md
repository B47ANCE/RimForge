# RimForge Coding Standards

## Project responsibilities

- `RimForge.App` composes the desktop application and owns application-specific views and workflows.
- `RimForge.Core` contains domain models, contracts, and business rules. It references no other RimForge project.
- `RimForge.Infrastructure` implements Core contracts for files, configuration, discovery, and persistence.
- `RimForge.PowerShellBridge` isolates legacy PowerShell execution and migration adapters.
- `RimForge.UI` contains reusable WPF presentation assets and no application business logic.

## Naming

- One public type per file; file name matches the public type.
- Interfaces use the `I` prefix.
- Async methods use the `Async` suffix and accept `CancellationToken` where meaningful.
- Services use role-based names such as `ModLibraryService`, not generic names such as `Manager` or `Helper`.
- Views end in `View`; reusable large composites end in `Panel`; modal windows end in `Dialog`.
- View models end in `ViewModel`; converters end in `Converter`; attached behaviors end in `Behavior`.
- XAML resource keys use semantic PascalCase names; colors end in `Color`, brushes in `Brush`, durations in `Duration`.

## Namespace layout

Namespaces mirror project-relative folders. Avoid broad catch-all namespaces such as `Common`, `Misc`, or `Utilities` unless the contained abstraction is genuinely cross-cutting.

## Dependency direction

`App -> Infrastructure / PowerShellBridge / UI -> Core`

`Core` must remain independent. `UI` must not reference `App`, `Infrastructure`, or `PowerShellBridge`.

## Source conventions

- Nullable reference types and implicit usings are repository-wide defaults.
- Prefer immutable records for value-like domain models.
- Prefer constructor injection for services.
- Avoid service locators and static mutable state.
- Keep code-behind limited to window mechanics and transitional UI wiring; new workflows belong in view models/services.
- Do not swallow exceptions. Add context or translate them at subsystem boundaries.

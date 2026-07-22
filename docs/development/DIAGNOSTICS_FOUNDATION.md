# RimForge diagnostics foundation

Step 1B introduces a small, dependency-free diagnostics API under
`RimForge.Core.Diagnostics`.

## Behavioral boundary

This step does not connect diagnostics to application startup, profiles, scanning,
WPF controls, file output, or any existing event path. There are intentionally no
new call sites. The types are dormant until a later, separately validated step
uses them.

## Components

- `RimForgeLogger` creates structured entries and writes them to
  `System.Diagnostics.Trace`.
- `RimForgeTrace` provides optional operation IDs that flow through asynchronous
  calls.
- `RimForgeTimer` measures an explicitly selected operation and records one timing
  entry when disposed.

Diagnostics subscribers are isolated with exception handling so a failing observer
cannot break the workflow being observed.

## Future adoption rule

Instrumentation must be introduced one workflow at a time. Each instrumentation
change must pass the normal RimForge runtime validation gate before another
workflow is instrumented.

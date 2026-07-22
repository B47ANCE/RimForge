# Pass 40 — Engineering Workstation Shell Foundation

## Scope implemented

Pass 40 begins by establishing permanent shell surfaces without prematurely implementing unapproved behavior.

- Added a unified Engineering Command Bar host.
- Consolidated navigation placeholders, transaction undo, reserved Reforge placement, unified search, and application status into the shell.
- Exposed the pending undo description directly through the Undo tooltip.
- Projected background-task state into the application-status surface while preserving existing status producers.
- Converted Settings into General, Appearance, Profiles, Automation, and Advanced tabs.
- Gave every Settings tab an independent scrolling surface.
- Added the first Skin Manager placeholder under Appearance using semantic resources only.
- Added semantic command-bar resources to the application resource dictionary.

## Deliberately reserved

The following controls are visible as architectural reservations but disabled:

- Browser-style Back and Forward.
- Reforge.

These behaviors require explicit approval of history-entry semantics and workspace-preservation contracts before implementation. Their visible placement is approved shell architecture; their behavior is not implied to be approved.

## Validation

Run:

```powershell
pwsh -NoProfile -File .\Tests\Pass40WorkstationShellFoundation-Test.ps1
dotnet build .\RimForge.sln --configuration Release
```

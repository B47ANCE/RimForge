# RimForge Runtime Validation Checklist

Use this checklist after every code-changing micro-step. Do not begin the next step until all applicable checks pass.

## Build and launch

```powershell
dotnet clean .\RimForge.sln
dotnet build .\RimForge.sln
.\src\RimForge.App\bin\Debug\net10.0-windows\RimForge.exe --logging
```

- [ ] Build succeeds.
- [ ] Application launches without an unhandled exception.
- [ ] Dashboard finishes loading.
- [ ] Navigation remains responsive.

## Launch Bar and profiles

- [ ] Select an existing profile.
- [ ] Create a temporary profile.
- [ ] Rename the temporary profile.
- [ ] Clone the temporary profile.
- [ ] Favorite and unfavorite a profile; verify persistence after restart when relevant.
- [ ] Lock and unlock a profile.
- [ ] Export a profile.
- [ ] Import the exported profile.
- [ ] Delete only the temporary test profiles.

## Forge workflow

- [ ] Ignite the Forge.
- [ ] Confirm progress updates appear.
- [ ] Confirm the elapsed timer advances.
- [ ] Cancel when the operation supports cancellation.
- [ ] Confirm the UI returns to an idle state.

## Dashboard and sorter

- [ ] Active and inactive lists populate.
- [ ] Scrolling works in each list and on the dashboard.
- [ ] Mod Inspector updates when selection changes.
- [ ] Drag-and-drop behavior remains functional.
- [ ] Save and refresh actions remain functional.

## Settings and Console

- [ ] Settings load correctly.
- [ ] Change a harmless setting and save it.
- [ ] Restart and confirm persistence.
- [ ] Game log monitoring still functions.

## Visual regression check

- [ ] ForgeCheckBox visuals are unchanged.
- [ ] Dialog styling is unchanged.
- [ ] Hover states remain readable.
- [ ] ComboBoxes remain usable.
- [ ] Forge Evidence cards and badges remain visually correct.
- [ ] No stock Windows control styling has reappeared.

## Failure rule

When any item fails:

1. Stop the roadmap.
2. Record the exact failing action and any log or exception.
3. Revert or repair only the current micro-step.
4. Repeat the checklist before proceeding.

# ForgeView Suite — Offline QA Checklist

## Required build gate
- [ ] `dotnet clean .\RimForge.sln`
- [ ] `dotnet build .\RimForge.sln`
- [ ] RimForge launches without a XAML/runtime exception.
- [ ] `Tests\ForgeViewSuite-Test.ps1` passes.

## Graph workflow
- [ ] Open ForgeView after refreshing the library; nodes and directional edges appear.
- [ ] Drag empty canvas space and confirm panning remains smooth.
- [ ] Use the mouse wheel and +/− buttons; zoom stays between useful limits.
- [ ] Use **Fit** and confirm the view returns to its starting position.
- [ ] Click a node and confirm Mod Sorter and Mod Inspector select the same mod.
- [ ] Select a mod elsewhere and confirm the corresponding node and adjacent links gain focus.

## Shared context
- [ ] Enter a global search term and confirm matching ForgeView nodes highlight.
- [ ] Toggle profile-only/full-library mode and confirm inactive nodes are excluded/included.
- [ ] Use **Open in ForgeView** from Issue Viewer and confirm the intended mod becomes focused.
- [ ] Switch profiles and confirm graph scope updates without requiring restart.

## Relationship intelligence
- [ ] Required relationships render as solid neutral edges.
- [ ] Optional relationships render as dashed blue edges.
- [ ] Load-before/load-after relationships render amber.
- [ ] Incompatibilities render red.
- [ ] Cycle count matches the current Forge analysis summary.
- [ ] A disconnected or cyclic component remains visible rather than disappearing.

## Outline and export
- [ ] Switch to Outline; dependency hierarchy is readable and nodes are clickable.
- [ ] Switch back to Graph without losing selection.
- [ ] Export DOT and open it in a text editor; nodes and relationships are present.
- [ ] Export CSV and confirm source, target, relationship, and description columns.

## Stress and UX notes
- [ ] Test with the largest available mod library and record any pause above two seconds.
- [ ] Confirm node labels remain readable at normal zoom.
- [ ] Confirm no black text appears.
- [ ] Record overlapping nodes, confusing edge direction, clipped content, or navigation friction for the system-wide stabilization sweep.

## Certification state
Feature complete in alpha.39. Final certification is intentionally deferred to the section-wide QA and stabilization sweep.

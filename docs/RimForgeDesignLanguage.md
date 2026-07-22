# RimForge Design Language

## Product voice

RimForge uses forge-themed language for primary actions and progress narration while keeping technical information literal and immediately understandable.

## Canonical terminology

| Concept | UI term |
|---|---|
| RimWorld compatibility metadata | Supported Versions |
| Installed mod directory | Installation Path |
| Overall state | Health |
| Workshop page in a web browser | Open in Browser |
| Workshop page in the Steam client | Open in Steam |
| Mod directory | Open Installation Folder |
| Generated audit stream | Activity Feed |
| Full ecosystem analysis | Ignite the Forge *(Forge Session commit)* |

## Inspector information order

Inspectors use a predictable hierarchy:

1. Visual identity
2. Information
3. Navigation
4. Maintenance
5. Diagnostics
6. Danger Zone

Only sections with working actions should be rendered. Placeholder controls are not included in active workflows.

## Interaction principles

- Never fake progress or add artificial delays.
- Every personality message must map to real engine work.
- Common actions remain visible rather than hidden in context menus.
- Destructive actions must be isolated and require confirmation.
- Technical labels remain clear even when actions use RimForge branding.

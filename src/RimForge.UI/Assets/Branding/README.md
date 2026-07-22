# RimForge UI Assets

`src/RimForge.UI/Assets` is the single canonical root for production UI artwork.

## Structure

- `Branding/Badge` — canonical compact identity source and exact Windows icon derivative.
- `Branding/Logo` — full-size logo artwork for contexts that can support it.
- `Branding/Anvil` — retained forge/health feature artwork; no longer the application identity.
- `Icons/Actions` — reusable action icons.
- `Icons/State` — reusable state and toggle icons.
- `Icons/Source` — content-source icons.
- `Branding/AssetManifest.json` — canonical asset registry and workflow mapping.

Feature projects reference these resources through `RimForge.UI` pack URIs. Do not keep duplicate feature-local copies. Concept sheets, rejected variants, old exports, and deprecated assets do not belong in the production repository.


The supplied `RimForge.Badge.png` is immutable. Runtime sizes may be derived from it without visual redesign.

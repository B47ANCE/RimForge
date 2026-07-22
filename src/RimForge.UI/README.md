# RimForge.UI

Reusable WPF presentation assets for RimForge. This project must not contain application orchestration, persistence, mod discovery, or profile business logic.

## Folder conventions

- `Behaviors/` — attached behaviors and interaction helpers.
- `Controls/` — reusable atomic or composite controls.
- `Converters/` — stateless WPF value converters.
- `Dialogs/` — reusable modal dialog shells.
- `Icons/` — vector icon data and icon-specific resources.
- `Panels/` — reusable large composite panels.
- `Resources/` — non-theme shared presentation resources.
- `Themes/` — colors, brushes, typography, styles, templates, and animation tokens.
- `ViewModels/` — presentation-only view models for reusable UI components.
- `Views/` — reusable views that do not own application workflows.

Namespaces mirror folders: `RimForge.UI.Controls`, `RimForge.UI.Converters`, and so on.

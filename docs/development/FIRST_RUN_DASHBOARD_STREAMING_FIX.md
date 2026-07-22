# First-Run Dashboard Streaming Fix

This pass corrects two first-run experience problems:

1. The onboarding overlay is hosted across both root window columns, so its dimmer and card cover the full RimForge client area instead of only the navigation rail.
2. Parsed `About.xml` records are streamed to the Dashboard before Forge Evidence and dependency analysis finish. These preliminary rows use the pending/gray health state and are replaced by the final analyzed snapshot when scanning completes.

The built-in Vanilla profile no longer hides non-official installed mods from the inactive list. Official content remains active according to the profile, while all other discovered mods remain available for activation.

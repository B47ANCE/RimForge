# RimForge Design System

**RF-DS** defines the visual, interaction, artwork, and language standards for RimForge.

## Brand position

RimForge is a premium industrial engineering workstation for RimWorld modpacks. The visual language combines practical mission-control clarity with restrained blacksmithing and workshop cues.

RimForge is not fantasy, medieval, steampunk, neon science fiction, or a generic game launcher.

## Product identity

- Product: **RimForge**
- Descriptor: **The RimWorld Modpack Engineering Platform**
- Canonical workflow: **Discover → Build → Understand → Repair → Optimize → Play**
- Tone: calm, professional, precise, reassuring, and evidence-led

## Core visual principles

### Readability before decoration

Information hierarchy, state recognition, and safe action selection always outrank atmosphere. Decorative artwork must not compete with text, controls, progress, or evidence.

### One authoritative symbol per concept

The same concept should use the same base symbol across navigation, command bars, status, dialogs, and empty states. Active-work variants may add restrained motion or approved accessories without changing meaning.

### Industrial, maintained, and human-made

Materials may suggest weathered steel, cast iron, aged brass, walnut, parchment, blueprint paper, painted machinery, and carefully used tools. Wear is subtle and functional, not dirty or ruined.

### Calm motion

Motion confirms state changes, progress, focus, or continuity. It must not loop aggressively, block interaction, or obscure evidence.

## Color tokens

Canonical baseline palette:

| Role | Value | Use |
|---|---:|---|
| Background | `#1B1B1D` | Window and deepest workspace |
| Secondary | `#252528` | Secondary surfaces |
| Card | `#2F3136` | Primary cards and panels |
| Raised | `#3A3D42` | Buttons and elevated controls |
| Border | `#4A4D54` | Structural boundaries |
| Forge Orange | `#F28C28` | Primary action and active process |
| Forge Hover | `#FFAA45` | Hover and emphasis |
| Success | `#4CAF50` | Healthy and completed |
| Warning | `#FFC107` | Attention and advisory |
| Error | `#D9534F` | Failure and incompatibility |
| Information | `#4DA3FF` | Informational and selected relations |
| Text | `#FFFFFF` | Primary text |

Secondary text uses light neutral grays with sufficient contrast. **Black text is prohibited** in the application UI.

Tokens must be consumed through shared resources rather than copied as ad hoc values.

## Typography

Inter is the approved UI typeface where redistribution and packaging are properly licensed. Use system fallback fonts when the approved font is unavailable.

- Headings communicate hierarchy, not decoration.
- Body copy uses comfortable line height and avoids dense walls of text.
- Labels use sentence case unless a platform convention requires otherwise.
- Monospace is reserved for paths, package IDs, logs, code, and technical evidence.
- Do not compress text with abbreviations merely to fit a layout.

## Canonical compact identity and brand artwork

The approved circular star badge is RimForge's canonical compact identity. Use the unmodified source asset at `src/RimForge.UI/Assets/Branding/Badge/RimForge.Badge.png` for small-format application identity, including window chrome, the taskbar, shortcuts, compact menus, and installer surfaces. The multi-resolution `.ico` file is a technical derivative of that exact supplied artwork.

- Do not redraw, regenerate, recolor, relight, crop, or reinterpret the badge.
- Preserve its transparent background and complete circular silhouette.
- Prefer the badge over the legacy anvil whenever RimForge must remain recognizable at small sizes.
- The former anvil asset is no longer the application icon. It may remain only as intentional forge/health feature artwork and in historical material.
- Full-size logos and splash artwork are separate assets and must not be substituted automatically for the compact badge.
- Never distribute source font files as project assets.

## Icon family

Canonical workflow symbols:

| Symbol | Meaning |
|---|---|
| Clipboard | Mod List / Mod Sorter |
| Magnifying glass | Issue Viewer / Scan |
| Blueprint | Dependency Map |
| Stack of photos | Texture Tools |
| Cogwheel | Settings |
| Industrial console | Console |
| Hammer | Active Forge operation |

The hammer is primarily an active-process symbol rather than a navigation destination.

Until final assets are integrated, placeholders must remain clearly isolated and must not be mistaken for approved production artwork.

## Artwork language

Production illustration assets use:

- Hand-painted, colony-simulation-adjacent rendering
- Front-facing or compositionally clear silhouettes
- Muted industrial colors
- Soft painterly edges with controlled detail
- Transparent backgrounds for standalone assets unless a scene or wallpaper is explicitly requested
- Subtle wear, scratches, chipped paint, and fingerprints
- Consistent top-left lighting for compositing

Avoid:

- Photorealism
- Cartoon exaggeration
- Medieval or fantasy blacksmith shops
- Steampunk ornament
- Neon and holographic interfaces
- Glossy plastic
- Excessive smoke, sparks, glow, or cinematic contrast
- Text, logos, watermarks, and baked-in UI unless explicitly part of a designed screen

## Splash and loading artwork

RF-DS-BG-001 defines the canonical background direction:

- Quiet industrial engineering workshop
- 16:9 composition
- Approximately the center 40% reserved as clean negative space for logo and loading UI
- Environmental storytelling concentrated near outer edges
- Workbench, blueprints, drafting tools, clipboards, notebooks, consoles, gears, shelves, and diagnostic equipment
- Soft ambient light plus restrained orange forge spill from an unseen off-screen source
- Charcoal, gunmetal, walnut, blueprint blue, parchment, olive displays, and ember-orange accents
- Calm, professional, organized, and welcoming

No people, animals, weapons, armor, giant furnace, lava, molten metal, fantasy props, steampunk, neon, text, logos, vignette, or photorealism.

## Shell and layout

- The command/search bar is visually stable across workspaces.
- Global search is centered independently of left and right command groups.
- Back and Forward remain a paired control.
- Reforge and Undo use matching raised surfaces.
- Page content must not disappear beneath fixed overlays; safe insets are mandatory.
- Active data regions use internal scrolling and virtualization rather than expanding indefinitely.
- Mixed views are allowed when they remain readable at common viewport sizes.

## Lists and scrollbars

- Active lists and evidence surfaces have always-visible scrollbars.
- Scrollbars are rounded and clearly interactive.
- Hover and drag use Forge Orange emphasis.
- Multi-selection remains visible during drag initiation.
- Drag previews must communicate the full group operation.
- Empty states explain why a list is empty and what action is available.

## Search

- Search examples are contextual to the visible workspace.
- Placeholder/example text disappears while focused.
- Feature navigation ranks above content results.
- Results identify source and health consistently.
- One query filters relevant lists, issues, Dependency Map, and discovery projections.
- Escape clears an active query.
- No-results is a designed state, not a blank popup.

## Inspector and evidence

- Hide empty sections rather than displaying empty chrome.
- Technical evidence remains selectable and copyable.
- Package IDs and paths use monospace presentation.
- Tooltips appear promptly and may be pinned when explanation is longer than a label.
- Overflow badges use a clear `+N` form.
- Official content does not receive meaningless blank badges.

Technology badge guidance:

- C# — blue
- Harmony — purple
- XML — orange
- PatchOperations — teal
- Other/unknown — neutral gray

## Health and relationship semantics

- Healthy state uses green.
- Warning uses yellow.
- Error/incompatibility uses red.
- Selected dependency and dependent directions use distinct, documented colors.
- Relationship types also differ by line pattern or shape; color alone is insufficient.
- Official RimWorld content uses the approved official-content identity treatment rather than a generic health anvil.

## Progress and active work

A long-running operation exposes:

- Operation name
- Current phase/action
- Current file or mod when useful
- Processed/total counts when known
- Percentage when meaningful
- Elapsed time
- Cancel action
- Clear terminal status

The active process icon overrides the passive page icon in status contexts. Motion should be restrained: status lights, subtle CRT movement, hammer/forge cues, or small progress animation.

## Dialogs

All modal workflows use the RimForge dialog framework.

Dialogs must provide:

- Clear title and purpose
- Evidence or consequences before destructive actions
- Primary and secondary actions in consistent positions
- Visible focus and keyboard operation
- Progress/cancellation when work continues inside the dialog
- No raw platform message boxes for production workflows unless explicitly approved as an emergency fallback

## Accessibility

- Visible keyboard focus is mandatory.
- Critical actions require accessible names and descriptions.
- Do not encode meaning by color alone.
- Validate common Windows scaling factors and high-DPI displays.
- Maintain readable contrast for normal, disabled, selected, and hover states.
- Motion-sensitive users should not depend on animation to understand status.
- Click targets must remain practical at supported scaling levels.

## Asset organization

Approved assets should be separated from concepts and source studies. Release packaging must include only licensed, approved production assets.

Recommended structure:

```text
Assets/
  Brand/
    Production/
  Icons/
    Production/
  Artwork/
    Production/
  Motion/
    Production/
  Source/
```

Concept, deprecated, and rejected assets should remain outside production folders and must not be referenced by application resources.

## Design acceptance checklist

A UI change is complete only when it has been checked for:

- Visual hierarchy and text contrast
- Keyboard focus and navigation
- High-DPI layout
- Loading, empty, no-results, failure, and disabled states
- Cancellation and recovery where applicable
- Consistent tokens and icon semantics
- No black text
- No placeholder production assets
- Correct shared selection/navigation behavior
- Screenshot review at representative viewport sizes

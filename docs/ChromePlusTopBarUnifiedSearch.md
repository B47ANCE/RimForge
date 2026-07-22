# Chrome+ Top Bar and Unified Search

This checkpoint replaces the persistent left navigation rail with compact command-bar navigation and completes global search as a shared application interaction.

## Shell ownership

- `EngineeringCommandBarView.xaml` owns navigation, browser history, Reforge, Undo, search presentation, and application status.
- `EngineeringCommandBarView.xaml.cs` raises navigation and result-invocation events.
- `MainWindow.Navigation.cs` maps every destination to the continuous-workspace section and updates the active location.
- `MainWindow.Search.cs` owns search projection and navigation-result behavior while `ISearchContext` owns the query itself.

The search host spans all three command-bar columns and is horizontally centered. Left controls and right status have independent layout anchors, so status-label growth cannot shift search.

## Search lifecycle

1. `SearchText` updates the shared search context.
2. The typed application event refreshes active mods, inactive mods, issues, dependency relationships, feature discovery, and ForgeView match IDs.
3. Results are bounded and ranked. RimForge features use a dedicated high score band and therefore appear first.
4. The popup opens through a OneWay computed-state binding. It renders results, validation feedback, or a no-results message.
5. Invoking a mod or issue synchronizes the existing selection model. Invoking a feature delegates to workspace navigation.
6. If the complete result set contains exactly one mod, both its Mod Sorter list row and Mod Inspector are selected automatically.

## Viewport prompts

The active continuous-workspace location selects the idle prompt:

| Viewport | Example guidance |
|---|---|
| Mod Sorter | names, authors, sources, active state |
| Issue Viewer | issue categories, related mods, active state |
| ForgeView | dependency names, `requires:`, sources |
| Texture Tools | textures, DDS, Texture Conversion Tools |
| Settings | profiles, paths, launch configuration |
| Console | activity, game logs, mods, features |

The prompt and TextBox share the same left inset. A focus trigger hides the prompt even when the query is empty.

## ForgeView consistency

ForgeView does not interpret the query again. It receives the package IDs already matched by `IModFilteringService`. Graph and Outline restrict nodes and edges to that set, after which their existing health, relationship, path, and active-profile filters continue normally.

## Validation

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tests\ChromePlusTopBarUnifiedSearch-Test.ps1
dotnet build .\RimForge.sln --configuration Debug
```

Then boot the application and manually verify minimum-width and normal-width command-bar layout, menu navigation, focus/idle prompt transitions, result/no-result seams, feature navigation, source/anvil presentation, cross-workspace filtering, and single-result selection.

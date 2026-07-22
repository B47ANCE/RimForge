# RimForge 6 Stabilization Validation

Run from the repository root:

```powershell
dotnet clean .\RimForge.sln
dotnet build .\RimForge.sln
.\src\RimForge.App\bin\Debug\net10.0-windows\RimForge.exe --logging
```

Validate:

1. The native scan completes without a CollectionView error.
2. Startup and Refresh remain interactive.
3. Workshop mods are discovered from any Steam library.
4. `ludeon.rimworld` and installed DLC are discovered from the RimWorld `Data` folder.
5. Active load-order rows show mod names and correct Official/Workshop/Local source icons.
6. Dashboard active and inactive selections update the Inspector.
7. Official content shows local metadata immediately and Steam Store enrichment afterward.
8. Workshop items show local metadata immediately and Workshop enrichment afterward.
9. Profiles load from `Output\Profiles`; profile create/rename/clone/import/export/lock/delete still work.
10. Ignite the Forge treats unavailable optional curated evidence data as non-fatal.

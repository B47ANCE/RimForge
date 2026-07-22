# Runtime Companion

The Runtime Companion is the accepted bridge between RimForge's desktop analysis environment and a running RimWorld process.

## Components

- `src/RimForge.Companion.Host` — desktop process for Forge Session lifecycle, local IPC, Player.log monitoring, buffering, process health, and session persistence.
- `../RimForge.Companion/src/RimForge.Agent` — normal RimWorld mod for runtime events, Harmony observations, def loading, timing, and diagnostic context.
- `src/RimForge.Protocol` — single shared versioned contract assembly.
- `../RimForge.Companion/mods/RimForge.Runtime` — distributable mod layout.

## Safety contract

The Agent is diagnostics-only. It opens no network port, does not mutate saves or gameplay state, and disables itself when initialization or transport fails. Communication is local named-pipe IPC. The Host treats every payload as untrusted input and validates envelope size, type, protocol version, and session identity.

## Build

Runtime builds require the RimWorld 1.6 managed assemblies:

```powershell
..\RimForge.Companion\build\companion\Build.ps1 -RimWorldDir 'D:\SteamLibrary\steamapps\common\RimWorld'
```

The packaged mod is emitted beneath ignored `artifacts/` storage and versioned from the repository `VERSION` file.

## Validation

Controlled fixtures and the harness live in `../RimForge.Companion.TestSuite`. See that repository's `docs/testing/RUNTIME-TESTING.md`.

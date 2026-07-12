# DirectXTex / texconv

RimForge's texture optimizer requires Microsoft's `texconv.exe`.

Place the executable here:

```text
Tools\DirectXTex\texconv.exe
```

RimForge does not execute any mod code during texture conversion. It uses texconv
only to encode staged PNG images as `BC7_UNORM` DDS files with mipmaps.

The executable is not bundled in this update. Include the correct DirectXTex
license and release packaging when distributing texconv with a future RimForge
release.

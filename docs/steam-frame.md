# Steam Frame support notes

Research notes on getting WolfSharp (and other `BenMcLean.Wolf3D` Godot presentation
layers) running natively on Valve's Steam Frame VR headset.

**Distribution: a native Linux arm64 AppImage**, alongside the existing x86_64 AppImage,
Windows build, and Android (Meta Quest) build. CI plumbing for the arm64 export and
AppImage exists in `.github/workflows/build.yml`.

## Hardware

Steam Frame (released 2026-09-18) runs SteamOS on a Qualcomm Snapdragon 8 Gen 3 SoC:
**arm64**, 16 GB unified LPDDR5X RAM. Unlike Steam Deck/Steam Machine (x86_64), this is
the first mainline SteamOS device on ARM. Valve bridges x86 Windows games via
Proton + FEX binary translation, but a native arm64 Linux build avoids that translation
layer entirely - relevant for a VR title where frame time matters.

## This is a first-party Steamworks-documented target

Valve publishes an official guide: <https://partner.steamgames.com/doc/steamframe/engines/godot>
(Steamworks partner docs, so it may require a partner login to view; findings below were
retrieved via fetch on 2026-09-26 and should be re-verified against the live page before
acting on them).

Key facts from that page:
- **Linux arm64 has been a supported Godot export platform since Godot 4.2.3.** This is
  a normal export preset/architecture setting, not something that requires compiling
  custom export templates. The arm64 binaries are bundled inside the same
  `*_mono_export_templates.tpz` archive as every other platform/arch, just not called
  out as a separate download on the godotengine.org download page.
- **Godot 4.6 beta 1 or later is required** to use the Steam Frame Controller Interaction
  Profile (OpenXR). Our project (`godot/BenMcLean.Wolf3D.VR/project.godot`) currently
  targets Godot **4.5** - see "Godot version" below.
- Foveated rendering needs the OpenXR Vendors plugin (vendor extension support from Godot
  4.2.x on), Godot **4.2.3 minimum for Linux arm64 support** specifically.
- Two build targets are supported for Steam Frame: **Android** (Gradle/OpenXR, same as our
  existing Meta Quest export path) and **Linux arm64** (native, no translation layer).
- **GodotSteam 4.17+** ships precompiled Android arm64 and Linux arm64 libraries, if we
  ever integrate GodotSteam (we don't currently - no Steamworks API usage in this repo).
- Distribution path Valve documents is: Export Project (or headless
  `--export-release`) -> upload via the **Steam DevKit Tool** to the headset, i.e. the
  same path as any other Steam release, going through Steamworks/SteamPipe. That path
  requires becoming a Steamworks partner (Steam Direct's per-title fee, a store page,
  revenue share), which doesn't fit a free/open-source hobby project, so distribution
  stays on the existing GitHub Releases + itch.io pipeline (AppImage), same as every
  other Linux target this project ships.

## Godot version

Project currently targets Godot 4.5 (`config/features=PackedStringArray("4.5", "C#", "Mobile")`
in `godot/BenMcLean.Wolf3D.VR/project.godot`; CI pins `GODOT_VERSION: "4.5.1"` in
`.github/workflows/build.yml`). Since the Steam Frame Controller Interaction Profile
needs Godot 4.6 beta 1+, and Godot 4.6 stable (released 2026-01-26) is reported to have
added first-class Steam Frame/OpenXR 1.1 support (frame synthesis, a universal APK
across OpenXR headsets), **pulling the engine version forward to 4.6 (or later, per
whatever's current when this work starts) looks worth doing before investing effort in
Steam Frame-specific controller bindings.** NuGet caches on this machine already have
both `godot.net.sdk` 4.5.1 and 4.7.2 packages, suggesting a version bump has already been
test-driven locally at some point.

## Implementation state (2026-10-03)

**Done:**
- `godot/BenMcLean.Wolf3D.VR/export_presets.cfg` has a `[preset.3]`, `"Linux (arm64)"`,
  identical to the existing `"Linux"` preset except `binary_format/architecture="arm64"`.
- `.github/workflows/build.yml`'s `export` job matrix has a `linux-arm64` entry using
  that preset. Its AppImage step shares the existing `linux-x64` logic via
  `startsWith(matrix.name, 'linux-')`, parameterized by a `appimage_arch` matrix field
  (`x86_64` / `aarch64`) and `matrix.output_file`.
- The AppImage job's desktop file is a checked-in `packaging/linux/net.benmclean.wolf3d.desktop`
  instead of an inline heredoc.
- `itch-release` has a "Push Linux arm64 build" step (channel `linux-arm64`).
- The arm64 export (Godot editor + export template + `dotnet build` + `godot --export-release`)
  has been verified both cross-platform (WSL2 Ubuntu x86_64 host, cross-exporting to arm64 -
  see "Local verification" below) and natively (built directly on a real Steam Frame, aarch64
  host, same toolchain installed portably with no root since SteamOS's root filesystem is
  read-only).
- **Verified on real Steam Frame hardware**: the native arm64 export, run directly
  (unsandboxed), connects to `SteamVR/OpenXR 2.17.10`, initializes Vulkan on the real GPU
  (`Qualcomm Turnip Adreno (TM) 750`), and creates a working OpenXR swapchain - confirmed
  running in the headset. This resolves the GPU/Vulkan passthrough question that was
  previously open for real hardware.

**Operational note:** a build transferred to a Steam Frame from another machine (e.g. via
a `.7z` archive) can lose the executable bit on the main binary even though the `.sh`
launcher script keeps its own - 7-Zip and similar archivers don't reliably preserve Unix
permissions across platforms. If a transferred build fails with a permission error,
`chmod +x` the actual `BenMcLean.Wolf3D.VR.arm64`/`.x86_64` binary (not just the `.sh`
wrapper) before assuming anything deeper is wrong.

## Local verification (2026-09-26, WSL2 cross-export) - PASSED

Ran the same steps as the CI Linux job, by hand, in WSL2 Ubuntu 24.04 (x86_64 host), to
sanity-check the arm64 export claim before touching CI:

1. Downloaded the Godot 4.5.1 `.NET` (mono) Linux editor + `.NET` export templates
   `.tpz` - same URLs the CI job already uses, no sudo/root needed (installed to
   `$HOME/bin` instead of `/usr/local/bin`).
2. Confirmed `linux_release.arm64` / `linux_debug.arm64` are present inside the
   templates archive, right alongside `linux_release.x86_64` etc.
3. `godot --headless --import`, then `dotnet build` on
   `godot/BenMcLean.Wolf3D.VR/BenMcLean.Wolf3D.VR.csproj` - succeeded, 0 errors.
4. Patched a scratch copy of `export_presets.cfg`'s `[preset.2.options]` (the `Linux`
   preset) to `binary_format/architecture="arm64"` (was `"x86_64"`), ran
   `godot --headless --export-release "Linux" <out>`, then restored the original file
   (no lasting repo changes from this test).
5. **Result: real arm64 binaries**, confirmed with `file`:
   ```
   BenMcLean.Wolf3D.VR.arm64:  ELF 64-bit LSB executable, ARM aarch64, ... dynamically linked, interpreter /lib/ld-linux-aarch64.so.1, for GNU/Linux 5.15.0, stripped
   libgodotopenxrvendors.so:   ELF 64-bit LSB shared object, ARM aarch64, ...
   ```
   The OpenXR Vendors plugin's native `.so` also has a prebuilt aarch64 build, so that
   dependency isn't a blocker either. `godot --export-release` did the arm64 build from
   an x86_64 host without any cross-compiler setup - it's just picking the matching
   prebuilt template/plugin binary and publishing .NET IL against the `linux-arm64`
   runtime pack (a NuGet download), exactly as expected.

   One unrelated, pre-existing warning surfaced during import: `res://addons/godot_meta_toolkit/toolkit.gdextension`
   failed to load (`GDExtension dynamic library not found`) - this is a missing/optional
   Meta-specific addon, unrelated to arm64 or Linux, not investigated further here.

**Conclusion: the arm64 Linux export "just works" on the currently-pinned Godot 4.5.1,
with zero engine-version bump and zero custom template build required.** The Godot
version bump (see above) is still worth doing for Steam Frame's OpenXR controller
profile, but it is not a prerequisite for producing a running arm64 binary.

## Open questions / not yet done

- No decision on Steam Frame Controller Interaction Profile / OpenXR Vendors plugin
  integration - out of scope until the Godot version bump happens.
- Content policy for any future storefront submission still applies:
  `[[feedback_no_content_censorship]]` - label historical Nazi imagery, never censor it.

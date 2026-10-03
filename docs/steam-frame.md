# Steam Frame support notes

Research notes on getting WolfSharp (and other `BenMcLean.Wolf3D` Godot presentation
layers) running natively on Valve's Steam Frame VR headset.

**End goal, as of 2026-09-26: an AppImage and a Flatpak, each for both x86_64 and
aarch64 (four artifacts total).** AppImage covers direct/sideload distribution
(itch.io, GitHub Releases - unchanged from before); Flatpak covers Steam Frame's
Discover/Dolphin Software Center, and incidentally any other Flatpak-capable Linux
desktop. CI plumbing for all four now exists in `.github/workflows/build.yml` - see
"Current implementation state" below for what's actually verified vs. still unverified.

## Hardware

Steam Frame (released 2026-09-18) runs SteamOS on a Qualcomm Snapdragon 8 Gen 3 SoC:
**arm64**, 16 GB unified LPDDR5X RAM. Unlike Steam Deck/Steam Machine (x86_64), this is
the first mainline SteamOS device on ARM. Valve bridges x86 Windows games via
Proton + FEX binary translation, but a native arm64 Linux build avoids that translation
layer entirely - relevant for a VR title where frame time matters.

## The one thing that matters most: this is a first-party Steamworks-documented target

Valve publishes an official guide: <https://partner.steamgames.com/doc/steamframe/engines/godot>
(Steamworks partner docs, so it may require a partner login to view; findings below were
retrieved via fetch on 2026-09-26 and should be re-verified against the live page before
acting on them).

Key facts from that page:
- **Linux arm64 has been a supported Godot export platform since Godot 4.2.3.** This is
  a normal export preset/architecture setting, not something that requires compiling
  custom export templates. This directly contradicts an earlier (wrong) finding in this
  research pass that Godot's `.NET`/Mono export templates lack arm64 Linux support - they
  don't lack it; the arm64 binaries are bundled inside the same `*_mono_export_templates.tpz`
  archive as every other platform/arch, just not called out as a separate download on the
  godotengine.org download page.
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
  same path as any other Steam release, going through Steamworks/SteamPipe. **Flathub is
  not mentioned anywhere in Valve's own docs as a distribution path for Steam Frame apps.**

## Reframing: Flathub vs. the native Steam path

The original framing of this task was "build an arm64 Flatpak and send it to Flathub."
Two things changed that:

1. The user found a **Flathub-distributed Godot Editor (C#/.NET build)** already
   installable via the Dolphin Software Center (Discover) on Steam Frame day one. This
   confirms Flathub packages *do* reach the Frame's software center and that Mono/.NET
   runs fine on this arm64 SteamOS - but it's evidence about the *editor* being on
   Flathub, not evidence that shipping *games* through Flathub is how Frame owners are
   expected to get them.
2. Valve's own Steamworks documentation describes a completely different, more direct
   path: native Linux arm64 export + Steam DevKit upload, i.e. distribute through Steam
   itself (Steamworks/SteamPipe), the same as Steam Deck.

So there are two independent, non-exclusive distribution questions:
- **Getting a native arm64 Linux build to exist at all** (needed either way) - this is
  the bulk of the real engineering work and is now confirmed *not* to require custom
  Godot export templates. It's mostly CI plumbing (see below).
- **Where it's distributed**: Steamworks/SteamPipe is Valve's documented path, but it's
  built for commercial titles with a storefront listing (Steam Direct's $100-per-title
  fee, a store page, revenue share) - **there is no Valve-provided category for
  distributing a free/open-source app or game through Steam itself without going through
  that commercial publishing pipeline.** Flathub, reached via the Dolphin Software
  Center/Discover, is the actual free/open distribution channel available on the device
  - not because Valve intended it for games, but because it's the only app-store-shaped
  path that doesn't require becoming a Steamworks partner. That matches the Godot editor
  the user already found there.

**Decision: pursue Flathub for Steam Frame distribution**, not Steamworks/SteamPipe -
not because it's Valve's documented route, but because Valve doesn't offer a
free/open-source-friendly alternative on this device. This reopens the Flatpak/Flathub
effort that `[[project_appimage_distribution]]` previously shelved (that decision was
made before Steam Frame existed, when the only Discover-relevant devices were Deck/Machine
and AppImage-only was judged sufficient). The existing GitHub Releases + itch.io pipeline
stays as-is for sideloading/other platforms; Flathub is additive, specifically to reach
Frame's built-in software center.

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

## Current implementation state (2026-09-26)

**Done and locally verified:**
- `godot/BenMcLean.Wolf3D.VR/export_presets.cfg` has a new `[preset.3]`, `"Linux (arm64)"`,
  identical to the existing `"Linux"` preset except `binary_format/architecture="arm64"`.
- `.github/workflows/build.yml`'s `export` job matrix gained a `linux-arm64` entry using
  that preset. Its AppImage step reuses the existing `linux-x64` logic (now shared via
  `startsWith(matrix.name, 'linux-')` instead of an exact match), parameterized by a new
  `appimage_arch` matrix field (`x86_64` / `aarch64`) and `matrix.output_file` (the
  AppRun script and appimagetool download URL now come from matrix values instead of
  being hardcoded to `x86_64`).
- The AppImage job's inline desktop-file heredoc was replaced with a checked-in
  `packaging/linux/net.benmclean.wolf3d.desktop`, shared with the new Flatpak manifest
  (single source of truth instead of two copies drifting apart).
- `itch-release` gained a "Push Linux arm64 build" step (channel `linux-arm64`).
- The arm64 export itself (Godot editor + export template + `dotnet publish` +
  `godot --export-release`) was verified for real in WSL2 Ubuntu, see "Local verification"
  below - this predates and motivated the CI changes.

**Flatpak manifest: built and verified for real, offline, via Docker (2026-09-26).**
- A `flatpak` CI job builds a `.flatpak` bundle per arch (`x86_64`, `aarch64`) from
  `flatpak/net.benmclean.wolf3d.yml`, using the official
  `flatpak/flatpak-github-actions/flatpak-builder@v6` action inside the
  `ghcr.io/flathub-infra/flatpak-github-actions:gnome-48` container (the same image
  Flathub's own build infrastructure is based on), plus `docker/setup-qemu-action` for
  the aarch64 leg.
- The manifest deliberately does **not** build Godot/.NET from source inside the Flatpak
  sandbox - it bundles the already-exported Linux binary (the same output the AppImage
  job produces) as a `type: dir` source, installed via a `buildsystem: simple` module.
  This is a legitimate, commonly-accepted Flathub pattern for tools that don't fit the
  from-source model cleanly, but it's still a choice a Flathub reviewer could push back
  on - be ready to defend or change it during actual submission review.
- **Local, offline test method** (this is the answer to "how do we test this without
  committing/pushing anything"): pull `ghcr.io/flathub-infra/flatpak-github-actions:gnome-48`
  with plain `docker pull` (Docker Desktop, confirmed installed and working on this
  machine) and run `flatpak-builder` inside it by hand against a `flatpak/build-input/`
  populated from a local arm64 export (see "Local verification" below) - no GitHub Actions
  run needed to validate the manifest itself.
- **This surfaced and fixed a real bug**: the original manifest's
  `install -Dm755 build-input/BenMcLean.Wolf3D.VR.* /app/bin/net.benmclean.wolf3d` both
  (a) glob-matched *both* the executable and its `.pck` (ambiguous `install` invocation,
  hard failure) and (b) even fixed, would have dropped the `.pck` and the `data_*`
  support directory Godot's Linux export needs at runtime (for GDExtension plugins like
  the OpenXR Vendors `.so`). Fixed by copying the whole export directory verbatim and
  symlinking a stable `net.benmclean.wolf3d` entry point to the real arch-suffixed
  binary - a plain rename would have broken Godot's pck lookup, which resolves the
  *real* binary path via `/proc/self/exe`, not the invoked symlink name, so the
  installed binary and its `.pck` must keep matching basenames. This is exactly the kind
  of bug that offline testing before pushing is for.
- **Full local run succeeded end-to-end**: `flatpak-builder` build, appstream-compose
  validation ("Success!"), export to an ostree repo (confirmed via
  `ostree refs --repo=flatpak/repo` showing `app/net.benmclean.wolf3d/aarch64/master`),
  and `flatpak build-bundle` produced a real 40MB `.flatpak` file. Test artifacts
  (`flatpak/build-input/`, `flatpak/repo/`, `flatpak/build/`, the test bundle, the
  `.flatpak-builder/` cache, and the Docker test container) were all cleaned up
  afterward - nothing from this test is left in the repo or on disk.
- **Local-testing-only caveat, do NOT port into the real CI job**: running
  `flatpak-builder` inside Docker Desktop on Windows (Windows -> WSL2 VM -> Docker
  daemon -> container, i.e. real nested virtualization) required three extra steps the
  real GitHub Actions runner almost certainly won't need, because GH-hosted runners are
  one layer shallower (VM -> container): (1) `dbus-uuidgen > /etc/machine-id` (no
  machine-id existed in the fresh container), (2) `dbus-run-session --` wrapping the
  build (no session bus otherwise), (3) `flatpak-builder --disable-rofiles-fuse` (the
  FUSE-based build-cache overlay failed with a D-Bus portal error under this specific
  nesting depth). If the real CI run somehow hits the same `rofiles-fuse`/D-Bus errors,
  these three fixes are the known solution - but expect it not to be necessary there.
- New files: `packaging/linux/net.benmclean.wolf3d.desktop`,
  `packaging/linux/net.benmclean.wolf3d.metainfo.xml` (AppStream metadata - contains
  several **placeholder values flagged inline** that need real ones before submission:
  OARS content rating, release version/date, and the Nazi-imagery content rating
  question flagged per `[[feedback_no_content_censorship]]`).
- `.gitignore` gained entries for `flatpak/build-input/`, `.flatpak-builder/`,
  `flatpak/repo/`, `flatpak/build/` (all local/CI scratch, not checked in).

**Still not verified against a real GitHub Actions run** (only verified via the local
Docker method above): whether the official action's defaults (no `--disable-rofiles-fuse`
etc.) work as-is on a real GH-hosted runner - expected to be fine per the reasoning
above, but "expected" isn't "observed." Watch the first real run regardless.

**Still not started:** actually opening a submission PR against `flathub/flathub` (not
authorized - a separate, explicit step); GPU/Vulkan passthrough verification inside the
Flatpak sandbox specifically (separate from the AppImage's own unverified assumption
about this, see below) - Flatpak sandboxing is stricter than an AppImage's, so this
deserves its own check on real hardware, not an assumption that whatever works for the
AppImage also works here.

**Game data folder (done, untested on hardware):** inside a Flatpak (detected via
`$FLATPAK_ID` or `/.flatpak-info`), `RuntimeOptions.Path` defaults to `~/WOLF3D`, and the
manifest grants `--filesystem=~/WOLF3D:create`. Override order: `--path`, bare positional
argument, `WOLF3D_GAMES_DIR` environment variable, then the platform default. A custom
location also needs a sandbox grant (`flatpak override --user --filesystem=...`).

## Local verification (2026-09-26) - PASSED

Ran the same steps as the existing CI Linux job, by hand, in WSL2 Ubuntu 24.04 (x86_64
host), to sanity-check the arm64 export claim before touching CI:

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

**Conclusion: the arm64 Linux export "just works" today, on the currently-pinned Godot
4.5.1, with zero engine-version bump and zero custom template build required.** The
Godot version bump (see above) is still worth doing for Steam Frame's OpenXR controller
profile, but it is not a prerequisite for producing a running arm64 binary.

## Open questions / not yet done

- Confirm Vulkan/Adreno GPU passthrough works for both the AppImage (no-bundled-libraries
  approach) and the Flatpak (stricter sandbox, `--device=dri` + `--socket=wayland`/
  `--socket=fallback-x11` in the manifest's `finish-args` - an assumption, not tested)
  once real Frame hardware is available. Unverified even for the existing x86_64 Linux
  AppImage path (see `.github/workflows/build.yml` comments and
  `[[project_appimage_distribution]]`).
- No decision on Steam Frame Controller Interaction Profile / OpenXR Vendors plugin
  integration - out of scope until the Godot version bump happens.
- The `flatpak` CI job has never actually run - see "Current implementation state" above
  for exactly what's unverified there. Watch its first real run.
- Fill in the placeholder values in `packaging/linux/net.benmclean.wolf3d.metainfo.xml`
  (OARS content rating including Nazi-imagery attributes, real release version/date)
  before any Flathub submission.
- Opening a submission PR against `flathub/flathub` is a separate, explicit,
  not-yet-authorized step - this doc and the CI job only get a bundle to the point of
  "downloadable and installable," not "submitted."
- Content policy for any future storefront submission (Steam or Flathub) still applies:
  `[[feedback_no_content_censorship]]` - label historical Nazi imagery, never censor it.

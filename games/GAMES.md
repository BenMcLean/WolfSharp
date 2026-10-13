The [Wolfenstein 3-D Shareware v1.4](https://archive.org/download/Wolfenstein3d/Wolfenstein3dV14sw.ZIP) game data created by id Software and published by Apogee is included under its original shareware redistribution permission from 1992. No other game data is included in official builds. Users supply their own game data to play anything else.

# Installed copies

You do not need to copy game data into the games folder if you have installed a game on Steam, GOG or itch.io. On Windows and Linux (including SteamOS) if the games folder does not have a game's data, the program looks for a copy installed by one of those stores and reads it from there.

The games folder always wins: if it has the game's data, installed copies are not even looked at. Otherwise the stores are tried in this order: Steam, GOG, itch.io.

Installed copies are only read, never modified. High scores, settings and saved games are always stored in the games folder, even for a game that is read from a store's install folder.

|Title|Steam|GOG|itch.io|
|---|---|---|---|
|Wolfenstein 3-D|yes|yes||
|Spear of Destiny|yes|yes||
|Spear of Destiny Mission 2: Return to Danger|yes|yes||
|Spear of Destiny Mission 3: Ultimate Challenge|yes|yes||
|Super 3D Noah's Ark|yes|yes|yes (the original DOS release)|

- **Steam:** all Steam library folders are searched, including those on other drives or a Steam Deck SD card. The Flatpak and Snap versions of Steam are found too.
- **GOG on Windows:** the install is located through GOG Galaxy's registry entry, wherever it was installed.
- **GOG on Linux:** GOG has no official Linux client, so only games installed with the Heroic Games Launcher are found. Otherwise copy the files into the games folder.
- **itch.io:** installs made by the itch app are found. The itch page for Super 3D Noah's Ark offers several downloads; only the original DOS release contains the game data used here, so install that one.
- **Spear of Destiny on Steam** keeps all three missions in one folder. Each one is found separately, so Missions 1, 2 and 3 are all available.

If a game isn't found automatically, or you want to use a different copy, put its files in the games folder as described above.

# Loose files

On PC, a subfolder should be created for each game under a "games" folder placed next to the executable.

If you installed from a `.deb`, `.rpm` or AUR package, use `~/.local/share/wolfsharp/games` instead.

On Android, the subfolders go under `/sdcard/WOLF3D`.

On any platform, the location can be overridden with `--path <path>` or the `WOLF3D_GAMES_DIR` environment variable.

|Title|Subfolder|File extension|
|---|---|---|
|Wolfenstein 3-D Shareware|`WL1`|`*.WL1`|
|Wolfenstein 3-D|`WL6`|`*.WL6`|
|Spear of Destiny|`M1`|`*.SOD`|
|Spear of Destiny Mission 2: Return to Danger|`M2`|`*.SOD` (GOG) or `*.SD2` (original retail)|
|Spear of Destiny Mission 3: Ultimate Challenge|`M3`|`*.SOD` (GOG) or `*.SD3` (original retail)|
|Super 3D Noah's Ark|`N3D`|`*.N3D`|

Note: Adding the WL1 data will override the bundled version.

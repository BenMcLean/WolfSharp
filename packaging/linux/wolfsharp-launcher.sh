#!/bin/sh
# Launcher for the .deb/.rpm/AUR packages. The install dir (/opt/wolfsharp) is root-owned, so
# default the games folder to a per-user location instead. WOLF3D_GAMES_DIR and --path still win.
if [ -z "${WOLF3D_GAMES_DIR:-}" ]; then
	WOLF3D_GAMES_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/wolfsharp/games"
	mkdir -p "$WOLF3D_GAMES_DIR" 2>/dev/null
	export WOLF3D_GAMES_DIR
fi
case "$(uname -m)" in
	aarch64|arm64) SUFFIX=arm64 ;;
	*) SUFFIX=x86_64 ;;
esac
exec "/opt/wolfsharp/BenMcLean.Wolf3D.VR.$SUFFIX" "$@"

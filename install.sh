#!/bin/sh
# Installs (or updates) WolfSharp VR from the latest GitHub Release. No root needed.
#   curl -fsSL https://raw.githubusercontent.com/BenMcLean/WolfSharp/master/install.sh | sh
# Installs to ~/.local/share/wolfsharp (put game data in its "games" subfolder), adds a
# "wolfsharp" command to ~/.local/bin and an application menu entry.
set -eu

REPO="BenMcLean/WolfSharp"
BASE_URL="${WOLFSHARP_BASE_URL:-https://github.com/$REPO/releases/latest/download}"
DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
INSTALL_DIR="$DATA_HOME/wolfsharp"
BIN_DIR="$HOME/.local/bin"
APP_ID="net.benmclean.wolf3d"

case "$(uname -m)" in
	x86_64|amd64) ARCH=x86_64 ;;
	aarch64|arm64) ARCH=aarch64 ;;
	*) echo "Unsupported CPU architecture: $(uname -m)" >&2; exit 1 ;;
esac
[ "$(uname -s)" = Linux ] || { echo "This installer is for Linux only." >&2; exit 1; }

APPIMAGE="$INSTALL_DIR/BenMcLean.Wolf3D.VR-$ARCH.AppImage"
mkdir -p "$INSTALL_DIR/games" "$BIN_DIR" "$DATA_HOME/applications" "$DATA_HOME/icons/hicolor/256x256/apps"

echo "Downloading WolfSharp VR ($ARCH)..."
TMP="$APPIMAGE.part"
if command -v curl >/dev/null 2>&1; then
	curl -fL --progress-bar "$BASE_URL/BenMcLean.Wolf3D.VR-$ARCH.AppImage" -o "$TMP"
elif command -v wget >/dev/null 2>&1; then
	wget -q --show-progress "$BASE_URL/BenMcLean.Wolf3D.VR-$ARCH.AppImage" -O "$TMP"
else
	echo "Need curl or wget." >&2; exit 1
fi
chmod +x "$TMP"
mv -f "$TMP" "$APPIMAGE"

# Pull the icon out of the AppImage itself (works without FUSE) for the menu entry.
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
( cd "$WORK" && "$APPIMAGE" --appimage-extract "$APP_ID.png" >/dev/null 2>&1 ) &&
	cp "$WORK/squashfs-root/$APP_ID.png" "$DATA_HOME/icons/hicolor/256x256/apps/$APP_ID.png" || true

cat > "$DATA_HOME/applications/$APP_ID.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Wolfenstein 3-D
Comment=Wolfenstein 3-D engine recreation for VR (WolfSharp)
Exec="$APPIMAGE"
Icon=$APP_ID
Categories=Game;ActionGame;
Terminal=false
DESKTOP

ln -sf "$APPIMAGE" "$BIN_DIR/wolfsharp"

echo
echo "Installed to $INSTALL_DIR"
echo "Put your game data in $INSTALL_DIR/games (see games/GAMES.md in the repo)."
case ":$PATH:" in *":$BIN_DIR:"*) ;; *) echo "Note: $BIN_DIR is not on your PATH." ;; esac
echo "Run it from your application menu or with: wolfsharp"
echo "Run this installer again at any time to update."

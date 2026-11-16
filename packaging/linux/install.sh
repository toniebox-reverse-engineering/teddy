#!/bin/sh
# Installs TeddyBench for the current user (no root needed):
#   ~/.local/opt/teddybench          program files
#   ~/.local/bin/teddybench, teddy   commands
#   desktop entry and icon           application menu
# Run ./install.sh --uninstall to remove it again.
set -eu

ID=io.github.toniebox_reverse_engineering.TeddyBench
HERE="$(cd "$(dirname "$0")" && pwd)"
DATA="${XDG_DATA_HOME:-$HOME/.local/share}"
PREFIX="$HOME/.local/opt/teddybench"
BIN="$HOME/.local/bin"

if [ "${1:-}" = "--uninstall" ]; then
    rm -rf "$PREFIX"
    rm -f "$BIN/teddybench" "$BIN/teddy" "$DATA/applications/$ID.desktop" "$DATA/icons/hicolor/256x256/apps/$ID.png"
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q "$DATA/applications" || true
    echo "TeddyBench removed."
    exit 0
fi

rm -rf "$PREFIX"
mkdir -p "$PREFIX" "$BIN" "$DATA/applications" "$DATA/icons/hicolor/256x256/apps"
cp -R "$HERE/lib/." "$PREFIX/"
ln -sf "$PREFIX/TeddyBench" "$BIN/teddybench"
ln -sf "$PREFIX/Teddy" "$BIN/teddy"

sed "s|^Exec=.*|Exec=$PREFIX/TeddyBench|" "$HERE/share/$ID.desktop" > "$DATA/applications/$ID.desktop"
cp "$HERE/share/$ID.png" "$DATA/icons/hicolor/256x256/apps/$ID.png"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q "$DATA/applications" || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q "$DATA/icons/hicolor" 2>/dev/null || true

echo "TeddyBench installed to $PREFIX"
echo "Start it from the application menu or with: teddybench   (command line tool: teddy)"
case ":$PATH:" in *":$BIN:"*) ;; *) echo "Note: $BIN is not in your PATH." ;; esac
echo
echo "NFC readers: add yourself to the dialout group (sudo usermod -aG dialout \$USER) and,"
echo "for a Proxmark3, install the udev rule: sudo cp $HERE/77-teddybench-readers.rules /etc/udev/rules.d/"

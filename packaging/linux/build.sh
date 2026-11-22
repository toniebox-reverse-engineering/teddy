#!/usr/bin/env bash
# Builds the Linux release artifacts (x86_64):
#   artifacts/TeddyBench-x86_64.AppImage   GUI; "TeddyBench-x86_64.AppImage cli ..." runs the command line tool
#   artifacts/TeddyBench-linux-x64.tar.gz  both programs + install.sh (per-user install, no root)
#   artifacts/TeddyBench-x86_64.flatpak     single-file Flatpak bundle (runtime org.freedesktop.Platform)
#
# Needs: .NET 10 SDK, curl, sha256sum, tar. appimagetool and the AppImage runtime are downloaded
# (pinned versions, checked by SHA-256) into $TEDDY_TOOLS_CACHE (default ~/.cache/teddybench-build).
# The Flatpak needs `flatpak` and the runtime $FLATPAK_RUNTIME//$FLATPAK_BRANCH installed (no flatpak-builder).
# Set SKIP_APPIMAGE=1 / SKIP_FLATPAK=1 to leave those out.
# TEDDY_UPDATE_REPOSITORY=owner/name makes the GUI check that repository's releases (default: upstream).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
HERE="$ROOT/packaging/linux"
OUT="${OUT:-$ROOT/artifacts}"
WORK="$OUT/work"
RID=linux-x64
ID=io.github.toniebox_reverse_engineering.TeddyBench
TOOLS="${TEDDY_TOOLS_CACHE:-${XDG_CACHE_HOME:-$HOME/.cache}/teddybench-build}"

APPIMAGETOOL_URL=https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage
APPIMAGETOOL_SHA256=ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0
RUNTIME_URL=https://github.com/AppImage/type2-runtime/releases/download/20251108/runtime-x86_64
RUNTIME_SHA256=2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d
FLATPAK_RUNTIME=org.freedesktop.Platform
# 26.08 (fontconfig 2.17): the GUI spins forever inside fontconfig at startup; 25.08 works
FLATPAK_BRANCH="${FLATPAK_BRANCH:-25.08}"

fetch() { # url sha256 file
    if [ -f "$3" ] && echo "$2  $3" | sha256sum -c --status; then
        return
    fi
    mkdir -p "$(dirname "$3")"
    curl -sSfL --retry 4 -o "$3.part" "$1"
    if ! echo "$2  $3.part" | sha256sum -c --status; then
        echo "Checksum mismatch for $1" >&2
        rm -f "$3.part"
        exit 1
    fi
    mv "$3.part" "$3"
}

rm -rf "$WORK"
mkdir -p "$WORK" "$OUT"

# 1. Both programs, self-contained, into one folder (they share the .NET runtime files)
LIB="$WORK/lib"
for project in Teddy/Teddy.csproj TeddyBench.Avalonia/TeddyBench.Avalonia.csproj; do
    dotnet publish "$ROOT/$project" -c Release -r "$RID" --self-contained -p:DebugType=none \
        -p:TeddyUpdateRepository="${TEDDY_UPDATE_REPOSITORY:-}" -o "$LIB" --nologo -v quiet
done
test -x "$LIB/Teddy" && test -x "$LIB/TeddyBench"

# 2. Tarball
TAR="$WORK/teddybench-linux-x64"
mkdir -p "$TAR/share"
cp -R "$LIB" "$TAR/lib"
cp "$HERE/$ID.desktop" "$HERE/$ID.png" "$HERE/$ID.metainfo.xml" "$TAR/share/"
cp "$HERE/install.sh" "$HERE/77-teddybench-readers.rules" "$TAR/"
cp "$ROOT/README.md" "$ROOT/LICENSE" "$ROOT/THIRD_PARTY_NOTICES.md" "$TAR/"
tar -C "$WORK" -czf "$OUT/TeddyBench-linux-x64.tar.gz" "$(basename "$TAR")"
echo "Built $OUT/TeddyBench-linux-x64.tar.gz"

# 3. AppImage
build_appimage() {
APPDIR="$WORK/TeddyBench.AppDir"
mkdir -p "$APPDIR/usr/lib" "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" \
         "$APPDIR/usr/share/icons/hicolor/256x256/apps" "$APPDIR/usr/share/metainfo"
cp -R "$LIB" "$APPDIR/usr/lib/teddybench"
mkdir -p "$APPDIR/usr/share/doc/teddybench"
cp "$ROOT/LICENSE" "$ROOT/THIRD_PARTY_NOTICES.md" "$APPDIR/usr/share/doc/teddybench/"
ln -s ../lib/teddybench/TeddyBench "$APPDIR/usr/bin/TeddyBench"
ln -s ../lib/teddybench/Teddy "$APPDIR/usr/bin/Teddy"
install -m 755 "$HERE/AppRun" "$APPDIR/AppRun"
cp "$HERE/$ID.desktop" "$APPDIR/usr/share/applications/"
cp "$HERE/$ID.desktop" "$APPDIR/"
cp "$HERE/$ID.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/"
cp "$HERE/$ID.png" "$APPDIR/"
ln -s "$ID.png" "$APPDIR/.DirIcon"
cp "$HERE/$ID.metainfo.xml" "$APPDIR/usr/share/metainfo/$ID.appdata.xml"

fetch "$APPIMAGETOOL_URL" "$APPIMAGETOOL_SHA256" "$TOOLS/appimagetool-1.9.1-x86_64.AppImage"
fetch "$RUNTIME_URL" "$RUNTIME_SHA256" "$TOOLS/runtime-20251108-x86_64"
chmod +x "$TOOLS/appimagetool-1.9.1-x86_64.AppImage"

# extract-and-run: works without FUSE (CI containers)
APPIMAGE_EXTRACT_AND_RUN=1 ARCH=x86_64 "$TOOLS/appimagetool-1.9.1-x86_64.AppImage" --no-appstream \
    --runtime-file "$TOOLS/runtime-20251108-x86_64" "$APPDIR" "$OUT/TeddyBench-x86_64.AppImage"
echo "Built $OUT/TeddyBench-x86_64.AppImage"
}

# 4. Flatpak: the same self-contained build, repackaged on the freedesktop runtime (which provides
#    X11, fontconfig, ICU, pw-play/paplay, xdg-open and dbus-send). Sandbox permissions, see below.
build_flatpak() {
    local FP="$WORK/flatpak" BUILD="$WORK/flatpak/build"
    rm -rf "$FP"
    # the runtime doubles as "SDK": nothing is compiled inside the sandbox
    flatpak build-init "$BUILD" "$ID" "$FLATPAK_RUNTIME" "$FLATPAK_RUNTIME" "$FLATPAK_BRANCH"
    local F="$BUILD/files"
    mkdir -p "$F/lib" "$F/bin" "$F/share/applications" "$F/share/icons/hicolor/256x256/apps" "$F/share/metainfo"
    cp -R "$LIB" "$F/lib/teddybench"
    mkdir -p "$F/share/licenses/$ID"
    cp "$ROOT/LICENSE" "$ROOT/THIRD_PARTY_NOTICES.md" "$F/share/licenses/$ID/"
    ln -s ../lib/teddybench/TeddyBench "$F/bin/TeddyBench"
    ln -s ../lib/teddybench/Teddy "$F/bin/Teddy"
    cp "$HERE/$ID.desktop" "$F/share/applications/"
    cp "$HERE/$ID.png" "$F/share/icons/hicolor/256x256/apps/"
    cp "$HERE/$ID.metainfo.xml" "$F/share/metainfo/"

    # x11: Avalonia 11 has no native Wayland backend (XWayland is used)
    # device=all: Proxmark3/PN5180 readers on /dev/ttyACM*/ttyUSB* (plus GPU)
    # filesystem /run/media, /media: SD card detection and the FAT "hidden" flag; home: MP3 import/export
    # FileManager1: "show in file manager"; network: tonies.json, cover images, update check
    flatpak build-finish "$BUILD" --command=TeddyBench \
        --share=ipc --socket=x11 --device=all --socket=pulseaudio --share=network \
        --filesystem=home --filesystem=/run/media --filesystem=/media \
        --talk-name=org.freedesktop.FileManager1
    flatpak build-export "$FP/repo" "$BUILD" stable
    flatpak build-bundle --runtime-repo=https://dl.flathub.org/repo/flathub.flatpakrepo \
        "$FP/repo" "$OUT/TeddyBench-x86_64.flatpak" "$ID" stable
    echo "Built $OUT/TeddyBench-x86_64.flatpak"
}

[ "${SKIP_APPIMAGE:-0}" = "1" ] || build_appimage
[ "${SKIP_FLATPAK:-0}" = "1" ] || build_flatpak

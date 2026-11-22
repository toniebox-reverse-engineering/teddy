# Tonie file tool

With this tool you can dump existing files of the famous audio box or create custom ones.

## Linux

The command line tool `Teddy` (encode / decode / info) and the cross-platform GUI `TeddyBench`
(Avalonia, project `TeddyBench.Avalonia`) run natively on Linux. The original WinForms GUI
(`TeddyBench`, Windows only) is still built on Windows until the Avalonia version replaces it.

Linux integration of the GUI:

- **SD card**: an inserted card is opened automatically once the desktop has mounted it (any FAT/exFAT
  mount with a `CONTENT` folder, usually below `/run/media/$USER`). *File / Open Directory* works too.
- **Live flag**: set via the FAT hidden attribute (vfat ioctl), so it only works on the mounted card itself.
- **Audio preview**: played through `pw-play` (PipeWire), `paplay` (PulseAudio) or `aplay` (ALSA),
  whichever is installed.
- **Show in file manager**: uses the freedesktop `FileManager1` D-Bus interface (Nautilus, Dolphin, Nemo,
  Thunar, ...), otherwise opens the folder with `xdg-open`.
- **NFC readers (Proxmark3, PN5180/ESP32)**: only USB serial ports (`/dev/ttyACM*`, `/dev/ttyUSB*`) are probed.
  Your user needs access to them: `sudo usermod -aG dialout $USER`, then log out and in again (the status bar
  tells you when access is missing). To stop ModemManager from grabbing the Proxmark3, install
  [`packaging/linux/77-teddybench-readers.rules`](packaging/linux/77-teddybench-readers.rules)
  (instructions inside).

### Project layout

| Project | Target | Purpose |
|---|---|---|
| `TonieAudio`, `Concentus*`, `ID3` | netstandard2.0 | Tonie file format, Opus encoding/decoding |
| `Teddy` | net10.0 | Command line tool |
| `TeddyBench.Core` | netstandard2.0 | Everything of TeddyBench that is not UI: NFC reader drivers (Proxmark3, PN5180), tonies.json database, SD card operations, card detection, update check, diagnostics, playback, logging. Platform specific parts (card detection, shell, "live" flag, audio output, app data paths) are behind interfaces in `PlatformServices`/`AppPaths`. |
| `TeddyBench.Avalonia` | net10.0 | Cross-platform GUI (Avalonia 11, MVVM), uses `TeddyBench.Core`; builds as `TeddyBench` |
| `TeddyBench` | net48 | Original WinForms GUI (Windows only), uses `TeddyBench.Core` |
| `*.Tests` | net10.0 | xUnit tests, run on Linux |

On Linux, TeddyBench keeps its settings, `tonies.json` and `customTonies.json` in `$XDG_CONFIG_HOME/TeddyBench`
(default `~/.config/TeddyBench`) and downloaded images in `~/.cache/TeddyBench`. On Windows they stay in the
current directory as before.

### Install

Download from the [releases](../../releases) of this repository:

- **`TeddyBench-x86_64.AppImage`**: make it executable (`chmod +x`) and start it. Runs the GUI;
  `./TeddyBench-x86_64.AppImage cli --help` runs the command line tool.
- **`TeddyBench-linux-x64.tar.gz`**: unpack and run `./install.sh` (installs for your user into `~/.local`,
  with menu entry and the commands `teddybench` / `teddy`; `./install.sh --uninstall` removes it).

- **`TeddyBench-x86_64.flatpak`**: `flatpak install --user TeddyBench-x86_64.flatpak` (fetches the
  `org.freedesktop.Platform` 25.08 runtime from Flathub if needed), then start it from the menu or with
  `flatpak run io.github.toniebox_reverse_engineering.TeddyBench`. The command line tool:
  `flatpak run --command=Teddy io.github.toniebox_reverse_engineering.TeddyBench --help`.
  The sandbox may access your home folder, removable media (`/run/media`, `/media`), the network, audio
  and devices (NFC readers). Settings live in `~/.var/app/io.github.toniebox_reverse_engineering.TeddyBench/config/TeddyBench`,
  not `~/.config/TeddyBench`. For Proxmark3/PN5180 access the udev rules from the tarball
  (`77-teddybench-readers.rules`) still have to be installed on the host.

All of them are self-contained (no .NET installation needed) and run on x86_64 distributions with glibc.

### Build

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) (Fedora: `sudo dnf install dotnet-sdk-10.0`).

```sh
dotnet build Teddy.Linux.slnf -c Release      # everything except TeddyBench
dotnet test  Teddy.Linux.slnf -c Release      # unit, round-trip and headless UI tests
# TEDDYBENCH_SCREENSHOTS=/some/dir dotnet test ... also saves PNGs of the rendered windows

# self-contained single binaries, no .NET needed on the target machine
for p in Teddy/Teddy.csproj TeddyBench.Avalonia/TeddyBench.Avalonia.csproj; do
  dotnet publish $p -c Release -r linux-x64 --self-contained \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true -o publish/linux-x64
done

publish/linux-x64/TeddyBench                   # GUI

# release packages (AppImage, Flatpak, tarball) into artifacts/, as built by CI for v* tags
# (Flatpak: needs flatpak + org.freedesktop.Platform//25.08; SKIP_FLATPAK=1 / SKIP_APPIMAGE=1 skip them)
packaging/linux/build.sh

publish/linux-x64/Teddy -m encode -o 500304E0 ~/Music/MyAlbum/
publish/linux-x64/Teddy -m info 500304E0
publish/linux-x64/Teddy -m decode -o dump/ 500304E0
```

MP3 decoding uses the managed [NLayer](https://github.com/naudio/NLayer) decoder and NAudio's WDL resampler
instead of the Windows ACM / Media Foundation codecs, so generated files are not byte-identical to
ones produced by older Windows builds (the audio content is equivalent).

## License

BSD 3-Clause License, see [`LICENSE`](LICENSE). Bundled components keep their own licenses
(Concentus: BSD-3-Clause, Concentus.Oggfile: Ms-PL, ID3: Apache-2.0, NuGet packages: mostly MIT), see
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

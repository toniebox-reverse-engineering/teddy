# Tonie file tool

With this tool you can dump existing files of the famous audio box or create custom ones.

## Linux

The command line tool `Teddy` (encode / decode / info) and the cross-platform GUI `TeddyBench`
(Avalonia, project `TeddyBench.Avalonia`) run natively on Linux. The original WinForms GUI
(`TeddyBench`, Windows only) is still built on Windows until the Avalonia version replaces it.

Not yet available on Linux (follows with the Linux platform integration): automatic detection of the
inserted SD card (use *File / Open Directory* on the card's `CONTENT` folder), audio preview, and the
"live" flag. Proxmark3/PN5180 readers need access to the serial port (`sudo usermod -aG dialout $USER`).

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

publish/linux-x64/Teddy -m encode -o 500304E0 ~/Music/MyAlbum/
publish/linux-x64/Teddy -m info 500304E0
publish/linux-x64/Teddy -m decode -o dump/ 500304E0
```

MP3 decoding uses the managed [NLayer](https://github.com/naudio/NLayer) decoder and NAudio's WDL resampler
instead of the Windows ACM / Media Foundation codecs, so generated files are not byte-identical to
ones produced by older Windows builds (the audio content is equivalent).

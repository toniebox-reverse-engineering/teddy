# Tonie file tool

With this tool you can dump existing files of the famous audio box or create custom ones.

## Linux

The command line tool `Teddy` (encode / decode / info) and its audio library run natively on Linux.
The GUI `TeddyBench` is still Windows-only (WinForms) and is not part of the Linux build yet.

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) (Fedora: `sudo dnf install dotnet-sdk-10.0`).

```sh
dotnet build Teddy.Linux.slnf -c Release      # everything except TeddyBench
dotnet test  Teddy.Linux.slnf -c Release      # unit + encode/decode round-trip tests

# self-contained single binary, no .NET needed on the target machine
dotnet publish Teddy/Teddy.csproj -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o publish/linux-x64

publish/linux-x64/Teddy -m encode -o 500304E0 ~/Music/MyAlbum/
publish/linux-x64/Teddy -m info 500304E0
publish/linux-x64/Teddy -m decode -o dump/ 500304E0
```

MP3 decoding uses the managed [NLayer](https://github.com/naudio/NLayer) decoder and NAudio's WDL resampler
instead of the Windows ACM / Media Foundation codecs, so generated files are not byte-identical to
ones produced by older Windows builds (the audio content is equivalent).

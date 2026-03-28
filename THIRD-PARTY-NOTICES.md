# Third-Party Notices

Teddy includes or depends upon third-party software components that are licensed under various open-source licenses. This file contains the required notices and license information for those components.

---

## LibVLC and LibVLCSharp

**Component:** LibVLC (VLC media player library) and LibVLCSharp (.NET bindings)
**Copyright:** Copyright © 1996-2025 VideoLAN and the VLC Authors
**License:** GNU Lesser General Public License v2.1 or later (LGPL-2.1-or-later)
**Website:** https://www.videolan.org/vlc/libvlc.html
**Source Code:** https://code.videolan.org/videolan/vlc

### Usage in Teddy

TeddyBench.Avalonia uses LibVLCSharp to provide cross-platform audio playback functionality. LibVLC is dynamically linked via NuGet packages:
- LibVLCSharp (cross-platform bindings)
- VideoLAN.LibVLC.Windows (native Windows libraries)
- VideoLAN.LibVLC.Mac (native macOS libraries)
- System VLC installation required on Linux

### LGPL 2.1 Compliance

Teddy complies with the LGPL 2.1 license requirements for LibVLC:

1. **Dynamic Linking:** LibVLC is dynamically linked through the LibVLCSharp wrapper and platform-specific NuGet packages
2. **Source Availability:** LibVLC source code is available at https://code.videolan.org/videolan/vlc
3. **Library Replacement:** Users can replace LibVLC with their own version:
   - **Windows/macOS:** Replace the native libraries in the application directory
   - **Linux:** Install a different version of VLC system-wide
4. **License Notice:** This file serves as the required notice that Teddy uses LGPL-licensed components

### Full LGPL 2.1 License Text

The complete LGPL 2.1 license can be found at:
- https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html
- https://code.videolan.org/videolan/vlc/-/blob/master/COPYING.LIB

---

## NAudio

**Component:** NAudio
**Copyright:** Copyright © Mark Heath
**License:** MIT License
**Website:** https://github.com/naudio/NAudio
**NuGet:** https://www.nuget.org/packages/NAudio/

NAudio is used for audio processing on Windows platforms.

---

## Concentus and Concentus.Oggfile

**Component:** Concentus (Opus codec) and Concentus.Oggfile
**Copyright:** Copyright © Logan Stromberg
**License:** MIT License
**Website:** https://github.com/lostromb/concentus
**NuGet:** https://www.nuget.org/packages/Concentus/

Concentus provides Opus audio codec functionality for encoding Tonie files.

---

## FFMpegCore

**Component:** FFMpegCore
**Copyright:** Copyright © Vlad Jerca
**License:** MIT License
**Website:** https://github.com/rosenbjerg/FFMpegCore
**NuGet:** https://www.nuget.org/packages/FFMpegCore/

FFMpegCore provides cross-platform audio resampling and format conversion via FFmpeg.

**Note:** FFMpegCore requires the FFmpeg binary to be installed separately. FFmpeg itself is licensed under LGPL 2.1+ or GPL 2+ depending on compilation flags. Users must install FFmpeg independently on Linux/macOS systems.

---

## Avalonia

**Component:** Avalonia UI Framework
**Copyright:** Copyright © The Avalonia Project
**License:** MIT License
**Website:** https://avaloniaui.net/
**GitHub:** https://github.com/AvaloniaUI/Avalonia

Avalonia provides the cross-platform UI framework for TeddyBench.Avalonia.

---

## CommunityToolkit.Mvvm

**Component:** .NET Community Toolkit - MVVM
**Copyright:** Copyright © .NET Foundation and Contributors
**License:** MIT License
**Website:** https://github.com/CommunityToolkit/dotnet
**NuGet:** https://www.nuget.org/packages/CommunityToolkit.Mvvm/

Provides MVVM helpers for the TeddyBench.Avalonia application.

---

## Newtonsoft.Json

**Component:** Json.NET
**Copyright:** Copyright © James Newton-King
**License:** MIT License
**Website:** https://www.newtonsoft.com/json
**NuGet:** https://www.nuget.org/packages/Newtonsoft.Json/

Used for JSON parsing and serialization throughout the project.

---

## Mono.Options

**Component:** Mono.Options (Command-line parser)
**Copyright:** Copyright © Xamarin Inc.
**License:** MIT License
**NuGet:** https://www.nuget.org/packages/Mono.Options/

Used for command-line argument parsing in the Teddy CLI tool.

---

## ID3

**Component:** ID3 tag library
**Copyright:** Copyright © Jonathan Dibble
**License:** MIT License
**NuGet:** https://www.nuget.org/packages/ID3/

Used for reading ID3 tags from MP3 files.

---

## License Summary

All third-party components use permissive licenses (MIT, LGPL 2.1) that allow:
- ✅ Commercial use
- ✅ Distribution
- ✅ Modification
- ✅ Private use

The only component with copyleft requirements is LibVLC (LGPL 2.1), which is satisfied through dynamic linking and proper attribution (this file).

---

**Last Updated:** 2025-11-16

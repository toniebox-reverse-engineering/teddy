# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Teddy is a tool for working with Tonie audio box files. It can dump existing audio files from Tonie boxes and create custom Tonie-compatible files. The project consists of a CLI tool (`Teddy`) and a GUI application (`TeddyBench`) for Windows.

## Building

### Prerequisites
- .NET 8.0 SDK or later
- FFmpeg (required for audio resampling on Linux/macOS)
- fatattr (required for LIVE flag management on Linux: `sudo apt install fatattr`)
- VLC media player libraries (required for TeddyBench.Avalonia audio playback):
  - **Windows**: Bundled via VideoLAN.LibVLC.Windows NuGet package (no installation required)
  - **macOS**: Bundled via VideoLAN.LibVLC.Mac NuGet package (no installation required)
  - **Linux**: Install system-wide via package manager:
    - Debian/Ubuntu: `sudo apt install vlc libvlc-dev`
    - Fedora: `sudo dnf install vlc vlc-devel`
    - Arch: `sudo pacman -S vlc`
- Windows OS (required for TeddyBench GUI application only)

### Build Commands

**Cross-platform CLI (Teddy):**
```bash
# Restore packages
dotnet restore Teddy/Teddy.csproj

# Build
dotnet build Teddy/Teddy.csproj --configuration Release

# Run
dotnet Teddy/bin/Release/net8.0/Teddy.dll --help
```

**Cross-platform GUI (TeddyBench.Avalonia):**
```bash
# Build
dotnet build TeddyBench.Avalonia/TeddyBench.Avalonia.csproj --configuration Release

# Run
dotnet TeddyBench.Avalonia/bin/Release/net8.0/TeddyBench.Avalonia.dll
```

**Windows GUI (TeddyBench) - Windows only:**
```bash
# Requires Windows and MSBuild
msbuild TeddyBench/TeddyBench.csproj -t:rebuild -property:Configuration=Release
```

### Output Locations
- Teddy CLI: `Teddy/bin/{Configuration}/net8.0/Teddy.dll`
- TeddyBench.Avalonia GUI: `TeddyBench.Avalonia/bin/{Configuration}/net8.0/TeddyBench.Avalonia.dll` (cross-platform)
- TeddyBench GUI: `TeddyBench/bin/{Configuration}/net48/win10-x64/TeddyBench.exe` (Windows only)

## Architecture

### Project Structure

The solution contains 7 projects organized in a layered architecture:

```
Teddy (CLI)            ──┐
TeddyBench.Avalonia ────├──> TonieAudio (Core Library) ──┬──> Concentus (Opus Codec)
(Cross-platform GUI)    │                                 ├──> Concentus.Oggfile (Ogg Container)
TeddyBench ─────────────┘                                 └──> ID3 (ID3 Tag Parsing)
(Windows GUI)
```

**Core Projects:**
- **TonieAudio** - Core library (.NET 8.0) containing the main logic for encoding/decoding Tonie files
  - Key classes: `TonieAudio` (main file operations), `TonieTools` (utilities), `ProtoCoder` (protobuf serialization), `CrossPlatformResampler` (audio resampling), `CrossPlatformAudioReader` (multi-format audio decoding)
  - Dependencies: NAudio, Concentus, Concentus.Oggfile, ID3, FFMpegCore
  - Supports multiple input audio formats: MP3, OGG, FLAC, WAV, M4A, AAC, WMA

- **Teddy** - Console application (.NET 8.0) providing cross-platform CLI interface
  - Entry point: `Teddy/Program.cs`
  - Uses Mono.Options for command-line parsing
  - Runs on Windows, Linux, and macOS

- **TeddyBench.Avalonia** - Cross-platform GUI application (.NET 8.0) with Avalonia UI
  - Modern MVVM architecture with CommunityToolkit.Mvvm
  - Key features: file browsing with icon view, metadata display, decode, show info, LIVE flag management, delete, custom tonie creation, audio playback
  - Automatically downloads tonies.json metadata database on first run from https://api.revvox.de/tonies.json
  - Downloads and caches Tonie images from CDN (stored in cache/ directory)
  - Auto-detects Toniebox SD cards and navigates to CONTENT folder
  - Displays Tonies as icons with proper titles instead of cryptic filenames
  - Custom tonies support via customTonies.json (hash-to-title mapping for unknown tonies)
    - Tonie title determined from audio metadata tags with priority: Album Artist - Album Title, Artist - Album Title, Album Title, or folder name (fallback)
    - RFID stored in reverse byte order in customTonies.json for readability
    - Format: `"Title [RFID: 0EED5104]"` (user-entered format, not directory format)
  - Add Custom Tonie workflow:
    - RFID input: 4-character user input + 4-character configurable prefix (default "0EED" in reverse byte order)
    - Entire 8-character string editable with automatic uppercase conversion
    - Multi-format audio file picker: MP3, OGG, FLAC, WAV, M4A, AAC, WMA
    - Track sorting dialog with Move Up/Down buttons and multi-selection (Shift/Ctrl)
    - Automatically determines tonie title using metadata tags with priority:
      1. Album Artist - Album Title (if both exist)
      2. Artist - Album Title (if Album Artist missing but Artist and Album Title exist)
      3. Album Title (if only Album Title set)
      4. Source folder name (fallback)
    - Creates RFID directory structure: `{reversed-uid}/500304E0`
  - Delete functionality:
    - Confirmation dialog before deletion
    - Removes file, directory, and customTonies.json entry
    - Accessible via context menu
  - Progress indication:
    - Indeterminate progress bar during directory scanning
    - Detailed status messages (directory selected, scanning, reading files with progress counter)
    - Real-time status updates for all operations (decode, toggle LIVE flag, encode, etc.)
  - LIVE flag management:
    - Detects and displays files with Hidden attribute (LIVE flag set by Toniebox)
    - Shows [LIVE] prefix for affected files
    - "Remove All LIVE Flags" button to bulk-remove flags from all tonies (only removes, never adds)
    - Context menu "Toggle LIVE Flag" to toggle flag on individual files (can add or remove)
    - Right-clicking automatically selects the file before showing context menu
    - LIVE flag prevents auto-resume on Toniebox when figurine is removed/replaced
    - Cross-platform implementation:
      - Linux: Uses `fatattr` command to manipulate FAT32 DOS attributes
      - Windows: Uses FileInfo.Attributes API
      - macOS: Uses `fatattr` command (same as Linux)
    - Implementation matches Windows TeddyBench behavior: single-file toggle, bulk-only-remove
  - Audio Player functionality:
    - Built-in audio playback for Tonie files using LibVLCSharp
    - Play/pause/resume controls with visual feedback
    - Stop button to end playback
    - Real-time position tracking
    - Duration display with time formatting (m:ss or h:mm:ss)
    - Cross-platform audio output (Windows, Linux, macOS)
    - Implementation: AudioPlayerService.cs using LibVLC media engine
  - Key services:
    - AudioPlayerService (cross-platform audio playback using LibVLCSharp)
    - TonieMetadataService (handles metadata, image downloads, and customTonies.json management)
    - PathToBitmapConverter (converts file paths to displayable images)
  - Configuration via appsettings.json:
    - RfidPrefix: 4-character prefix in reverse byte order (default "0EED" for ED0E)
  - Runs on Windows, Linux, and macOS
  - **Recommended for non-Windows users**

- **TeddyBench** - Windows GUI application (.NET Framework 4.8) with WinForms
  - Full-featured visual interface for all operations
  - Includes hardware reader integration (Proxmark3 support)
  - Output configured as single-file publish
  - **Windows only** - feature complete but not cross-platform

**Audio Libraries:**
- **Concentus** (.NET 8.0) - Opus audio codec implementation
- **Concentus.Oggfile** (.NET 8.0) - Ogg container format handling
- **ID3** (.NET 8.0) - ID3 tag parsing for MP3 metadata
- **LibVLCSharp** (.NET 8.0) - VLC media player bindings for cross-platform audio/video playback
  - Used in TeddyBench.Avalonia for audio playback functionality
  - Native libraries bundled for Windows (VideoLAN.LibVLC.Windows) and macOS (VideoLAN.LibVLC.Mac)
  - Linux requires system VLC installation (no bundled package available)

### Tonie File Format

Tonie files use a custom binary format:
- **Header** (0x1000 bytes): Protobuf-serialized structure containing:
  - SHA1 hash of audio data
  - Audio length
  - Audio ID (typically Unix timestamp)
  - Chapter markers (page numbers in Ogg stream)
  - Padding to align to 4096 bytes

- **Audio Data** (offset 0x1000+): Opus-encoded audio in Ogg container format
  - Sample rate: 48000 Hz
  - Channels: 2 (stereo)
  - Default bitrate: 96 kbps
  - Each Ogg page aligns to 4096-byte boundaries

### Key Components

**File Operations (TonieAudio.cs):**
- `TonieAudio.FromFile()` - Parse existing Tonie file
- `TonieAudio(sources, audioId, bitRate, useVbr, prefixLocation)` - Create new Tonie file from audio sources
- `DumpAudioFiles()` - Extract audio to Ogg files with optional chapter splitting
- `ReadFile()` - Read and validate Tonie file structure
- `ParsePositions()` - Calculate chapter positions from granule information

**Command-Line Operations (Program.cs):**
- `encode` - Create Tonie file from audio files (supports MP3, OGG, FLAC, WAV, M4A, AAC, WMA)
- `decode` - Extract audio from Tonie file to Ogg format
- `info` - Display Tonie file metadata (supports text/CSV/JSON output)
- `rename` - Organize Tonie files using metadata from JSON database

**Audio Encoding:**
- Accepts multiple audio formats as input: MP3, OGG, FLAC, WAV, M4A, AAC, WMA
- Decodes audio using `CrossPlatformAudioReader`:
  - Windows: Uses NAudio's Mp3FileReader for MP3 (efficient), FFmpeg for all other formats
  - Linux/macOS: Uses FFmpeg for all formats (requires `ffmpeg` binary installed)
  - Special case: Ogg files use OpusWaveStream directly (most efficient for Opus-encoded files)
- Resamples to 48kHz stereo using `CrossPlatformResampler`:
  - Windows: Uses MediaFoundationResampler (native)
  - Linux/macOS: Uses FFmpeg (requires `ffmpeg` binary installed)
- Encodes to Opus format via Concentus library
- Writes to Ogg container with proper page alignment
- Optional prefix files can prepend track numbers (0001.mp3, 0002.mp3, etc.)

## Common Operations

### CLI Usage

```bash
# Get information about a Tonie file
Teddy.exe -m info <toniefile>

# Decode Tonie file to audio files
Teddy.exe -m decode -o <output_dir> <toniefile>

# Encode audio files to Tonie format (supports MP3, OGG, FLAC, WAV, M4A, AAC, WMA)
Teddy.exe -m encode -o <output_file> -b 96 <folder_or_files>

# Encode with custom Audio ID
Teddy.exe -m encode -o output.bin -i 0x5E034216 input_folder/

# Encode with VBR and prefix files
Teddy.exe -m encode -b 96 -vbr -p prefix_folder/ input_folder/
```

### Important Implementation Details

**File Validation:**
- Header length must be 0xFFC bytes (4092 bytes) for valid files
- SHA1 hash verification ensures audio data integrity
- Ogg pages must not cross 4096-byte block boundaries

**Chapter Markers:**
- Stored as Ogg page sequence numbers in the protobuf header
- Used to split multi-track files into individual chapters
- Granule positions track precise playback timing (48000 granules per second)

**Audio ID:**
- Typically a Unix timestamp (seconds since epoch)
- Creative Tonies use Audio ID = 1
- Custom files offset timestamp by 0x50000000

**JSON Database:**
- External metadata database at https://api.revvox.de/tonies.json
- Maps Audio IDs and hashes to Tonie titles, tracks, and metadata
- Used for automatic file naming and track information

**Custom Tonies (customTonies.json):**
- Local JSON file for user-defined Tonie names
- Hash-to-title dictionary format: `{ "HASH": "FolderName [RFID: 0EED5104]" }`
- Automatically created for unknown tonies in Avalonia app
- Takes precedence over tonies.json entries
- Stored in application base directory
- TeddyBench.Avalonia format:
  - Determines title from audio metadata tags with priority: Album Artist - Album Title, Artist - Album Title, Album Title, or folder name (fallback)
  - RFID stored in reverse byte order for readability (user-entered format, not directory format)
  - Example: If user enters RFID `0EED5104` and audio files have "The Beatles" as Album Artist and "Abbey Road" as Album Title, entry becomes: `"HASH123...": "The Beatles - Abbey Road [RFID: 0EED5104]"`
  - Directory on SD card: `0451ED0E/500304E0` (reversed)

**LIVE Flag:**
- FAT32 DOS Hidden attribute used by Toniebox to mark manually-placed files
- Prevents auto-resume functionality when figurine is removed/replaced
- TeddyBench.Avalonia detects and displays [LIVE] prefix for flagged files
- Two ways to manage:
  - **Context menu "Toggle LIVE Flag"**: Toggles Hidden attribute on single file (can add or remove)
  - **Button "Remove All LIVE Flags"**: Bulk operation that only removes Hidden attribute, never adds
- Cross-platform implementation (MainWindowViewModel.cs):
  - **Linux/macOS**: Uses `fatattr` command-line tool to manipulate FAT32 DOS attributes
    - `fatattr +h <file>` to add Hidden attribute
    - `fatattr -h <file>` to remove Hidden attribute
    - `fatattr <file>` to check attributes (output contains "h" if hidden)
    - Required because .NET's FileInfo.Attributes doesn't work with FAT32 on Linux
  - **Windows**: Uses FileInfo.Attributes API with bitwise operations
    - `fileInfo.Attributes |= FileAttributes.Hidden` to add
    - `fileInfo.Attributes &= ~FileAttributes.Hidden` to remove
- ContextMenu.Opened event handler automatically selects item on right-click
- Implementation mirrors Windows TeddyBench behavior for consistency

## Hardware Integration

TeddyBench supports direct interaction with NFC readers (specifically Proxmark3 devices) for reading Tonie figurines. The hardware interface is found in TeddyBench project components that handle RFID/NFC communication.

## Version Information

Version info is embedded using GitInfo package, generating version strings from Git repository state in format: `major.minor.patch-branch+commit[,dirty]`

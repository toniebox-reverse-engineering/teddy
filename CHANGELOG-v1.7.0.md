# Teddy v1.7.0

Enhanced cross-platform fork with .NET 8.0 and improved compatibility.

## What's New

### Core Features
- **Cross-platform CLI tool** (Teddy) for encoding/decoding Tonie files
  - Supports multiple audio formats: MP3, OGG, FLAC, WAV, M4A, AAC, WMA
  - Extract audio from Tonie files to OGG format
  - Create custom Tonie files from audio sources
  - Display file metadata (text, CSV, JSON output)

- **Cross-platform GUI application** (TeddyBench.Avalonia)
  - Modern Avalonia UI with MVVM architecture
  - Icon-based file browser with automatic metadata and image downloads
  - Auto-detects Toniebox SD cards and navigates to CONTENT folder
  - Audio playback with play/pause/stop controls and progress bar
  - Multi-selection support with Shift/Ctrl modifiers
  - **Keyboard Navigation**
    - Arrow keys for navigating between Tonies
    - Space bar to play/pause audio
    - Context menu accessible via keyboard (Menu key or Shift+F10)
    - **Mnemonic access mode** - press Alt once to activate mnemonic mode, then press underlined letter to trigger actions (e.g., H for Help)
    - Press Alt again to exit mnemonic mode
    - Automatic focus on first Tonie after directory load
  - **Help System**
    - Built-in help dialog with hotkey reference
    - Complete list of keyboard shortcuts
    - Accessible via Help button or F1 key
  - **Search Functionality**
    - Real-time search with 250ms debounce
    - Filters by display name, Audio ID (hex), folder name, or UID
    - Supports reverse RFID lookup (search by actual RFID to find reversed folder)
    - Clear search with ESC key
    - Search field automatically appears when typing

- **Custom Tonie Creation**
  - Multi-format audio file picker
  - Track sorting dialog with drag-and-drop and Move Up/Down buttons
  - **Automatic track sorting by ID3 tag track numbers** - reads audio file metadata to intelligently order tracks
  - **Enhanced track display with ID3 metadata** - track sort dialog shows formatted track names (track number - artist - title) extracted from audio file ID3 tags
  - Configurable RFID prefix (default: 0EED)
  - **Smart title generation from audio metadata** - automatically creates Tonie titles using ID3 tags with priority: Album Artist - Album Title, then Artist - Album Title, then Album Title alone, falling back to folder name
  - Custom metadata management via customTonies.json
  - **Lossless Tonie Modification** - when adding tracks to existing Tonies, the system now uses efficient in-place editing that preserves the original audio encoding without decoding and re-encoding
  - **Audio ID Modification** - users can now change the Audio ID for any Tonie (custom or official)
    - Unified base class for audio ID management across all dialogs
    - Choose between custom Tonie format (timestamp + 0x50000000 offset) or official Tonie format (integer range 0-99999999)
    - Prevents conflicts with official Tonie ID ranges when creating custom Tonies
    - Smart restoration: TRASHCAN recovery automatically uses saved Audio ID from customTonies.json when available
    - **Correct custom Tonie Audio ID format** - implements proper timestamp-based ID generation for custom Tonies

- **LIVE Flag Management**
  - Visual [LIVE] indicator for flagged files
  - Toggle LIVE flag on individual files (context menu)
  - Bulk remove LIVE flags from all tonies (button)
  - Cross-platform implementation (Windows, Linux, macOS)

- **TRASHCAN Recovery System** ⭐ NEW
  - Recover Tonies deleted by online Toniebox from quarantine directory
  - View all quarantined files with metadata (UID, hash, deletion date, duration)
  - Restore deleted Tonies back to CONTENT folder with original directory structure
  - Permanently delete files from TRASHCAN
  - **Byte-for-byte restoration**: restored files are **100% identical** to originals (preserves Audio ID)
  - **Advanced customTonies.json format**: now stores Directory field for reliable restoration without RFID parsing ambiguity
  - **Conflict resolution**: detects and handles conflicts when restoring to occupied directory
  - **UID prompt**: interactive dialog for unknown Tonies during restoration
  - **Restore as new custom Tonie**: Can restore deleted Tonies even when they have no matching hash entry in customTonies.json or tonies.json - assigns new Audio ID while preserving original audio encoding
  - Accessible via "TRASHCAN Recovery" toolbar button

- **File Operations**
  - Decode Tonie files to audio
  - Display detailed file information
  - Delete Tonies with confirmation dialog
  - Re-assign UID to existing Tonies
  - **Enhanced File Details Display** in Selected File Details panel
    - File size with human-readable formatting (KB/MB/GB)
    - Last modified date and time
    - Directory location information

### Platform Support
- **Windows**: Full support for CLI and GUI (including legacy TeddyBench)
- **Linux**: Full support for CLI and GUI (requires `ffmpeg` and `fatattr` packages)
- **macOS**: Full support for CLI and GUI (requires `ffmpeg` and `fatattr` via Homebrew)

### Technical Improvements
- Self-contained single-file executables for all platforms
- FFmpeg-based audio resampling for Linux/macOS
- NAudio-based audio processing for Windows
- LibVLC integration for cross-platform audio playback
- Opus encoding with configurable bitrate and VBR support
- Proper Ogg page alignment (4096-byte boundaries)
- Platform-specific LibVLC bundling (Windows/macOS bundled, Linux system-installed)
- Optimized release binaries without debug symbols
- .NET 8.0 compatibility (replaced deprecated APIs)
- **BREAKING CHANGE**: customTonies.json format changed from JObject (dictionary) to JArray (array of objects)
  - Added "Directory" field to TonieMetadata model for reliable TRASHCAN restoration
  - Backward compatibility maintained via RFID regex fallback for old entries
  - New format: `[{"No": "0", "Hash": "...", "Title": "...", "Directory": "EA33ED0E", "AudioId": [...], "Tracks": [...]}]`
- **TonieAudio.UpdateFileContent()**: New method to update file content when header is modified (enables Audio ID restoration)
- **TonieAudio.UpdateStreamSerialNumber()**: New method to update Ogg stream serial number without re-encoding audio
  - Enables hash-deterministic TRASHCAN restoration for unknown Tonies
  - Preserves original Opus encoding quality without generation loss
  - Updates only Ogg container metadata (stream serial) and recalculates CRC checksums
  - Detailed documentation in HASH_DETERMINISM_FIX.md
- **TonieAudio.ExtractRawChapterData()** and **CombineOggTracksLossless()**: New methods for lossless Tonie modification
  - Extracts raw Ogg pages without decoding for zero quality loss
  - Combines tracks with proper 4k boundary alignment
  - Enables efficient track addition to existing Tonies without re-encoding
  - Detailed documentation in TONIE_MODIFICATION_FIX.md
- **Comprehensive test coverage** for end-to-end workflows
  - ID3 metadata parsing and display in track sort dialog (TrackSortDialogTests.cs)
  - Audio playback validation including shuffled track order (EndToEndWorkflowTests.cs)

### Bug Fixes
- **Fixed mnemonic key behavior** - keyboard shortcuts now work correctly in mnemonic mode without interfering with search functionality
- **Fixed custom Tonie Audio ID format** - corrected timestamp-based ID generation algorithm for proper custom Tonie creation
- **Fixed critical Audio ID and hash generation behavior** ⚠️ IMPORTANT
  - Audio ID (timestamp) is now correctly used as Ogg logical stream serial number for hardware compatibility
  - Toniebox hardware expects Stream Serial == Audio ID in the Ogg stream
  - Different Audio IDs now produce different hashes (expected behavior for different files)
  - Same audio source with same Audio ID produces identical hash (deterministic and reproducible)
  - This matches actual hardware behavior and ensures proper file identification
  - Enhanced test coverage to verify audio ID uniqueness and hash determinism
- **Fixed hash validation error** when clicking "Show Info" on Tonie files
- **Fixed extended Info display** - improved layout and formatting of detailed file information
- **Fixed sorting dropdown alignment** - now properly right-aligned in UI
- **Fixed audio-id generation bug** that could cause duplicate IDs across different custom Tonies
  - Improved audio ID generation algorithm to ensure uniqueness
  - Added comprehensive test suite to verify audio ID and hash generation specifications
- **Fixed space bar audio playback** issue where pressing and holding space caused buggy behavior
- **Fixed Tonie modification workflow** - completely redesigned using lossless approach for efficiency and quality
  - Original tracks are now preserved without decoding/re-encoding
  - Proper 4k boundary alignment prevents Ogg page errors
  - Eliminates red LED flash issue when playing modified Tonies
  - Comprehensive test coverage in EndToEndWorkflowTests.cs and HybridEncodingTests.cs
- **Fixed encoding progress dialog** now properly displays during Tonie modification workflow
- **Fixed TRASHCAN restoration to preserve original Audio ID**
  - Restored Tonies now have identical Audio ID to the original file
  - Added comprehensive test suite to verify byte-for-byte restoration (TrashcanRestorationTests.cs)
  - Updated all test fixtures to work with new JArray-based customTonies.json format
- Replaced deprecated `Thread.Abort()` for .NET 8.0 compatibility
- Replaced deprecated `WebRequest` with `HttpClient` to eliminate SYSLIB0014 warnings
- **Improved test reliability** - removed hardcoded SD card paths from test fixtures
- **Fixed granule position handling for normalized tracks** - resolved critical bugs causing incorrect track durations when mixing pre-encoded and new tracks in lossless modification workflow
- **Added consistent line ending handling** - implemented .gitattributes for cross-platform development consistency with LF normalization

### Legal & Licensing
- MIT License for Teddy codebase
- Full LGPL 2.1 compliance for LibVLC usage
- Comprehensive third-party notices document (THIRD-PARTY-NOTICES.md)
- Clear attribution for all open-source dependencies

## Installation

Download the appropriate binary for your platform:
- **Windows**: `teddy-v1.7.0-win-x64.zip` or `teddybench-v1.7.0-win-x64.zip`
- **Linux**: `teddy-v1.7.0-linux-x64.tar.gz` or `teddybench-v1.7.0-linux-x64.tar.gz`
- **macOS (Intel)**: `teddy-v1.7.0-osx-x64.tar.gz` or `teddybench-v1.7.0-osx-x64.tar.gz`
- **macOS (Apple Silicon)**: `teddy-v1.7.0-osx-arm64.tar.gz` or `teddybench-v1.7.0-osx-arm64.tar.gz`

### Linux/macOS Prerequisites
```bash
# Ubuntu/Debian (VLC required for audio playback in GUI)
sudo apt install ffmpeg fatattr vlc libvlc-dev

# macOS (Homebrew - only FFmpeg needed, LibVLC is bundled)
brew install ffmpeg
```

## Usage

### CLI Examples
```bash
# Get information about a Tonie file
./Teddy -m info <toniefile>

# Decode Tonie file to audio
./Teddy -m decode -o <output_dir> <toniefile>

# Encode audio files to Tonie format
./Teddy -m encode -o <output_file> -i <audio_id> <folder_or_files>
```

### GUI
Simply launch TeddyBench.Avalonia and navigate to your Toniebox SD card's CONTENT folder.

## Known Issues
- None at this time

## Credits
Developed by the Teddy community. Special thanks to all contributors and testers.

---

**Full Changelog**: https://github.com/BdN3504/teddy/commits/v1.7.0

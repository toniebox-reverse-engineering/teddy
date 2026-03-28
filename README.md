# Teddy - Tonie File Tool

With this tool you can dump existing files of the famous Toniebox audio box or create custom ones.

## Features

- **Cross-platform CLI tool** (Teddy) for encoding/decoding Tonie files
- **Cross-platform GUI application** (TeddyBench.Avalonia) with modern Avalonia UI
- Support for multiple audio formats: MP3, OGG, FLAC, WAV, M4A, AAC, WMA
- Audio playback with visual controls
- Custom Tonie creation with track sorting
- LIVE flag management
- Auto-detection of Toniebox SD cards

## Installation

Download pre-built binaries from the [Releases](https://github.com/BdN3504/teddy/releases) page.

### Linux/macOS Prerequisites
```bash
# Ubuntu/Debian (VLC required for audio playback in GUI)
sudo apt install ffmpeg fatattr vlc libvlc-dev

# macOS (Homebrew - only FFmpeg needed, LibVLC is bundled)
brew install ffmpeg
```

## Usage

### CLI
```bash
# Get information about a Tonie file
./Teddy -m info <toniefile>

# Decode Tonie file to audio
./Teddy -m decode -o <output_dir> <toniefile>

# Encode audio files to Tonie format
./Teddy -m encode -o <output_file> -i <audio_id> <folder_or_files>
```

### GUI
Launch TeddyBench.Avalonia and navigate to your Toniebox SD card's CONTENT folder.

## Building from Source

See [CLAUDE.md](CLAUDE.md) for detailed build instructions.

## License

Teddy is licensed under the MIT License. See [LICENSE](LICENSE) for details.

This project uses several third-party libraries, most notably LibVLC which is licensed under LGPL 2.1. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for complete licensing information.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

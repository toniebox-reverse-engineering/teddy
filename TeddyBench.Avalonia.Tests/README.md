# TeddyBench.Avalonia End-to-End Tests

This project contains comprehensive end-to-end integration tests for the TeddyBench.Avalonia application.

## Test Overview

The test suite verifies the complete user workflow:

1. **Start Application** - Initializes the app and waits for metadata (tonies.json) to load
2. **Open Directory** - Navigates to `/media/username/3238-3133/CONTENT` directory (Toniebox SD card)
3. **Select Tonie** - Finds tonie at `/media/username/3238-3133/CONTENT/A13DED0E/500304E0`, calculates SHA1 hash, and matches it against customTonies.json
4. **Delete Tonie** - Removes the selected tonie from the SD card and updates customTonies.json
5. **Add Custom Tonie** - Creates new custom tonie with track1.mp3 and track2.mp3
6. **Open Player (Initial)** - Opens the audio player for the newly created tonie
7. **Test Initial Tracks** - Tests playback of both initial tracks and verifies:
   - Both tracks can be played back without errors
   - Track 1 duration displayed in player matches source MP3 file
   - Track 2 duration displayed in player matches source MP3 file
8. **Open Modify Dialog** - Opens the modify dialog showing existing 2 tracks
9. **Add Track3 and Encode** - Adds track3.mp3 to the track list, presses Encode button, and waits for encoding to complete
10. **Refresh and Select Modified Tonie** - Refreshes the directory view and selects the newly modified tonie
11. **Open Player (Modified)** - Opens the audio player for the modified tonie
12. **Test All Three Tracks** - Tests playback of all three tracks and verifies:
   - All 3 tracks can be played back without errors
   - Track 1 duration displayed in player matches source MP3 file
   - Track 2 duration displayed in player matches source MP3 file
   - Track 3 duration displayed in player matches source MP3 file
13. **Final Verification** - Verifies the tonie file structure contains exactly 3 tracks with correct durations

## Prerequisites

- .NET 8.0 SDK
- FFmpeg (required for audio duration calculations)
- VLC libraries (required for audio playback testing, see TeddyBench.Avalonia prerequisites)
- xUnit test runner
- Test audio files: track1.mp3, track2.mp3, track3.mp3 (automatically copied from TonieAudio.Tests/TestData)

## Running the Tests

### Via Command Line

```bash
# Build the test project
dotnet build TeddyBench.Avalonia.Tests/TeddyBench.Avalonia.Tests.csproj

# Run all tests
dotnet test TeddyBench.Avalonia.Tests/TeddyBench.Avalonia.Tests.csproj

# Run with detailed output
dotnet test TeddyBench.Avalonia.Tests/TeddyBench.Avalonia.Tests.csproj --logger "console;verbosity=detailed"
```

### Via IDE

The tests can be run from any IDE that supports xUnit (Visual Studio, VS Code with C# extension, Rider, etc.).

## Test Architecture

### Headless Testing

The tests use Avalonia.Headless to run the application in a headless mode without requiring a display server. This allows the tests to:
- Run in CI/CD environments
- Execute on headless Linux servers
- Run much faster than traditional UI automation

### Integration Testing Approach

Rather than testing at the UI control level (which is brittle), these tests:
- **Directly invoke ViewModel methods** - Using reflection to call internal methods like `ScanDirectory`
- **Test at the service layer** - Directly using services like `TonieMetadataService`, `CustomTonieCreationService`, etc.
- **Verify file system state** - Checking that files are created/deleted correctly
- **Validate metadata** - Ensuring customTonies.json is updated properly
- **Test audio properties** - Verifying track durations using FFmpeg

### Test Data Management

- **Temporary test directories** - Each test creates a unique temporary directory structure that mimics a Toniebox SD card
- **Cleanup** - Test directories and customTonies.json entries are automatically cleaned up after each test
- **Isolation** - Tests are fully isolated and don't affect each other or the actual file system

## Project Structure

```
TeddyBench.Avalonia.Tests/
├── AssemblyInfo.cs              # Avalonia test application configuration
├── TestAppBuilder.cs            # Avalonia headless app builder
├── EndToEndWorkflowTests.cs     # Main end-to-end test class
└── README.md                    # This file
```

## Test Class: EndToEndWorkflowTests

### Main Test Method

`CompleteWorkflow_CreateDeleteModifyAndPlayTonie_ShouldSucceed()` - Comprehensive end-to-end test covering all major features

### Helper Methods

- `SimulateDirectoryOpen` - Simulates opening a directory via reflection
- `SimulateDeleteTonie` - Simulates deleting a tonie including file system and metadata cleanup
- `SimulateAddCustomTonie` - Simulates creating a custom tonie with audio files
- `TestPlayerFunctionality` - Tests the audio player with N tracks, verifies playback and track durations
- `SimulateModifyTonieWithEncode` - Simulates the complete modification workflow:
  - Decodes original tracks from tonie
  - Adds new track to the list
  - Presses "Encode" button (performs hybrid encoding)
  - Waits for encoding to complete
  - Updates metadata
- `GetAudioDuration` - Uses FFmpeg to get the duration of audio files
- `ExtractDurationFromDisplayText` - Parses duration from track display text (e.g., "Track 1 (2:34)")
- `VerifyTrackDurations` - Verifies that track durations in the tonie match source files

## Known Limitations

1. **Audio Playback Testing** - Actual audio playback may not work in fully headless environments (e.g., CI/CD without audio devices). The test handles this gracefully and skips audio verification if LibVLC initialization fails.

2. **File System Permissions** - Tests create temporary directories and files. Ensure the test runner has appropriate permissions.

3. **Real SD Card Testing** - The test currently creates a mock SD card structure. To test against a real SD card, you would need to modify the test to use actual paths (be careful of data loss!).

4. **Platform-Specific Features** - Some features (like LIVE flag management) may behave differently on different platforms. Tests should be run on all target platforms.

## Troubleshooting

### Tests Fail with "Could not find ffprobe"

Install FFmpeg:
```bash
# Ubuntu/Debian
sudo apt install ffmpeg

# macOS
brew install ffmpeg

# Windows
# Download from https://ffmpeg.org/download.html
```

### Tests Fail with VLC Errors

Install VLC libraries (see TeddyBench.Avalonia prerequisites in main CLAUDE.md).

### Tests Timeout

Increase the delay in Step 1 if metadata downloading is slow:
```csharp
await Task.Delay(5000); // Increase from 2000 to 5000ms
```

## Contributing

When adding new features to TeddyBench.Avalonia, please:
1. Add corresponding test coverage in this project
2. Follow the existing integration testing patterns
3. Ensure tests clean up after themselves
4. Document any new test prerequisites

## License

Same as the main Teddy project.

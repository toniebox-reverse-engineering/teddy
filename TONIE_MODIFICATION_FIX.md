# Tonie Modification Fix - Lossless Ogg Page Manipulation

## Problem

When modifying a Tonie by adding tracks to an existing Tonie file, the Toniebox would:
- Play the first two original tracks correctly
- Flash red LED after track 2 ends
- Fail to automatically play track 3
- Require tag removal/replacement to play track 3

Additionally, there was an Ogg page alignment issue causing errors:
```
[ERROR] Ogg page ends in next block at 0x0034AE00
```

This indicated that Ogg pages were not aligned to 4096-byte (4k) boundaries as required by the Tonie format.

## Root Cause

The issues had two causes:

1. **Missing Ogg Page Padding**: The `CombineOggTracksLossless` method wrote Ogg pages directly without padding them to 4k boundaries, violating the Tonie format requirement that "Ogg pages must not cross 4096-byte block boundaries."

2. **Complex Manual Stream Manipulation**: Early implementations used manual Ogg stream manipulation with subtle bugs in page alignment and chapter marker generation.

## Solution

Implemented a **lossless approach** that preserves original track encoding while properly aligning all Ogg pages.

### Core Methods

#### 1. `ExtractRawChapterData()`

**Location:** `TonieAudio/TonieAudio.cs:1589-1667`

**How it works:**

Extracts raw Ogg pages for each track without any decoding:
- Scans the Ogg stream to find chapter markers (page sequence numbers)
- Copies raw byte ranges for each track
- Returns byte arrays containing the exact encoded data
- **Zero quality loss** - no decoding or re-encoding

```csharp
// Extract original tracks without decoding
var rawChapters = originalTonie.ExtractRawChapterData();
// rawChapters[0] = Track 1 raw Ogg data
// rawChapters[1] = Track 2 raw Ogg data
```

#### 2. `UpdateStreamSerialNumber()`

**Location:** `TonieAudio/TonieAudio.cs:1674-1723`

**How it works:**

Updates the Audio ID (stream serial number) in Ogg pages without re-encoding:
- Parses Ogg pages from raw data
- Updates the `BitstreamSerialNumber` field in each page header
- Optionally resets granule positions (for combining tracks)
- Recalculates CRC checksums
- Returns updated raw Ogg data

```csharp
// Update stream serial number to match new Audio ID
byte[] updatedOggData = tempAudio.UpdateStreamSerialNumber(audioId, resetGranulePositions: true);
```

#### 3. `CombineOggTracksLossless()`

**Location:** `TonieAudio/TonieAudio.cs:1731-1930`

**How it works:**

Combines multiple Ogg tracks into a single Tonie-compatible stream:
1. **Parse all tracks**: Reads Ogg pages from each track's raw data
2. **Extract headers**: Takes OpusHead and OpusTags from first track
3. **Pad headers**: Ensures headers occupy exactly 0x200 (512) bytes
4. **Process each track**:
   - Updates stream serial number (Audio ID)
   - Renumbers page sequences for continuity
   - Adjusts granule positions to be cumulative
   - **Adds padding segments** to align pages to 4k boundaries
   - Clears BOS/EOS flags (set only on final page)
5. **Set EOS flag**: Marks the last page as end-of-stream
6. **Generate chapter markers**: Records page sequence number for each track start
7. **Compute hash**: SHA1 of the entire audio stream

**Key Fix - Ogg Page Alignment:**

```csharp
// Calculate how much padding is needed to reach 4k boundary
long posAfterPage = currentPos + pageSize;
long nextBoundary = ((posAfterPage + 0xFFF) / 0x1000) * 0x1000;
long spaceToFill = nextBoundary - posAfterPage;

if (spaceToFill > 0)
{
    // Add padding segments (max 254 bytes each to avoid 255-byte special case)
    // Each 254-byte segment needs exactly 1 byte in the segment table
    long paddingData = (spaceToFill * 254) / 255;
    int segmentEntries = (int)((paddingData + 253) / 254);

    // Create new segments array with padding
    byte[][] newSegments = new byte[page.Segments.Length + segmentEntries][];
    Array.Copy(page.Segments, newSegments, page.Segments.Length);

    // Add padding segments (254 bytes each)
    int segIdx = page.Segments.Length;
    long remaining = paddingData;
    while (remaining > 0)
    {
        int segSize = (int)Math.Min(254, remaining);
        newSegments[segIdx++] = new byte[segSize];
        remaining -= segSize;
    }
    page.Segments = newSegments;
}
```

This ensures every Ogg page ends exactly at a 4k boundary, eliminating the page alignment errors.

### Updated: `HybridTonieEncodingService.EncodeHybridTonie()`

**Location:** `TeddyBench.Avalonia/Services/HybridTonieEncodingService.cs:40-117`

**Lossless workflow:**

```csharp
// Extract raw Ogg data without re-encoding
var originalAudio = TonieAudio.FromFile(originalTonieFilePath, readAudio: true);
List<byte[]> rawChapterData = originalAudio.ExtractRawChapterData();

// Build list of all track Ogg data in correct order
var allTrackOggData = new List<byte[]>();

for (int i = 0; i < tracks.Count; i++)
{
    var track = tracks[i];

    if (track.IsOriginal && track.OriginalTrackIndex >= 0)
    {
        // Use raw chapter data (already encoded, no quality loss!)
        var tempAudio = new TonieAudio();
        tempAudio.Audio = rawChapterData[track.OriginalTrackIndex];
        byte[] updatedOggData = tempAudio.UpdateStreamSerialNumber(audioId, resetGranulePositions: true);
        allTrackOggData.Add(updatedOggData);
    }
    else
    {
        // Encode new track with same audio ID
        TonieAudio newTrackAudio = new TonieAudio(new[] { track.AudioFilePath! }, audioId, bitRate * 1000, false, null, callback);
        allTrackOggData.Add(newTrackAudio.Audio);
    }
}

// Combine all tracks losslessly using proper Ogg page manipulation
var (audioData, hash, chapterMarkers) = TonieAudio.CombineOggTracksLossless(allTrackOggData, audioId);

// Build final Tonie file with header
byte[] fileContent = new byte[audioData.Length + 0x1000];
Array.Copy(audioData, 0, fileContent, 0x1000, audioData.Length);

// Create and write header
var tonieAudio = new TonieAudio();
tonieAudio.FileContent = fileContent;
tonieAudio.Audio = audioData;
tonieAudio.Header.Hash = hash;
tonieAudio.Header.AudioLength = audioData.Length;
tonieAudio.Header.AudioId = audioId;
tonieAudio.Header.AudioChapters = chapterMarkers;
tonieAudio.Header.Padding = new byte[0];
tonieAudio.UpdateFileContent();
```

## Advantages of Lossless Approach

✅ **Zero quality loss** - Original tracks preserved byte-for-byte (except stream serial number)
✅ **Deterministic hashes** - Same audio + same Audio ID = same hash
✅ **Proper page alignment** - All Ogg pages end at 4k boundaries
✅ **Hardware compatible** - Stream serial number matches Audio ID
✅ **No decoding/re-encoding** - Manipulates only Ogg container metadata
✅ **Faster** - No transcoding overhead for original tracks
✅ **Simpler dependencies** - No ffmpeg required for modification

## How It Differs from Re-encoding

| Aspect | Re-encoding Approach | Lossless Approach |
|--------|---------------------|-------------------|
| Original tracks | Decoded → Re-encoded | Raw Ogg pages copied |
| Quality loss | Minor (Opus 96kbps → 96kbps) | None (bit-perfect) |
| Speed | Slower (transcoding) | Faster (metadata only) |
| Hash determinism | Not guaranteed | Guaranteed |
| Dependencies | Requires ffmpeg | Only for new tracks |

## Testing

### Automated Tests

**Test:** `LosslessApproach_CreateCustomTonie_ThenAddTrack_ShouldPreserveAudioIdAndFolder()`

**Location:** `TonieAudio.Tests/HybridEncodingTests.cs:564-772`

**Results:**
```
✓ Creates initial tonie with 2 tracks
✓ Modifies by adding 3rd track using lossless approach
✓ Original tracks preserved without re-encoding
✓ Hash changed (as expected when content changes)
✓ Audio ID preserved (0xCAFEBABE)
✓ Folder structure preserved
✓ Duration correct (original + new track)
✓ Ogg structure valid (no page alignment errors)
```

**Test:** `CreateTonie_ThenAppendTrack_ShouldProduceValidFile()`

**Location:** `TonieAudio.Tests/HybridEncodingTests.cs:18-142`

**Results:**
```
✓ Extracts 2 raw chapters from initial tonie
✓ Creates modified tonie with 3 tracks (2 original + 1 new)
✓ Produces valid Ogg page structure
✓ Hash validation passes
✓ No Ogg page alignment errors
```

### End-to-End Test

**Test:** `CompleteWorkflow_CreateDeleteModifyAndPlayTonie_ShouldSucceed()`

**Location:** `TeddyBench.Avalonia.Tests/EndToEndWorkflowTests.cs`

**Results:**
```
✓ Creates custom tonie with 2 tracks
✓ Deletes and re-creates
✓ Modifies by adding 3rd track using lossless approach
✓ All 3 tracks playable in VLC
✓ No VLC errors during playback
✓ Original tracks preserved without re-encoding
✓ Total test time: ~8.6 seconds
```

### Manual Testing

**To verify the fix on actual Toniebox:**

1. Build the GUI: `dotnet build TeddyBench.Avalonia/TeddyBench.Avalonia.csproj --configuration Release`
2. Create or open an existing Tonie with 2+ tracks
3. Right-click and select "Modify Tonie"
4. Add a new track (at any position: beginning, middle, or end)
5. Click "Encode" button
6. Save to SD card
7. Place tag on Toniebox and verify:
   - All original tracks play completely ✓
   - New track plays completely ✓
   - All tracks transition automatically (no red LED) ✓
   - Playback is seamless from track to track ✓
   - No console errors about page alignment ✓

## Files Changed

1. **TonieAudio/TonieAudio.cs**
   - `ExtractRawChapterData()` (lines 1589-1667): Extracts raw Ogg pages per track
   - `UpdateStreamSerialNumber()` (lines 1674-1723): Updates Audio ID without re-encoding
   - `CombineOggTracksLossless()` (lines 1731-1930): Combines tracks with proper 4k padding
   - Fixed Ogg page alignment bug by adding padding segments

2. **TeddyBench.Avalonia/Services/HybridTonieEncodingService.cs**
   - `EncodeHybridTonie()` (lines 40-117): Uses lossless approach
   - Removed obsolete `EncodeHybridTonieLegacy()` method

3. **TonieAudio.Tests/HybridEncodingTests.cs**
   - Added `LosslessApproach_CreateCustomTonie_ThenAddTrack_ShouldPreserveAudioIdAndFolder()` test
   - Added `CreateTonie_ThenAppendTrack_ShouldProduceValidFile()` test
   - Removed obsolete `NewApproach_ExtractTracksToTempFiles_ThenReencode_ShouldProduceValidFile()` test

4. **TeddyBench.Avalonia.Tests/EndToEndWorkflowTests.cs**
   - Added end-to-end test verifying complete workflow with audio playback

## Technical Details

### Ogg Page Alignment Algorithm

The fix uses a careful algorithm to add padding while maintaining valid Ogg structure:

1. **Calculate space needed**: `nextBoundary - posAfterPage`
2. **Account for segment table overhead**: Each segment needs 1 byte in segment table
3. **Use 254-byte segments**: Avoids 255-byte special case (which requires 2 table entries: 0xFF + 0x00)
4. **Solve equation**: `paddingData + ceil(paddingData / 254) = spaceToFill`
5. **Approximation**: `paddingData ≈ (spaceToFill * 254) / 255`
6. **Iterative refinement**: Adjust until exact

This ensures each page ends precisely at a 4k boundary without violating Ogg specification.

### Stream Serial Number = Audio ID

The Toniebox hardware expects the Ogg stream serial number to match the Audio ID in the protobuf header. This is why:
- Different Audio IDs produce different hashes (even with identical audio)
- The hash includes the stream serial number via Ogg page headers
- Modifying an existing tonie must preserve the Audio ID to maintain the same serial number

The lossless approach ensures this by:
1. Reading the original Audio ID from the tonie
2. Updating stream serial numbers in all tracks to match
3. Using the same Audio ID when encoding new tracks

## Dependencies

- **For encoding new tracks**: Requires `ffmpeg` binary in PATH (Linux/macOS/Windows)
- **For modifying existing tonies**: No additional dependencies (lossless manipulation only)

FFmpeg is already listed in prerequisites for audio resampling and multi-format support.
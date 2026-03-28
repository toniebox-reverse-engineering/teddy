using System;
using System.IO;
using Xunit;
using TonieFile;

namespace TonieAudio.Tests
{
    public class HybridEncodingTests
    {
        private readonly string _testDataDir;

        public HybridEncodingTests()
        {
            _testDataDir = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        }

        [Fact]
        public void CreateTonie_ThenAppendTrack_ShouldProduceValidFile()
        {
            // Arrange
            string track1 = Path.Combine(_testDataDir, "track1.mp3");
            string track2 = Path.Combine(_testDataDir, "track2.mp3");
            string track3 = Path.Combine(_testDataDir, "track3.mp3");

            Assert.True(File.Exists(track1), $"Track1 not found: {track1}");
            Assert.True(File.Exists(track2), $"Track2 not found: {track2}");
            Assert.True(File.Exists(track3), $"Track3 not found: {track3}");

            string initialTonieFile = Path.Combine(Path.GetTempPath(), $"test_initial_{Guid.NewGuid()}.bin");
            string modifiedTonieFile = Path.Combine(Path.GetTempPath(), $"test_modified_{Guid.NewGuid()}.bin");

            try
            {
                // Act 1: Create initial tonie with 2 tracks
                Console.WriteLine("=== STEP 1: Creating initial tonie with 2 tracks ===");
                TonieFile.TonieAudio initialTonie = new TonieFile.TonieAudio(
                    new[] { track1, track2 },
                    audioId: 12345,
                    bitRate: 96000,
                    useVbr: false,
                    prefixLocation: null,
                    cbr: null
                );

                File.WriteAllBytes(initialTonieFile, initialTonie.FileContent);
                Console.WriteLine($"Initial tonie created: {initialTonieFile}");
                Console.WriteLine($"Initial tonie size: {initialTonie.FileContent.Length} bytes");
                Console.WriteLine($"Initial tonie has {initialTonie.Header.AudioChapters.Length} chapters");

                // Assert 1: Initial tonie should be valid
                Assert.Equal(2, initialTonie.Header.AudioChapters.Length);
                Assert.True(initialTonie.FileContent.Length > 0x1000);

                // Act 2: Read back and validate
                Console.WriteLine("\n=== STEP 2: Reading back initial tonie ===");
                TonieFile.TonieAudio readBack = TonieFile.TonieAudio.FromFile(initialTonieFile, readAudio: true);
                Console.WriteLine($"Read back tonie: {readBack.Header.AudioChapters.Length} chapters");
                Console.WriteLine($"Hash correct: {readBack.HashCorrect}");

                // Assert 2: Read back should match
                Assert.True(readBack.HashCorrect, "Initial tonie hash should be correct");
                Assert.Equal(2, readBack.Header.AudioChapters.Length);

                // Act 3: Extract raw chapters
                Console.WriteLine("\n=== STEP 3: Extracting raw chapters ===");
                var rawChapters = readBack.ExtractRawChapterData();
                Console.WriteLine($"Extracted {rawChapters.Count} raw chapters");
                foreach (var i in Enumerable.Range(0, rawChapters.Count))
                {
                    Console.WriteLine($"  Chapter {i}: {rawChapters[i].Length} bytes");
                }

                // Assert 3: Should have extracted 2 chapters
                Assert.Equal(2, rawChapters.Count);

                // Act 4: Create modified tonie with original 2 tracks + new track
                Console.WriteLine("\n=== STEP 4: Creating modified tonie with 3 tracks (2 original + 1 new) ===");
                var trackSources = new[]
                {
                    new TonieFile.TonieAudio.TrackSource(rawChapters[0]), // Original track 1
                    new TonieFile.TonieAudio.TrackSource(rawChapters[1]), // Original track 2
                    new TonieFile.TonieAudio.TrackSource(track3)          // New track 3
                };

                TonieFile.TonieAudio modifiedTonie = new TonieFile.TonieAudio(
                    trackSources,
                    originalAudioData: readBack.Audio,
                    audioId: 12345,
                    bitRate: 96000,
                    useVbr: false,
                    prefixLocation: null,
                    cbr: null
                );

                File.WriteAllBytes(modifiedTonieFile, modifiedTonie.FileContent);
                Console.WriteLine($"Modified tonie created: {modifiedTonieFile}");
                Console.WriteLine($"Modified tonie size: {modifiedTonie.FileContent.Length} bytes");
                Console.WriteLine($"Modified tonie has {modifiedTonie.Header.AudioChapters.Length} chapters");

                // Assert 4: Modified tonie should have 3 chapters
                Assert.Equal(3, modifiedTonie.Header.AudioChapters.Length);

                // Act 5: Read back modified tonie and validate
                Console.WriteLine("\n=== STEP 5: Reading back modified tonie ===");
                TonieFile.TonieAudio modifiedReadBack = TonieFile.TonieAudio.FromFile(modifiedTonieFile, readAudio: true);
                Console.WriteLine($"Read back modified tonie: {modifiedReadBack.Header.AudioChapters.Length} chapters");
                Console.WriteLine($"Hash correct: {modifiedReadBack.HashCorrect}");

                // Try to calculate statistics (this will fail if pages aren't aligned correctly)
                Console.WriteLine("\n=== STEP 6: Validating Ogg page structure ===");
                try
                {
                    modifiedReadBack.CalculateStatistics(
                        out long totalSegments,
                        out long segLength,
                        out int minSegs,
                        out int maxSegs,
                        out ulong minGranule,
                        out ulong maxGranule,
                        out ulong highestGranule
                    );
                    Console.WriteLine("✓ Ogg page structure is valid");
                    Console.WriteLine($"  Total segments: {totalSegments}");
                    Console.WriteLine($"  Highest granule: {highestGranule}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Ogg page structure validation FAILED: {ex.Message}");
                    throw;
                }

                // Assert 5: Modified tonie should be valid
                Assert.True(modifiedReadBack.HashCorrect, "Modified tonie hash should be correct");
                Assert.Equal(3, modifiedReadBack.Header.AudioChapters.Length);
            }
            finally
            {
                // Cleanup
                if (File.Exists(initialTonieFile)) File.Delete(initialTonieFile);
                if (File.Exists(modifiedTonieFile)) File.Delete(modifiedTonieFile);
            }
        }

        [Fact]
        public void CreateTonieFromThreeNewTracks_ShouldProduceValidFile()
        {
            // Arrange
            string track1 = Path.Combine(_testDataDir, "track1.mp3");
            string track2 = Path.Combine(_testDataDir, "track2.mp3");
            string track3 = Path.Combine(_testDataDir, "track3.mp3");

            string tonieFile = Path.Combine(Path.GetTempPath(), $"test_three_tracks_{Guid.NewGuid()}.bin");

            try
            {
                // Act: Create tonie with 3 new tracks
                Console.WriteLine("=== Creating tonie with 3 new tracks ===");
                TonieFile.TonieAudio tonie = new TonieFile.TonieAudio(
                    new[] { track1, track2, track3 },
                    audioId: 54321,
                    bitRate: 96000,
                    useVbr: false,
                    prefixLocation: null,
                    cbr: null
                );

                File.WriteAllBytes(tonieFile, tonie.FileContent);
                Console.WriteLine($"Tonie created: {tonieFile}");
                Console.WriteLine($"Tonie size: {tonie.FileContent.Length} bytes");

                // Assert: Should have 3 chapters
                Assert.Equal(3, tonie.Header.AudioChapters.Length);

                // Read back and validate
                TonieFile.TonieAudio readBack = TonieFile.TonieAudio.FromFile(tonieFile, readAudio: true);
                Assert.True(readBack.HashCorrect, "Tonie hash should be correct");
                Assert.Equal(3, readBack.Header.AudioChapters.Length);

                // Validate structure
                readBack.CalculateStatistics(
                    out long totalSegments,
                    out long segLength,
                    out int minSegs,
                    out int maxSegs,
                    out ulong minGranule,
                    out ulong maxGranule,
                    out ulong highestGranule
                );
                Console.WriteLine("✓ Ogg page structure is valid");
            }
            finally
            {
                if (File.Exists(tonieFile)) File.Delete(tonieFile);
            }
        }

        [Fact]
        public void NormalEncoding_ParsePositions_ShouldReturnCorrectPositions()
        {
            // This tests that ParsePositions works correctly for normal encoding
            string track1 = Path.Combine(_testDataDir, "track1.mp3");
            string track2 = Path.Combine(_testDataDir, "track2.mp3");
            string tonieFile = Path.Combine(Path.GetTempPath(), $"test_parse_positions_{Guid.NewGuid()}.bin");

            try
            {
                // Create a normal 2-track tonie
                Console.WriteLine("=== Creating normal 2-track tonie ===");
                TonieFile.TonieAudio tonie = new TonieFile.TonieAudio(
                    new[] { track1, track2 },
                    audioId: 12345,
                    bitRate: 96000,
                    useVbr: false,
                    prefixLocation: null,
                    cbr: null
                );

                File.WriteAllBytes(tonieFile, tonie.FileContent);
                Console.WriteLine($"Created tonie with {tonie.Header.AudioChapters.Length} chapters");
                Console.WriteLine($"Chapter markers: [{string.Join(", ", tonie.Header.AudioChapters)}]");

                // Read back and call ParsePositions
                Console.WriteLine("\n=== Reading back and calling ParsePositions ===");
                TonieFile.TonieAudio readBack = TonieFile.TonieAudio.FromFile(tonieFile, readAudio: true);
                ulong[] positions = readBack.ParsePositions();

                Console.WriteLine($"ParsePositions returned {positions.Length} positions:");
                for (int i = 0; i < positions.Length; i++)
                {
                    double timeSeconds = positions[i] / 48000.0;
                    Console.WriteLine($"  Position {i}: {positions[i]} granules = {timeSeconds:F2}s");
                }

                // For a 2-track file with chapter markers [0, 32], we expect:
                // - Position 0 (implicit start)
                // - Position 0 (from chapter marker 0)
                // - Position at second track (from chapter marker 32)
                // - Final position
                // Total: 4 positions, but player deduplicates to get 2 tracks
                Assert.Equal(4, positions.Length);
                Assert.Equal(0UL, positions[0]); // Always starts at 0
                Assert.Equal(0UL, positions[1]); // Duplicate from chapter marker 0 (will be deduplicated by player)
            }
            finally
            {
                if (File.Exists(tonieFile)) File.Delete(tonieFile);
            }
        }

        [Fact]
        public void ModifiedTonie_AllTracksPlayable_ShouldHaveCorrectDurations()
        {
            // Arrange
            string track1 = Path.Combine(_testDataDir, "track1.mp3");
            string track2 = Path.Combine(_testDataDir, "track2.mp3");
            string track3 = Path.Combine(_testDataDir, "track3.mp3");

            string initialTonieFile = Path.Combine(Path.GetTempPath(), $"test_playback_initial_{Guid.NewGuid()}.bin");
            string modifiedTonieFile = Path.Combine(Path.GetTempPath(), $"test_playback_modified_{Guid.NewGuid()}.bin");

            try
            {
                // Act 1: Create initial tonie with 2 tracks
                Console.WriteLine("=== Creating initial tonie ===");
                TonieFile.TonieAudio initialTonie = new TonieFile.TonieAudio(
                    new[] { track1, track2 },
                    audioId: 12345,
                    bitRate: 96000,
                    useVbr: false,
                    prefixLocation: null,
                    cbr: null
                );

                File.WriteAllBytes(initialTonieFile, initialTonie.FileContent);

                // Validate initial tonie tracks
                Console.WriteLine("Validating initial tonie tracks:");
                var initialReadBack = TonieFile.TonieAudio.FromFile(initialTonieFile, readAudio: true);
                var initialChapters = initialReadBack.ExtractRawChapterData();
                for (int i = 0; i < initialChapters.Count; i++)
                {
                    var pages = 0;
                    ulong lastGranule = 0;
                    int offset = 0;
                    byte[] data = initialChapters[i];

                    while (offset < data.Length - 27)
                    {
                        if (data[offset] == 'O' && data[offset + 1] == 'g' && data[offset + 2] == 'g' && data[offset + 3] == 'S')
                        {
                            ulong granule = BitConverter.ToUInt64(data, offset + 6);
                            byte segCount = data[offset + 26];
                            int dataSize = 0;
                            for (int s = 0; s < segCount; s++) dataSize += data[offset + 27 + s];

                            if (granule != 0 && granule != ulong.MaxValue) lastGranule = granule;
                            pages++;
                            offset += 27 + segCount + dataSize;
                        }
                        else offset++;
                    }

                    Console.WriteLine($"  Initial track {i+1}: {pages} pages, {lastGranule} granules, {lastGranule/48000.0:F2}s");
                }

                // Act 2: Read and create modified tonie
                Console.WriteLine("\n=== Creating modified tonie with 3 tracks ===");
                TonieFile.TonieAudio readBack = TonieFile.TonieAudio.FromFile(initialTonieFile, readAudio: true);
                var rawChapters = readBack.ExtractRawChapterData();

                var trackSources = new[]
                {
                    new TonieFile.TonieAudio.TrackSource(rawChapters[0]),
                    new TonieFile.TonieAudio.TrackSource(rawChapters[1]),
                    new TonieFile.TonieAudio.TrackSource(track3)
                };

                TonieFile.TonieAudio modifiedTonie = new TonieFile.TonieAudio(
                    trackSources,
                    originalAudioData: readBack.Audio,
                    audioId: 12345,
                    bitRate: 96000,
                    useVbr: false,
                    prefixLocation: null,
                    cbr: null
                );

                File.WriteAllBytes(modifiedTonieFile, modifiedTonie.FileContent);

                // Act 3: Read back modified tonie
                Console.WriteLine("\n=== Reading back modified tonie ===");
                TonieFile.TonieAudio modifiedReadBack = TonieFile.TonieAudio.FromFile(modifiedTonieFile, readAudio: true);

                // Act 4: Dump each track to individual Ogg files and validate
                Console.WriteLine("\n=== Extracting and validating individual tracks ===");
                string outputDir = Path.GetTempPath();
                string baseFileName = $"test_playback_{Guid.NewGuid()}";

                // Extract each track
                var extractedChapters = modifiedReadBack.ExtractRawChapterData();
                Assert.Equal(3, extractedChapters.Count);

                for (int i = 0; i < extractedChapters.Count; i++)
                {
                    string trackFile = Path.Combine(outputDir, $"{baseFileName}_track{i + 1}.ogg");
                    Console.WriteLine($"\n--- Track {i + 1} ---");
                    Console.WriteLine($"  Raw data size: {extractedChapters[i].Length} bytes");

                    // Write chapter to file with proper headers
                    modifiedReadBack.WriteChapterToFile(extractedChapters[i], trackFile, i);
                    Console.WriteLine($"  Written to: {trackFile}");

                    // Verify file exists and has content
                    Assert.True(File.Exists(trackFile), $"Track {i + 1} file should exist");
                    FileInfo fi = new FileInfo(trackFile);
                    Assert.True(fi.Length > 0, $"Track {i + 1} file should have content");
                    Console.WriteLine($"  File size: {fi.Length} bytes");

                    // Read as Ogg file and check structure
                    byte[] oggData = File.ReadAllBytes(trackFile);
                    Assert.True(oggData[0] == 'O' && oggData[1] == 'g' && oggData[2] == 'g' && oggData[3] == 'S',
                        $"Track {i + 1} should start with Ogg signature");

                    // Parse pages and check for valid structure
                    int pageCount = 0;
                    ulong firstGranule = ulong.MaxValue;
                    ulong lastGranule = 0;
                    int offset = 0;

                    while (offset < oggData.Length - 27)
                    {
                        if (oggData[offset] == 'O' && oggData[offset + 1] == 'g' &&
                            oggData[offset + 2] == 'g' && oggData[offset + 3] == 'S')
                        {
                            ulong granule = BitConverter.ToUInt64(oggData, offset + 6);
                            byte segmentCount = oggData[offset + 26];

                            int dataSize = 0;
                            for (int s = 0; s < segmentCount; s++)
                            {
                                dataSize += oggData[offset + 27 + s];
                            }

                            pageCount++;

                            // Track first and last valid granules
                            // Note: granule 0 is valid (normalized tracks start at 0), only skip continuation pages (ulong.MaxValue)
                            if (granule != ulong.MaxValue)
                            {
                                if (firstGranule == ulong.MaxValue) firstGranule = granule;

                                if (lastGranule > 0 || (lastGranule == 0 && firstGranule == 0))
                                {
                                    Assert.True(granule >= lastGranule,
                                        $"Track {i + 1} page {pageCount}: granule should increase (was {lastGranule}, now {granule})");
                                }
                                lastGranule = granule;
                            }

                            offset += 27 + segmentCount + dataSize;
                        }
                        else
                        {
                            offset++;
                        }
                    }

                    // Calculate duration from relative granules (48000 granules per second)
                    ulong relativeDuration = (firstGranule == ulong.MaxValue) ? lastGranule : (lastGranule - firstGranule);
                    double durationSeconds = relativeDuration / 48000.0;
                    Console.WriteLine($"  Pages: {pageCount}");
                    Console.WriteLine($"  First granule: {firstGranule}, Last granule: {lastGranule}");
                    Console.WriteLine($"  Relative duration: {relativeDuration} granules = {durationSeconds:F2} seconds");

                    // Validate durations are reasonable
                    if (i == 0)
                    {
                        // Track 1 should be ~10 seconds
                        Assert.True(durationSeconds >= 9.5 && durationSeconds <= 10.5,
                            $"Track 1 duration should be ~10s, got {durationSeconds:F2}s");
                    }
                    else if (i == 1)
                    {
                        // Track 2 should be ~15 seconds
                        Assert.True(durationSeconds >= 14.5 && durationSeconds <= 15.5,
                            $"Track 2 duration should be ~15s, got {durationSeconds:F2}s");
                    }
                    else if (i == 2)
                    {
                        // Track 3 should be ~8 seconds
                        Assert.True(durationSeconds >= 7.5 && durationSeconds <= 8.5,
                            $"Track 3 duration should be ~8s, got {durationSeconds:F2}s");
                    }

                    // Cleanup
                    File.Delete(trackFile);
                }

                Console.WriteLine("\n✓ All tracks validated successfully");
            }
            finally
            {
                if (File.Exists(initialTonieFile)) File.Delete(initialTonieFile);
                if (File.Exists(modifiedTonieFile)) File.Delete(modifiedTonieFile);
            }
        }

        [Fact]
        public void CreateMultipleTonies_WithoutExplicitAudioId_ShouldHaveUniqueAudioIdsAndHashes()
        {
            // This test verifies that:
            // 1. Each Tonie gets a unique Audio ID based on timestamp when not specified
            // 2. Different Audio IDs produce different hashes (stream serial = audio ID for hardware compatibility)
            // 3. Same Audio ID produces same hash (deterministic)

            // Arrange
            string track1 = Path.Combine(_testDataDir, "track1.mp3");

            string tonieFile1 = Path.Combine(Path.GetTempPath(), $"test_unique_id_1_{Guid.NewGuid()}.bin");
            string tonieFile2 = Path.Combine(Path.GetTempPath(), $"test_unique_id_2_{Guid.NewGuid()}.bin");
            string tonieFile3 = Path.Combine(Path.GetTempPath(), $"test_same_id_3_{Guid.NewGuid()}.bin");

            try
            {
                // Act: Create first tonie without specifying audioId (defaults to timestamp)
                Console.WriteLine("=== Creating first tonie (audioId=0, will use timestamp) ===");
                TonieFile.TonieAudio tonie1 = new TonieFile.TonieAudio(
                    new[] { track1 },
                    audioId: 0,  // Will use current timestamp
                    bitRate: 96000,
                    useVbr: false,
                    prefixLocation: null,
                    cbr: null
                );

                File.WriteAllBytes(tonieFile1, tonie1.FileContent);
                uint audioId1 = tonie1.Header.AudioId;
                string hash1 = BitConverter.ToString(tonie1.Header.Hash).Replace("-", "");
                Console.WriteLine($"First tonie Audio ID: 0x{audioId1:X8}");
                Console.WriteLine($"First tonie Hash: {hash1}");

                // Wait a moment to ensure different timestamps
                System.Threading.Thread.Sleep(1500);

                // Act: Create second tonie without specifying audioId
                Console.WriteLine("\n=== Creating second tonie (audioId=0, will use timestamp) ===");
                TonieFile.TonieAudio tonie2 = new TonieFile.TonieAudio(
                    new[] { track1 },
                    audioId: 0,  // Will use current timestamp
                    bitRate: 96000,
                    useVbr: false,
                    prefixLocation: null,
                    cbr: null
                );

                File.WriteAllBytes(tonieFile2, tonie2.FileContent);
                uint audioId2 = tonie2.Header.AudioId;
                string hash2 = BitConverter.ToString(tonie2.Header.Hash).Replace("-", "");
                Console.WriteLine($"Second tonie Audio ID: 0x{audioId2:X8}");
                Console.WriteLine($"Second tonie Hash: {hash2}");

                // Assert: Audio IDs should be different
                Assert.NotEqual(audioId1, audioId2);
                Console.WriteLine($"\n✓ Audio IDs are unique: 0x{audioId1:X8} != 0x{audioId2:X8}");

                // Assert: Audio IDs should be sequential (approximately 1-2 seconds apart)
                int timeDifference = (int)audioId2 - (int)audioId1;
                Console.WriteLine($"Time difference: {timeDifference} seconds");
                Assert.True(timeDifference >= 1 && timeDifference <= 3,
                    $"Audio IDs should be ~1-2 seconds apart, got {timeDifference} seconds");

                // Assert: Different audio IDs produce DIFFERENT hashes (stream serial = audio ID)
                Assert.NotEqual(hash1, hash2);
                Console.WriteLine($"✓ Different audio IDs produce different hashes (hardware compatibility)");
                Console.WriteLine($"  Hash1: {hash1}");
                Console.WriteLine($"  Hash2: {hash2}");

                // Act: Create third tonie with SAME audio ID as first tonie
                Console.WriteLine("\n=== Creating third tonie with explicit audio ID (same as first) ===");
                TonieFile.TonieAudio tonie3 = new TonieFile.TonieAudio(
                    new[] { track1 },
                    audioId: audioId1,  // Use same audio ID as first tonie
                    bitRate: 96000,
                    useVbr: false,
                    prefixLocation: null,
                    cbr: null
                );

                File.WriteAllBytes(tonieFile3, tonie3.FileContent);
                uint audioId3 = tonie3.Header.AudioId;
                string hash3 = BitConverter.ToString(tonie3.Header.Hash).Replace("-", "");
                Console.WriteLine($"Third tonie Audio ID: 0x{audioId3:X8}");
                Console.WriteLine($"Third tonie Hash: {hash3}");

                // Assert: Same audio ID produces SAME hash (deterministic)
                Assert.Equal(audioId1, audioId3);
                Assert.Equal(hash1, hash3);
                Console.WriteLine($"\n✓ Same audio + same audio ID = same hash (deterministic)");

                // Verify all files are valid
                var readBack1 = TonieFile.TonieAudio.FromFile(tonieFile1, readAudio: true);
                var readBack2 = TonieFile.TonieAudio.FromFile(tonieFile2, readAudio: true);
                var readBack3 = TonieFile.TonieAudio.FromFile(tonieFile3, readAudio: true);

                Assert.True(readBack1.HashCorrect, "First tonie should have correct hash");
                Assert.True(readBack2.HashCorrect, "Second tonie should have correct hash");
                Assert.True(readBack3.HashCorrect, "Third tonie should have correct hash");
                Assert.Equal(audioId1, readBack1.Header.AudioId);
                Assert.Equal(audioId2, readBack2.Header.AudioId);
                Assert.Equal(audioId3, readBack3.Header.AudioId);

                Console.WriteLine("\n✓ All tonies are valid with correct behavior:");
                Console.WriteLine("  - Different timestamps = different audio IDs = different hashes");
                Console.WriteLine("  - Same audio ID = same hash (works with hardware)");
            }
            finally
            {
                if (File.Exists(tonieFile1)) File.Delete(tonieFile1);
                if (File.Exists(tonieFile2)) File.Delete(tonieFile2);
                if (File.Exists(tonieFile3)) File.Delete(tonieFile3);
            }
        }

        [Fact]
        public void LosslessApproach_CreateCustomTonie_ThenAddTrack_ShouldPreserveAudioIdAndFolder()
        {
            // This test verifies the NEW LOSSLESS approach using CombineOggTracksLossless:
            // 1. Creates a custom tonie with specific audio ID and RFID UID in folder structure
            // 2. Verifies initial hash and audio ID
            // 3. Modifies by adding a third track using the lossless approach
            // 4. Verifies: different hash, same audio ID, same folder structure
            // 5. Verifies total duration = original duration + third track duration

            // Arrange
            string track1 = Path.Combine(_testDataDir, "track1.mp3");
            string track2 = Path.Combine(_testDataDir, "track2.mp3");
            string track3 = Path.Combine(_testDataDir, "track3.mp3");

            Assert.True(File.Exists(track1), $"Track1 not found: {track1}");
            Assert.True(File.Exists(track2), $"Track2 not found: {track2}");
            Assert.True(File.Exists(track3), $"Track3 not found: {track3}");

            // Setup: Create folder structure mimicking SD card with RFID UID
            uint audioId = 0xCAFEBABE;
            string rfidUid = "0451ED0E"; // Example RFID in reversed format
            string tempRoot = Path.Combine(Path.GetTempPath(), $"teddy_test_{Guid.NewGuid()}");
            string tonieFolder = Path.Combine(tempRoot, rfidUid, "500304E0");
            Directory.CreateDirectory(tonieFolder);

            string initialTonieFile = Path.Combine(tonieFolder, "500304E0");
            string modifiedTonieFile = Path.Combine(tonieFolder, "500304E0"); // Same file, will overwrite

            try
            {
                // ===== STEP 1: Create initial custom tonie with 2 tracks =====
                Console.WriteLine("=== STEP 1: Creating initial custom tonie with 2 tracks ===");
                Console.WriteLine($"Audio ID: 0x{audioId:X8}");
                Console.WriteLine($"RFID UID: {rfidUid}");
                Console.WriteLine($"Folder: {tonieFolder}");

                TonieFile.TonieAudio initialTonie = new TonieFile.TonieAudio(
                    new[] { track1, track2 },
                    audioId: audioId,
                    bitRate: 96000,
                    useVbr: false,
                    prefixLocation: null,
                    cbr: null
                );

                File.WriteAllBytes(initialTonieFile, initialTonie.FileContent);
                string initialHash = BitConverter.ToString(initialTonie.Header.Hash).Replace("-", "");

                Console.WriteLine($"Initial tonie created: {initialTonieFile}");
                Console.WriteLine($"Initial hash: {initialHash}");
                Console.WriteLine($"Initial audio ID: 0x{initialTonie.Header.AudioId:X8}");
                Console.WriteLine($"Initial chapters: {initialTonie.Header.AudioChapters.Length}");

                // Assert 1: Initial tonie should have correct properties
                Assert.Equal(2, initialTonie.Header.AudioChapters.Length);
                Assert.Equal(audioId, initialTonie.Header.AudioId);
                Assert.True(File.Exists(initialTonieFile));

                // Calculate initial duration
                var initialReadBack = TonieFile.TonieAudio.FromFile(initialTonieFile, readAudio: true);
                Assert.True(initialReadBack.HashCorrect, "Initial hash should be correct");

                ulong[] initialPositions = initialReadBack.ParsePositions();
                ulong initialDurationGranules = initialPositions[initialPositions.Length - 1];
                double initialDurationSeconds = initialDurationGranules / 48000.0;
                int initialMinutes = (int)(initialDurationSeconds / 60);
                int initialSeconds = (int)(initialDurationSeconds % 60);

                Console.WriteLine($"Initial duration: {initialDurationGranules} granules = {initialMinutes}:{initialSeconds:D2}");

                // ===== STEP 2: Modify tonie by adding third track using LOSSLESS approach =====
                Console.WriteLine("\n=== STEP 2: Modifying tonie by adding third track (LOSSLESS) ===");

                // Extract raw chapter data (no re-encoding!)
                List<byte[]> rawChapterData = initialReadBack.ExtractRawChapterData();
                Console.WriteLine($"Extracted {rawChapterData.Count} raw chapters (no decoding!)");

                // Update stream serial numbers to match audio ID (preserves exact encoding)
                // Note: ExtractRawChapterData() already normalizes granules to start at 0,
                // so we don't need to resetGranulePositions (would cause double-normalization)
                var track1Updated = new TonieFile.TonieAudio { Audio = rawChapterData[0] }.UpdateStreamSerialNumber(audioId, resetGranulePositions: false);
                var track2Updated = new TonieFile.TonieAudio { Audio = rawChapterData[1] }.UpdateStreamSerialNumber(audioId, resetGranulePositions: false);

                Console.WriteLine($"Track 1 updated: {track1Updated.Length} bytes (lossless)");
                Console.WriteLine($"Track 2 updated: {track2Updated.Length} bytes (lossless)");

                // Encode third track
                Console.WriteLine($"Encoding track 3 from: {track3}");
                TonieFile.TonieAudio track3Audio = new TonieFile.TonieAudio(
                    new[] { track3 },
                    audioId: audioId,
                    bitRate: 96000,
                    useVbr: false,
                    prefixLocation: null,
                    cbr: null
                );

                Console.WriteLine($"Track 3 encoded: {track3Audio.Audio.Length} bytes");

                // Combine all tracks LOSSLESSLY
                Console.WriteLine("\nCombining tracks using CombineOggTracksLossless...");
                var allTrackOggData = new List<byte[]> { track1Updated, track2Updated, track3Audio.Audio };
                var (combinedAudioData, combinedHash, chapterMarkers) = TonieFile.TonieAudio.CombineOggTracksLossless(allTrackOggData, audioId);

                Console.WriteLine($"Combined audio: {combinedAudioData.Length} bytes");
                Console.WriteLine($"Combined hash: {BitConverter.ToString(combinedHash).Replace("-", "")}");
                Console.WriteLine($"Chapter markers: [{string.Join(", ", chapterMarkers)}]");

                // Build final Tonie file with header
                byte[] modifiedFileContent = new byte[combinedAudioData.Length + 0x1000];
                Array.Copy(combinedAudioData, 0, modifiedFileContent, 0x1000, combinedAudioData.Length);

                var modifiedTonie = new TonieFile.TonieAudio();
                modifiedTonie.FileContent = modifiedFileContent;
                modifiedTonie.Audio = combinedAudioData;
                modifiedTonie.Header.Hash = combinedHash;
                modifiedTonie.Header.AudioLength = combinedAudioData.Length;
                modifiedTonie.Header.AudioId = audioId;
                modifiedTonie.Header.AudioChapters = chapterMarkers;
                modifiedTonie.Header.Padding = new byte[0];
                modifiedTonie.UpdateFileContent();

                File.WriteAllBytes(modifiedTonieFile, modifiedTonie.FileContent);
                string modifiedHash = BitConverter.ToString(combinedHash).Replace("-", "");

                Console.WriteLine($"\nModified tonie written: {modifiedTonieFile}");
                Console.WriteLine($"Modified hash: {modifiedHash}");
                Console.WriteLine($"Modified audio ID: 0x{modifiedTonie.Header.AudioId:X8}");
                Console.WriteLine($"Modified chapters: {modifiedTonie.Header.AudioChapters.Length}");

                // ===== STEP 3: Verify modified tonie properties =====
                Console.WriteLine("\n=== STEP 3: Verifying modified tonie ===");

                // Read back and validate
                var modifiedReadBack = TonieFile.TonieAudio.FromFile(modifiedTonieFile, readAudio: true);
                Assert.True(modifiedReadBack.HashCorrect, "Modified hash should be correct");

                // Assert: Different hash
                Assert.NotEqual(initialHash, modifiedHash);
                Console.WriteLine($"✓ Hash changed: {initialHash} -> {modifiedHash}");

                // Assert: Same audio ID
                Assert.Equal(audioId, modifiedReadBack.Header.AudioId);
                Console.WriteLine($"✓ Audio ID preserved: 0x{audioId:X8}");

                // Assert: Same folder structure
                Assert.True(File.Exists(modifiedTonieFile));
                Assert.Equal(tonieFolder, Path.GetDirectoryName(modifiedTonieFile));
                Console.WriteLine($"✓ Folder structure preserved: {tonieFolder}");

                // Assert: 3 chapters
                Assert.Equal(3, modifiedReadBack.Header.AudioChapters.Length);
                Console.WriteLine($"✓ Track count: 3 chapters");

                // Calculate modified duration
                ulong[] modifiedPositions = modifiedReadBack.ParsePositions();
                ulong modifiedDurationGranules = modifiedPositions[modifiedPositions.Length - 1];
                double modifiedDurationSeconds = modifiedDurationGranules / 48000.0;
                int modifiedMinutes = (int)(modifiedDurationSeconds / 60);
                int modifiedSeconds = (int)(modifiedDurationSeconds % 60);

                Console.WriteLine($"\nInitial duration:  {initialMinutes}:{initialSeconds:D2}");
                Console.WriteLine($"Modified duration: {modifiedMinutes}:{modifiedSeconds:D2}");

                // Get track 3 duration from the source MP3 file using ffprobe
                double track3DurationSeconds = GetAudioDurationFromFile(track3);

                // Assert: Modified duration should be initial + track3
                double expectedDurationSeconds = initialDurationSeconds + track3DurationSeconds;
                int expectedMinutes = (int)(expectedDurationSeconds / 60);
                int expectedSeconds = (int)(expectedDurationSeconds % 60);

                Console.WriteLine($"Track 3 duration:  {(int)(track3DurationSeconds / 60)}:{(int)(track3DurationSeconds % 60):D2}");
                Console.WriteLine($"Expected total:    {expectedMinutes}:{expectedSeconds:D2}");

                // Allow 2 second tolerance for encoding variations
                Assert.True(Math.Abs(modifiedDurationSeconds - expectedDurationSeconds) <= 2.0,
                    $"Modified duration ({modifiedMinutes}:{modifiedSeconds:D2}) should be approximately initial ({initialMinutes}:{initialSeconds:D2}) + track3 ({(int)(track3DurationSeconds / 60)}:{(int)(track3DurationSeconds % 60):D2}) = {expectedMinutes}:{expectedSeconds:D2}");

                Console.WriteLine($"✓ Duration correct: {modifiedMinutes}:{modifiedSeconds:D2} ≈ {expectedMinutes}:{expectedSeconds:D2}");

                // Validate Ogg structure
                modifiedReadBack.CalculateStatistics(
                    out long totalSegments,
                    out long segLength,
                    out int minSegs,
                    out int maxSegs,
                    out ulong minGranule,
                    out ulong maxGranule,
                    out ulong highestGranule
                );

                Console.WriteLine($"✓ Ogg structure valid: {totalSegments} segments, {highestGranule} granules");

                Console.WriteLine("\n✓✓✓ ALL TESTS PASSED - Lossless approach works perfectly! ✓✓✓");
                Console.WriteLine("    - Original tracks preserved without re-encoding");
                Console.WriteLine("    - Hash changed (as expected when content changes)");
                Console.WriteLine("    - Audio ID preserved");
                Console.WriteLine("    - Folder structure preserved");
                Console.WriteLine("    - Duration correct (original + new track)");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        /// <summary>
        /// Gets the duration of an audio file in seconds using ffprobe.
        /// </summary>
        private double GetAudioDurationFromFile(string audioFilePath)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ffprobe",
                        Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{audioFilePath}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double duration))
                {
                    return duration;
                }

                throw new Exception($"Failed to parse duration from ffprobe output: {output}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get audio duration for {audioFilePath}: {ex.Message}", ex);
            }
        }
    }
}

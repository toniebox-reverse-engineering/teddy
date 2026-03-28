using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TeddyBench.Avalonia.Models;
using TeddyBench.Avalonia.Services;
using TonieFile;
using Xunit;

namespace TeddyBench.Avalonia.Tests;

/// <summary>
/// Tests to verify that metadata (Audio IDs, tracks) are properly stored
/// in customTonies.json when new custom tonies are discovered or when
/// TonieTrackInfoService is used to populate track information on-demand.
/// </summary>
public class MetadataPersistenceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _contentDir;
    private readonly string _customTonieJsonPath;

    public MetadataPersistenceTests()
    {
        // Set up test directory structure
        _testDir = Path.Combine(Path.GetTempPath(), $"TeddyBench_Metadata_Test_{Guid.NewGuid():N}");
        _contentDir = Path.Combine(_testDir, "CONTENT");
        Directory.CreateDirectory(_contentDir);

        _customTonieJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "customTonies.json");

        // Clean up any existing customTonies.json to start fresh
        if (File.Exists(_customTonieJsonPath))
        {
            File.Delete(_customTonieJsonPath);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task CustomTonie_WhenScanned_ShouldStoreAudioIdButNotTracks()
    {
        // Arrange: Create a custom tonie file with a known Audio ID
        var tonieDir = Path.Combine(_contentDir, "4242ED0E");
        Directory.CreateDirectory(tonieDir);
        var tonieFilePath = Path.Combine(tonieDir, "500304E0");

        var testAudioId = 0x19235099u; // Known test Audio ID
        var track1Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "track1.mp3");

        var tonie = new TonieAudio(
            sources: new[] { track1Path },
            audioId: testAudioId,
            bitRate: 96000,
            useVbr: false,
            prefixLocation: null
        );

        File.WriteAllBytes(tonieFilePath, tonie.FileContent);

        // Get the hash for verification
        var expectedHash = BitConverter.ToString(tonie.Header.Hash).Replace("-", "");

        // Act: Scan the directory (only reads headers, not full audio)
        var metadataService = new TonieMetadataService();
        var liveFlagService = new LiveFlagService();
        var scanService = new DirectoryScanService(metadataService, liveFlagService);

        var files = await scanService.ScanDirectoryAsync(_contentDir);

        // Assert: Verify file was found
        Assert.NotEmpty(files);
        var foundFile = files.FirstOrDefault(f => f.DirectoryName == "4242ED0E");
        Assert.NotNull(foundFile);
        Assert.True(foundFile.IsCustomTonie);

        // Assert: Verify customTonies.json was created
        Assert.True(File.Exists(_customTonieJsonPath), "customTonies.json should be created");

        // Assert: Verify customTonies.json contains the Audio ID
        var jsonContent = File.ReadAllText(_customTonieJsonPath);
        var customTonies = JsonConvert.DeserializeObject<System.Collections.Generic.List<TonieMetadata>>(jsonContent);

        Assert.NotNull(customTonies);
        Assert.NotEmpty(customTonies);

        var customTonie = customTonies.FirstOrDefault(t =>
            t.Hash != null && t.Hash.Any(h => h.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)));

        Assert.NotNull(customTonie);
        Assert.NotNull(customTonie.AudioId);
        Assert.NotEmpty(customTonie.AudioId);
        Assert.Equal(testAudioId.ToString(), customTonie.AudioId[0]);

        // Verify tracks are NOT populated during scanning (for performance)
        Assert.NotNull(customTonie.Tracks);
        Assert.Empty(customTonie.Tracks); // Should be empty after scanning

        Console.WriteLine($"✓ Audio ID {testAudioId} (0x{testAudioId:X8}) successfully stored in customTonies.json");
        Console.WriteLine($"✓ Tracks NOT populated during scan (on-demand only)");

        // Act: Now use TonieTrackInfoService to populate tracks on-demand
        var trackInfoService = new TonieTrackInfoService(metadataService);
        var tracks = trackInfoService.EnsureTrackInfo(tonieFilePath, expectedHash);

        // Assert: Verify tracks are now populated
        Assert.NotEmpty(tracks);
        Assert.Single(tracks); // Single track file
        Assert.Contains("Track 01 -", tracks[0]);

        // Assert: Verify tracks were saved to customTonies.json
        jsonContent = File.ReadAllText(_customTonieJsonPath);
        customTonies = JsonConvert.DeserializeObject<System.Collections.Generic.List<TonieMetadata>>(jsonContent);
        customTonie = customTonies.FirstOrDefault(t =>
            t.Hash != null && t.Hash.Any(h => h.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)));

        Assert.NotNull(customTonie);
        Assert.NotNull(customTonie.Tracks);
        Assert.NotEmpty(customTonie.Tracks);
        Assert.Contains("Track 01 -", customTonie.Tracks[0]);

        Console.WriteLine($"✓ Tracks populated on-demand: {string.Join(", ", customTonie.Tracks)}");
    }

    [Fact]
    public async Task MultipleTonies_WithDifferentAudioIds_ShouldStoreAllAudioIds()
    {
        // Arrange: Create two custom tonies with different Audio IDs
        var tonie1Dir = Path.Combine(_contentDir, "4242ED0E");
        var tonie2Dir = Path.Combine(_contentDir, "4343ED0E");
        Directory.CreateDirectory(tonie1Dir);
        Directory.CreateDirectory(tonie2Dir);

        var audioId1 = 0x19235099u;
        var audioId2 = 0x19235070u;

        var track1Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "track1.mp3");

        // Create first tonie
        var tonie1 = new TonieAudio(
            sources: new[] { track1Path },
            audioId: audioId1,
            bitRate: 96000,
            useVbr: false,
            prefixLocation: null
        );
        File.WriteAllBytes(Path.Combine(tonie1Dir, "500304E0"), tonie1.FileContent);
        var hash1 = BitConverter.ToString(tonie1.Header.Hash).Replace("-", "");

        // Create second tonie (same audio, different Audio ID)
        var tonie2 = new TonieAudio(
            sources: new[] { track1Path },
            audioId: audioId2,
            bitRate: 96000,
            useVbr: false,
            prefixLocation: null
        );
        File.WriteAllBytes(Path.Combine(tonie2Dir, "500304E0"), tonie2.FileContent);
        var hash2 = BitConverter.ToString(tonie2.Header.Hash).Replace("-", "");

        // Act: Scan the directory
        var metadataService = new TonieMetadataService();
        var liveFlagService = new LiveFlagService();
        var scanService = new DirectoryScanService(metadataService, liveFlagService);

        var files = await scanService.ScanDirectoryAsync(_contentDir);

        // Assert: Both files found
        Assert.Equal(2, files.Count);

        // Assert: customTonies.json contains both entries with correct Audio IDs
        var jsonContent = File.ReadAllText(_customTonieJsonPath);
        var customTonies = JsonConvert.DeserializeObject<System.Collections.Generic.List<TonieMetadata>>(jsonContent);

        Assert.NotNull(customTonies);
        // Note: With different audio IDs, we get different hashes (due to Ogg stream serial number)
        // So we expect at least 2 entries (one for each unique hash)
        Assert.True(customTonies.Count >= 2, $"Expected at least 2 entries, got {customTonies.Count}");

        // Verify first tonie - Audio ID stored, but tracks NOT populated during scan
        var customTonie1 = customTonies.FirstOrDefault(t =>
            t.Hash != null && t.Hash.Any(h => h.Equals(hash1, StringComparison.OrdinalIgnoreCase)));
        Assert.NotNull(customTonie1);
        Assert.NotNull(customTonie1.AudioId);
        Assert.NotEmpty(customTonie1.AudioId);
        Assert.Equal(audioId1.ToString(), customTonie1.AudioId[0]);
        Assert.NotNull(customTonie1.Tracks);
        Assert.Empty(customTonie1.Tracks); // Should be empty after scanning

        // Verify second tonie - Audio ID stored, but tracks NOT populated during scan
        var customTonie2 = customTonies.FirstOrDefault(t =>
            t.Hash != null && t.Hash.Any(h => h.Equals(hash2, StringComparison.OrdinalIgnoreCase)));
        Assert.NotNull(customTonie2);
        Assert.NotNull(customTonie2.AudioId);
        Assert.NotEmpty(customTonie2.AudioId);
        Assert.Equal(audioId2.ToString(), customTonie2.AudioId[0]);
        Assert.NotNull(customTonie2.Tracks);
        Assert.Empty(customTonie2.Tracks); // Should be empty after scanning

        Console.WriteLine($"✓ Both Audio IDs stored correctly:");
        Console.WriteLine($"  Tonie 1: {audioId1} (0x{audioId1:X8})");
        Console.WriteLine($"  Tonie 2: {audioId2} (0x{audioId2:X8})");
        Console.WriteLine($"✓ Different hashes verified:");
        Console.WriteLine($"  Hash 1: {hash1}");
        Console.WriteLine($"  Hash 2: {hash2}");
        Console.WriteLine($"✓ Tracks NOT populated during scan (on-demand only)");
    }

    [Fact]
    public async Task OldFormatEntry_WhenRescanned_ShouldGetUpdatedWithAudioId()
    {
        // Arrange: Create a tonie file
        var tonieDir = Path.Combine(_contentDir, "5151ED0E");
        Directory.CreateDirectory(tonieDir);
        var tonieFilePath = Path.Combine(tonieDir, "500304E0");

        var testAudioId = 0x12345678u;
        var track1Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "track1.mp3");

        var tonie = new TonieAudio(
            sources: new[] { track1Path },
            audioId: testAudioId,
            bitRate: 96000,
            useVbr: false,
            prefixLocation: null
        );

        File.WriteAllBytes(tonieFilePath, tonie.FileContent);
        var expectedHash = BitConverter.ToString(tonie.Header.Hash).Replace("-", "");

        // Create an OLD format entry manually (missing audio_id and tracks)
        var metadataService = new TonieMetadataService();
        var oldEntry = new TonieMetadata
        {
            No = "0",
            Hash = new List<string> { expectedHash },
            Title = "Old Format Tonie [RFID: 0EED5151]",
            Series = "Custom Tonie",
            Episodes = "Old Format Tonie [RFID: 0EED5151]",
            Category = "custom",
            Language = "en-us",
            AudioId = new List<string>(),  // Empty!
            Tracks = new List<string>()     // Empty!
        };

        // Save the old format entry
        var customToniesList = new System.Collections.Generic.List<TonieMetadata> { oldEntry };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(customToniesList, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(_customTonieJsonPath, json);

        // Act: Scan the directory (should only add Audio ID, not tracks)
        var liveFlagService = new LiveFlagService();
        var scanService = new DirectoryScanService(metadataService, liveFlagService);
        await scanService.ScanDirectoryAsync(_contentDir);

        // Assert: Verify the entry was updated with Audio ID
        var jsonContent = File.ReadAllText(_customTonieJsonPath);
        var updatedTonies = JsonConvert.DeserializeObject<System.Collections.Generic.List<TonieMetadata>>(jsonContent);

        Assert.NotNull(updatedTonies);
        Assert.Single(updatedTonies);

        var updatedTonie = updatedTonies[0];

        // Verify audio_id was added
        Assert.NotNull(updatedTonie.AudioId);
        Assert.NotEmpty(updatedTonie.AudioId);
        Assert.Equal(testAudioId.ToString(), updatedTonie.AudioId[0]);

        // Verify tracks are still empty (not populated during scanning)
        Assert.NotNull(updatedTonie.Tracks);
        Assert.Empty(updatedTonie.Tracks);

        Console.WriteLine($"✓ Old format entry successfully updated:");
        Console.WriteLine($"  Audio ID: {updatedTonie.AudioId[0]} (0x{testAudioId:X8})");
        Console.WriteLine($"  Tracks: Still empty (populated on-demand only)");
    }
}

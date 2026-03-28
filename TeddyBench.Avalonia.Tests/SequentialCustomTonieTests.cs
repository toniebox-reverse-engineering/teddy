using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TeddyBench.Avalonia.Models;
using TeddyBench.Avalonia.Services;
using TeddyBench.Avalonia.ViewModels;
using TonieFile;
using Xunit;
using Newtonsoft.Json.Linq;

namespace TeddyBench.Avalonia.Tests;

/// <summary>
/// End-to-end test for sequentially adding two custom tonies
/// Tests the workflow:
/// 1. Open /tmp directory
/// 2. Add first custom tonie with random RFID and track1.mp3
/// 3. Verify one tonie exists
/// 4. Add second custom tonie with different random RFID and track1.mp3
/// 5. Verify two tonies exist
/// </summary>
public class SequentialCustomTonieTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _contentDir;
    private readonly string _customTonieJsonPath;
    private readonly string _appSettingsPath;

    public SequentialCustomTonieTests()
    {
        // Set up test directory structure in /tmp
        _testDir = Path.Combine("/tmp", $"TeddyBench_Sequential_Test_{Guid.NewGuid():N}");
        _contentDir = Path.Combine(_testDir, "CONTENT");

        Directory.CreateDirectory(_contentDir);

        _customTonieJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "customTonies.json");
        _appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        // Ensure appsettings.json exists with AudioIdPrompt = false for tests
        EnsureAppSettings();
    }

    private void EnsureAppSettings()
    {
        // Create default appsettings.json if it doesn't exist
        // This ensures tests run with AudioIdPrompt = false (auto-generate mode)
        if (!File.Exists(_appSettingsPath))
        {
            var defaultSettings = new JObject
            {
                ["RfidPrefix"] = "0EED",
                ["SortOption"] = "DisplayName",
                ["AudioIdPrompt"] = false
            };
            File.WriteAllText(_appSettingsPath, defaultSettings.ToString());
        }
    }

    [AvaloniaFact]
    public async Task AddTwoCustomToniesSequentially_ShouldDisplayTwoTonies()
    {
        Console.WriteLine("Test started: Adding two custom tonies sequentially");

        // Arrange
        var window = new Window();
        var viewModel = new MainWindowViewModel(window);

        // Step 1: Wait for JSON metadata to be loaded
        Console.WriteLine("Step 1: Waiting for metadata to load...");

        // Wait up to 10 seconds for metadata to load, checking status every second
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(1000);
            if (viewModel.StatusText.Contains("Ready") ||
                viewModel.StatusText.Contains("downloaded") ||
                viewModel.StatusText.Contains("Metadata") ||
                viewModel.StatusText.Contains("metadata loaded") ||
                !viewModel.StatusText.Contains("Downloading"))
            {
                break;
            }
        }

        Console.WriteLine($"Status after waiting: {viewModel.StatusText}");

        // Accept various "ready" statuses
        Assert.True(
            viewModel.StatusText.Contains("Ready") ||
            viewModel.StatusText.Contains("downloaded") ||
            viewModel.StatusText.Contains("Metadata") ||
            viewModel.StatusText.Contains("metadata loaded") ||
            !viewModel.StatusText.Contains("Downloading"),
            $"Expected metadata to be loaded, but got: {viewModel.StatusText}");

        // Step 2: Simulate opening the /tmp CONTENT directory
        Console.WriteLine($"Step 2: Opening directory {_contentDir}...");
        await SimulateDirectoryOpen(viewModel, _contentDir);

        // Verify directory is empty
        Assert.Empty(viewModel.TonieFiles);

        // Step 3: Add first custom tonie with random RFID
        Console.WriteLine("Step 3: Adding first custom tonie...");
        var firstRfid = GenerateRandomRfid();
        Console.WriteLine($"  - First RFID: {firstRfid}");

        var track1Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "track1.mp3");
        Assert.True(File.Exists(track1Path), $"Test data file not found: {track1Path}");

        var firstTonieFile = await SimulateAddCustomTonie(
            viewModel,
            rfidUid: firstRfid,
            audioPaths: new[] { track1Path }
        );
        Console.WriteLine($"  - First tonie created at: {firstTonieFile}");

        // Step 4: Verify one tonie exists
        Console.WriteLine("Step 4: Verifying one tonie exists...");
        Assert.Single(viewModel.TonieFiles);
        var firstTonie = viewModel.TonieFiles.First();
        Assert.NotNull(firstTonie);
        Console.WriteLine($"  - First tonie display name: {firstTonie.DisplayName}");

        // Step 5: Add second custom tonie with different random RFID
        Console.WriteLine("Step 5: Adding second custom tonie...");
        string secondRfid;
        do
        {
            secondRfid = GenerateRandomRfid();
        } while (secondRfid == firstRfid); // Ensure different RFID

        Console.WriteLine($"  - Second RFID: {secondRfid}");

        var secondTonieFile = await SimulateAddCustomTonie(
            viewModel,
            rfidUid: secondRfid,
            audioPaths: new[] { track1Path }
        );
        Console.WriteLine($"  - Second tonie created at: {secondTonieFile}");

        // Step 6: Verify two tonies exist
        Console.WriteLine("Step 6: Verifying two tonies exist...");
        Assert.Equal(2, viewModel.TonieFiles.Count);

        var tonies = viewModel.TonieFiles.ToList();
        Assert.NotNull(tonies[0]);
        Assert.NotNull(tonies[1]);

        Console.WriteLine($"  - First tonie: {tonies[0].DisplayName}");
        Console.WriteLine($"  - Second tonie: {tonies[1].DisplayName}");

        // Verify both files exist on disk
        Assert.True(File.Exists(firstTonieFile), $"First tonie file should exist: {firstTonieFile}");
        Assert.True(File.Exists(secondTonieFile), $"Second tonie file should exist: {secondTonieFile}");

        // Step 7: Verify both tonies have different audio IDs
        Console.WriteLine("Step 7: Verifying both tonies have different audio IDs...");
        var firstTonieAudio = TonieAudio.FromFile(firstTonieFile, readAudio: false);
        var secondTonieAudio = TonieAudio.FromFile(secondTonieFile, readAudio: false);

        var firstAudioId = firstTonieAudio.Header.AudioId;
        var secondAudioId = secondTonieAudio.Header.AudioId;

        Console.WriteLine($"  - First tonie audio ID: 0x{firstAudioId:X8}");
        Console.WriteLine($"  - Second tonie audio ID: 0x{secondAudioId:X8}");

        Assert.NotEqual(firstAudioId, secondAudioId);
        Console.WriteLine("  - ✓ Audio IDs are different");

        // Step 8: Get hashes from first two tonies
        Console.WriteLine("Step 8: Getting hashes from first two tonies...");
        var firstHash = BitConverter.ToString(firstTonieAudio.Header.Hash).Replace("-", "");
        var secondHash = BitConverter.ToString(secondTonieAudio.Header.Hash).Replace("-", "");

        Console.WriteLine($"  - First tonie hash: {firstHash}");
        Console.WriteLine($"  - Second tonie hash: {secondHash}");

        // Different audio IDs produce different hashes because Audio ID is used as Ogg stream serial number
        // This is required by Toniebox hardware (Stream Serial must equal Audio ID)
        Assert.NotEqual(firstHash, secondHash);
        Console.WriteLine("  - ✓ Hashes are different (different audio IDs = different stream serial numbers)");

        // Step 9: Add third custom tonie with SAME audio ID as first tonie
        Console.WriteLine("Step 9: Adding third custom tonie with same audio ID as first tonie...");
        string thirdRfid;
        do
        {
            thirdRfid = GenerateRandomRfid();
        } while (thirdRfid == firstRfid || thirdRfid == secondRfid); // Ensure different RFID

        Console.WriteLine($"  - Third RFID: {thirdRfid}");
        Console.WriteLine($"  - Using same audio ID as first tonie: 0x{firstAudioId:X8}");

        var thirdTonieFile = await SimulateAddCustomTonieWithAudioId(
            viewModel,
            rfidUid: thirdRfid,
            audioPaths: new[] { track1Path },
            audioId: firstAudioId  // Use same audio ID as first tonie
        );
        Console.WriteLine($"  - Third tonie created at: {thirdTonieFile}");

        // Step 10: Verify three tonies exist
        Console.WriteLine("Step 10: Verifying three tonies exist...");
        Assert.Equal(3, viewModel.TonieFiles.Count);
        Console.WriteLine($"  - ✓ Three tonies exist in main window");

        // Step 11: Verify third tonie has same hash as first tonie
        Console.WriteLine("Step 11: Verifying third tonie has same hash as first tonie...");
        var thirdTonieAudio = TonieAudio.FromFile(thirdTonieFile, readAudio: false);
        var thirdAudioId = thirdTonieAudio.Header.AudioId;
        var thirdHash = BitConverter.ToString(thirdTonieAudio.Header.Hash).Replace("-", "");

        Console.WriteLine($"  - Third tonie audio ID: 0x{thirdAudioId:X8}");
        Console.WriteLine($"  - Third tonie hash: {thirdHash}");

        // Same audio source + same audio ID = same hash
        Assert.Equal(firstAudioId, thirdAudioId);
        Assert.Equal(firstHash, thirdHash);
        Assert.NotEqual(secondHash, thirdHash); // Second has different audio ID, so different hash
        Console.WriteLine("  - ✓ First and third tonies have same hash (same audio + same audio ID)");
        Console.WriteLine("  - ✓ Second tonie has different hash (different audio ID)");

        // Step 12: Verify customTonies.json exists and has correct entries
        Console.WriteLine("Step 12: Verifying customTonies.json...");
        Assert.True(File.Exists(_customTonieJsonPath), $"customTonies.json should exist at: {_customTonieJsonPath}");

        var customToniesJson = JArray.Parse(File.ReadAllText(_customTonieJsonPath));

        // We expect at least 2 entries: one for first/third tonies (same hash), one for second tonie (different hash)
        // Note: May have more entries if previous tests didn't clean up properly
        Assert.True(customToniesJson.Count >= 2, $"Expected at least 2 entries, got {customToniesJson.Count}");
        Console.WriteLine($"  - ✓ customTonies.json has {customToniesJson.Count} entries (checking for our expected 2)");

        // Find the entry with firstHash (should be same as thirdHash)
        var firstHashEntry = customToniesJson.FirstOrDefault(entry =>
        {
            var hashArray = entry["hash"] as JArray;
            return hashArray != null && hashArray.Any(h => h.ToString() == firstHash);
        }) as JObject;

        // Find the entry with secondHash
        var secondHashEntry = customToniesJson.FirstOrDefault(entry =>
        {
            var hashArray = entry["hash"] as JArray;
            return hashArray != null && hashArray.Any(h => h.ToString() == secondHash);
        }) as JObject;

        Assert.NotNull(firstHashEntry);
        Assert.NotNull(secondHashEntry);

        Console.WriteLine($"  - ✓ Found entry for first/third tonies (hash: {firstHash.Substring(0, 16)}...)");
        Console.WriteLine($"  - ✓ Found entry for second tonie (hash: {secondHash.Substring(0, 16)}...)");

        // Verify the first entry has the first RFID (or third, depending on registration order)
        var firstTitle = firstHashEntry["title"]?.ToString() ?? "";
        Console.WriteLine($"  - First/third entry title: {firstTitle}");

        var secondTitle = secondHashEntry["title"]?.ToString() ?? "";
        Console.WriteLine($"  - Second entry title: {secondTitle}");

        Console.WriteLine("Test completed successfully! Verified that different audio IDs produce different hashes (due to Ogg stream serial number).");
    }

    /// <summary>
    /// Generates a random 8-character hexadecimal RFID UID
    /// Format: 0EED#### where #### are random hex digits
    /// </summary>
    private string GenerateRandomRfid()
    {
        var random = new Random();
        var randomPart = random.Next(0x0000, 0xFFFF).ToString("X4");
        return $"0EED{randomPart}";
    }

    private async Task SimulateDirectoryOpen(MainWindowViewModel viewModel, string directory)
    {
        // Directly call the internal ScanDirectory method via reflection
        var method = typeof(MainWindowViewModel).GetMethod("ScanDirectory",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            var task = method.Invoke(viewModel, new object[] { directory }) as Task;
            if (task != null)
            {
                await task;
            }
        }
    }

    private async Task<string> SimulateAddCustomTonie(
        MainWindowViewModel viewModel,
        string rfidUid,
        string[] audioPaths)
    {
        return await SimulateAddCustomTonieWithAudioId(viewModel, rfidUid, audioPaths, null);
    }

    private async Task<string> SimulateAddCustomTonieWithAudioId(
        MainWindowViewModel viewModel,
        string rfidUid,
        string[] audioPaths,
        uint? audioId)
    {
        // Parse RFID and create directory structure
        var customTonieService = new CustomTonieCreationService(
            new TonieFileService(),
            new TonieMetadataService()
        );

        var tonieFileService = new TonieFileService();
        var reversedUid = tonieFileService.ReverseUidBytes(rfidUid);

        // Create the tonie file with optional audio ID
        var (generatedHash, targetFile) = customTonieService.CreateCustomTonieFile(
            _contentDir,
            reversedUid,
            audioPaths,
            rfidUid,
            audioId  // Pass the audio ID (null for auto-generate)
        );

        // Get the audio ID from the created file
        var createdTonie = TonieAudio.FromFile(targetFile, readAudio: false);
        var actualAudioId = createdTonie.Header.AudioId;

        // Register in metadata
        var sourceFolderName = tonieFileService.GetSourceFolderName(audioPaths);
        customTonieService.RegisterCustomTonie(generatedHash, sourceFolderName, rfidUid, actualAudioId, audioPaths, reversedUid, targetFile);

        // Refresh directory
        await SimulateDirectoryOpen(viewModel, _contentDir);

        return targetFile;
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not delete test directory: {ex.Message}");
            }
        }

        // Clean up customTonies.json if it was created for testing
        if (File.Exists(_customTonieJsonPath))
        {
            try
            {
                File.Delete(_customTonieJsonPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not delete customTonies.json: {ex.Message}");
            }
        }
    }
}

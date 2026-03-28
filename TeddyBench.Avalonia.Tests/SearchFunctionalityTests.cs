using System;
using System.Diagnostics;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TeddyBench.Avalonia.Tests;

/// <summary>
/// End-to-end test for search functionality
/// Tests the complete search workflow:
/// 1. Create temp directory with 3 custom tonies
/// 2. Open directory in the application
/// 3. Search for "ahm" and verify only Ahmed tonie is shown
/// 4. Clear search with ESC and verify all 3 tonies are shown again
/// </summary>
public class SearchFunctionalityTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _contentDir;
    private readonly string _customTonieJsonPath;
    private readonly string _appSettingsPath;

    public SearchFunctionalityTests()
    {
        // Set up test directory structure
        _testDir = Path.Combine(Path.GetTempPath(), $"TeddyBench_Search_Test_{Guid.NewGuid():N}");
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
    public async Task SearchWorkflow_FilterAndClearSearch_ShouldShowCorrectTonies()
    {
        var testSw = Stopwatch.StartNew();
        Console.WriteLine("[SEARCH TEST] Starting search functionality test");

        // Arrange
        var window = new Window();
        var viewModel = new MainWindowViewModel(window);

        // Step 1: Wait for JSON metadata to be loaded
        Console.WriteLine("[SEARCH TEST] Step 1: Waiting for metadata to load...");
        await Task.Delay(2000); // Give time for InitializeMetadataAsync to complete
        Console.WriteLine($"[SEARCH TEST] Status after waiting: {viewModel.StatusText}");

        // Step 2: Simulate opening the CONTENT directory
        Console.WriteLine("[SEARCH TEST] Step 2: Scanning directory...");
        await SimulateDirectoryOpen(viewModel, _contentDir);

        // Verify directory is empty initially
        Assert.Empty(viewModel.TonieFiles);

        // Step 3: Create three custom tonies with specific titles
        Console.WriteLine("[SEARCH TEST] Step 3: Creating three custom tonies...");
        var track1Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "track1.mp3");
        var track2Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "track2.mp3");
        var track3Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "track3.mp3");

        // Create Ahmed tonie with random UID
        Console.WriteLine("[SEARCH TEST] Creating Ahmed tonie...");
        CreateCustomTonieWithTitle(viewModel, "Ahmed", "0EED1111", track1Path, customAudioId: null);

        // Create Bertan tonie with random UID
        Console.WriteLine("[SEARCH TEST] Creating Bertan tonie...");
        CreateCustomTonieWithTitle(viewModel, "Bertan", "0EED2222", track2Path, customAudioId: null);

        // Create Can tonie with custom audio ID 0x12345678 for audio ID search test
        Console.WriteLine("[SEARCH TEST] Creating Can tonie with custom Audio ID 0x12345678...");
        CreateCustomTonieWithTitle(viewModel, "Can", "0EED3333", track3Path, customAudioId: 0x12345678);

        // Create a new ViewModel to ensure metadata is reloaded from customTonies.json
        Console.WriteLine("[SEARCH TEST] Step 4: Creating new ViewModel to reload metadata...");
        viewModel = new MainWindowViewModel(window);
        await Task.Delay(100); // Give time for initialization

        // Refresh directory to load all tonies
        Console.WriteLine("[SEARCH TEST] Step 5: Scanning directory...");
        await SimulateDirectoryOpen(viewModel, _contentDir);

        // Verify all 3 tonies are loaded
        Console.WriteLine($"[SEARCH TEST] Loaded {viewModel.TonieFiles.Count} tonies");
        Assert.Equal(3, viewModel.TonieFiles.Count);

        // Verify tonie names
        var tonieNames = viewModel.TonieFiles.Select(t => t.DisplayName).ToList();
        Console.WriteLine($"[SEARCH TEST] Tonie names: {string.Join(", ", tonieNames)}");

        // Check that all three tonies are present (ignoring [RFID: ...] suffix)
        Assert.Contains(viewModel.TonieFiles, t => t.DisplayName.Contains("Ahmed"));
        Assert.Contains(viewModel.TonieFiles, t => t.DisplayName.Contains("Bertan"));
        Assert.Contains(viewModel.TonieFiles, t => t.DisplayName.Contains("Can"));

        // Step 6: Simulate typing "ahm" to search for Ahmed
        Console.WriteLine("[SEARCH TEST] Step 6: Searching for 'ahm'...");
        viewModel.HandleSearchInput("ahm");

        // Wait for debounce (250ms) + a bit extra for processing
        Console.WriteLine("[SEARCH TEST] Waiting for search debounce and filtering...");
        await Task.Delay(500);

        // Verify only Ahmed tonie is shown
        Console.WriteLine($"[SEARCH TEST] After search: {viewModel.TonieFiles.Count} tonie(s) shown");
        Console.WriteLine($"[SEARCH TEST] Search active: {viewModel.IsSearchActive}");
        Console.WriteLine($"[SEARCH TEST] Search text: '{viewModel.SearchText}'");

        Assert.True(viewModel.IsSearchActive, "Search should be active");
        Assert.Equal("ahm", viewModel.SearchText);
        Assert.Single(viewModel.TonieFiles);

        var filteredTonie = viewModel.TonieFiles.First();
        Console.WriteLine($"[SEARCH TEST] Filtered tonie: {filteredTonie.DisplayName}");
        Assert.Contains("Ahmed", filteredTonie.DisplayName);

        // Step 7: Clear search by pressing ESC (simulated via ClearSearch command)
        Console.WriteLine("[SEARCH TEST] Step 7: Clearing search (simulating ESC key)...");
        viewModel.ClearSearchCommand.Execute(null);

        // Wait a bit for the filter to be cleared
        await Task.Delay(500);

        // Verify all 3 tonies are shown again
        Console.WriteLine($"[SEARCH TEST] After clearing search: {viewModel.TonieFiles.Count} tonie(s) shown");
        Console.WriteLine($"[SEARCH TEST] Search active: {viewModel.IsSearchActive}");
        Console.WriteLine($"[SEARCH TEST] Search text: '{viewModel.SearchText}'");

        Assert.False(viewModel.IsSearchActive, "Search should not be active after clearing");
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(3, viewModel.TonieFiles.Count);

        // Verify all tonies are back
        tonieNames = viewModel.TonieFiles.Select(t => t.DisplayName).ToList();
        Console.WriteLine($"[SEARCH TEST] Tonie names after clear: {string.Join(", ", tonieNames)}");
        Assert.Contains(viewModel.TonieFiles, t => t.DisplayName.Contains("Ahmed"));
        Assert.Contains(viewModel.TonieFiles, t => t.DisplayName.Contains("Bertan"));
        Assert.Contains(viewModel.TonieFiles, t => t.DisplayName.Contains("Can"));

        // Step 8: Search for "22" and verify only Bertan is shown
        Console.WriteLine("[SEARCH TEST] Step 8: Searching for '22' (should match Bertan RFID)...");
        viewModel.HandleSearchInput("22");

        // Wait for debounce (250ms) + a bit extra for processing
        Console.WriteLine("[SEARCH TEST] Waiting for search debounce and filtering...");
        await Task.Delay(500);

        // Verify only Bertan tonie is shown
        Console.WriteLine($"[SEARCH TEST] After searching '22': {viewModel.TonieFiles.Count} tonie(s) shown");
        Assert.True(viewModel.IsSearchActive, "Search should be active");
        Assert.Equal("22", viewModel.SearchText);
        Assert.Single(viewModel.TonieFiles);

        var bertanTonie = viewModel.TonieFiles.First();
        Console.WriteLine($"[SEARCH TEST] Filtered tonie: {bertanTonie.DisplayName}");
        Assert.Contains("Bertan", bertanTonie.DisplayName);

        // Step 9: Clear search again
        Console.WriteLine("[SEARCH TEST] Step 9: Clearing search...");
        viewModel.ClearSearchCommand.Execute(null);
        await Task.Delay(500);

        // Verify all 3 tonies are shown
        Assert.False(viewModel.IsSearchActive, "Search should not be active after clearing");
        Assert.Equal(3, viewModel.TonieFiles.Count);

        // Step 10: Search by Audio ID (0x12345678) and verify only Can is shown
        Console.WriteLine("[SEARCH TEST] Step 10: Searching for '12345678' (Audio ID in hex)...");
        viewModel.HandleSearchInput("12345678");

        // Wait for debounce (250ms) + a bit extra for processing
        Console.WriteLine("[SEARCH TEST] Waiting for search debounce and filtering...");
        await Task.Delay(500);

        // Verify only Can tonie is shown
        Console.WriteLine($"[SEARCH TEST] After searching by Audio ID: {viewModel.TonieFiles.Count} tonie(s) shown");
        Assert.True(viewModel.IsSearchActive, "Search should be active");
        Assert.Equal("12345678", viewModel.SearchText);
        Assert.Single(viewModel.TonieFiles);

        var canTonie = viewModel.TonieFiles.First();
        Console.WriteLine($"[SEARCH TEST] Filtered tonie: {canTonie.DisplayName}");
        Assert.Contains("Can", canTonie.DisplayName);

        // Verify the InfoText contains the audio ID
        Console.WriteLine($"[SEARCH TEST] Can tonie InfoText: {canTonie.InfoText}");
        Assert.Contains("0X12345678", canTonie.InfoText.ToUpper());

        // Step 11: Also test searching with 0x prefix
        Console.WriteLine("[SEARCH TEST] Step 11: Searching for '0x12345678' (Audio ID with 0x prefix)...");
        viewModel.HandleSearchInput("0x12345678");
        await Task.Delay(500);

        // Verify only Can tonie is shown
        Console.WriteLine($"[SEARCH TEST] After searching with 0x prefix: {viewModel.TonieFiles.Count} tonie(s) shown");
        Assert.Single(viewModel.TonieFiles);
        Assert.Contains("Can", viewModel.TonieFiles.First().DisplayName);

        // Step 12: Test searching with partial Audio ID
        Console.WriteLine("[SEARCH TEST] Step 12: Searching for '12345' (partial Audio ID)...");
        viewModel.HandleSearchInput("12345");
        await Task.Delay(500);

        // Verify only Can tonie is shown
        Console.WriteLine($"[SEARCH TEST] After searching partial Audio ID: {viewModel.TonieFiles.Count} tonie(s) shown");
        Assert.True(viewModel.IsSearchActive, "Search should be active");
        Assert.Equal("12345", viewModel.SearchText);
        Assert.Single(viewModel.TonieFiles);

        var canToniePartial = viewModel.TonieFiles.First();
        Console.WriteLine($"[SEARCH TEST] Filtered tonie: {canToniePartial.DisplayName}");
        Assert.Contains("Can", canToniePartial.DisplayName);

        // Step 13: Test searching by folder name (UID)
        Console.WriteLine("[SEARCH TEST] Step 13: Searching for '1111ED0E' (folder name/UID)...");
        viewModel.HandleSearchInput("1111ED0E");
        await Task.Delay(500);

        // Verify only Ahmed tonie is shown
        Console.WriteLine($"[SEARCH TEST] After searching by folder name: {viewModel.TonieFiles.Count} tonie(s) shown");
        Assert.True(viewModel.IsSearchActive, "Search should be active");
        Assert.Equal("1111ED0E", viewModel.SearchText);
        Assert.Single(viewModel.TonieFiles);

        var ahmedTonieByFolder = viewModel.TonieFiles.First();
        Console.WriteLine($"[SEARCH TEST] Filtered tonie: {ahmedTonieByFolder.DisplayName}");
        Console.WriteLine($"[SEARCH TEST] Filtered tonie directory: {ahmedTonieByFolder.DirectoryName}");
        Assert.Contains("Ahmed", ahmedTonieByFolder.DisplayName);
        Assert.Equal("1111ED0E", ahmedTonieByFolder.DirectoryName);

        // Step 14: Test searching by partial folder name
        Console.WriteLine("[SEARCH TEST] Step 14: Searching for '3333' (partial folder name)...");
        viewModel.HandleSearchInput("3333");
        await Task.Delay(500);

        // Verify only Can tonie is shown
        Console.WriteLine($"[SEARCH TEST] After searching partial folder name: {viewModel.TonieFiles.Count} tonie(s) shown");
        Assert.True(viewModel.IsSearchActive, "Search should be active");
        Assert.Equal("3333", viewModel.SearchText);
        Assert.Single(viewModel.TonieFiles);

        var canTonieByPartialFolder = viewModel.TonieFiles.First();
        Console.WriteLine($"[SEARCH TEST] Filtered tonie: {canTonieByPartialFolder.DisplayName}");
        Assert.Contains("Can", canTonieByPartialFolder.DisplayName);
        Assert.Contains("3333", canTonieByPartialFolder.DirectoryName);

        // Step 15: Test searching by user-facing RFID (should match display name RFID format)
        Console.WriteLine("[SEARCH TEST] Step 15: Searching for '0EED1111' (user-facing RFID from display name)...");
        viewModel.HandleSearchInput("0EED1111");
        await Task.Delay(500);

        // Verify only Ahmed tonie is shown (searches in display name which contains "RFID: 0EED1111")
        Console.WriteLine($"[SEARCH TEST] After searching by user-facing RFID: {viewModel.TonieFiles.Count} tonie(s) shown");
        Assert.True(viewModel.IsSearchActive, "Search should be active");
        Assert.Equal("0EED1111", viewModel.SearchText);
        Assert.Single(viewModel.TonieFiles);

        var ahmedTonieByRfid = viewModel.TonieFiles.First();
        Console.WriteLine($"[SEARCH TEST] Filtered tonie: {ahmedTonieByRfid.DisplayName}");
        Assert.Contains("Ahmed", ahmedTonieByRfid.DisplayName);
        Assert.Contains("0EED1111", ahmedTonieByRfid.DisplayName); // Display name contains [RFID: 0EED1111]

        // Step 16: Test reverse RFID search (search by actual RFID, find reversed folder)
        // We want folder B16DC21C, so we pass 1CC26DB1 which will be reversed to B16DC21C
        // Note: Using track2Path creates a hash collision with Bertan, so both will share metadata
        Console.WriteLine("[SEARCH TEST] Step 16: Creating official tonie simulation with folder B16DC21C...");
        CreateCustomTonieWithTitle(viewModel, "Official Tonie Test", "1CC26DB1", track2Path, customAudioId: 0x9ABCDEF0);

        // Reload to get the new tonie
        viewModel = new MainWindowViewModel(window);
        await Task.Delay(100);
        await SimulateDirectoryOpen(viewModel, _contentDir);
        Console.WriteLine($"[SEARCH TEST] Now have {viewModel.TonieFiles.Count} tonies loaded");

        // Search by actual RFID (unreversed: 1CC26DB1) - should find the folder B16DC21C (reversed)
        Console.WriteLine("[SEARCH TEST] Searching for '1CC26DB1' (actual RFID, should find folder B16DC21C)...");
        viewModel.HandleSearchInput("1CC26DB1");
        await Task.Delay(500);

        // Verify the tonie with folder B16DC21C is found
        // Note: Due to hash collision with Bertan (same audio file), we get 2 results
        Console.WriteLine($"[SEARCH TEST] After reverse RFID search: {viewModel.TonieFiles.Count} tonie(s) shown");
        Assert.True(viewModel.IsSearchActive, "Search should be active");
        Assert.Equal("1CC26DB1", viewModel.SearchText);

        // Find the tonie with the correct directory (B16DC21C)
        var officialTonieByReverseRfid = viewModel.TonieFiles.FirstOrDefault(t => t.DirectoryName == "B16DC21C");
        Assert.NotNull(officialTonieByReverseRfid);

        Console.WriteLine($"[SEARCH TEST] Found tonie with directory: {officialTonieByReverseRfid.DirectoryName}");
        Console.WriteLine($"[SEARCH TEST] Display name: {officialTonieByReverseRfid.DisplayName}");
        Assert.Equal("B16DC21C", officialTonieByReverseRfid.DirectoryName);

        // Final clear
        Console.WriteLine("[SEARCH TEST] Step 17: Final search clear...");
        viewModel.ClearSearchCommand.Execute(null);
        await Task.Delay(500);
        Assert.Equal(4, viewModel.TonieFiles.Count); // Now we have 4 tonies total

        Console.WriteLine($"[SEARCH TEST] Total test time: {testSw.ElapsedMilliseconds}ms ({testSw.Elapsed.TotalSeconds:F1}s)");
        Console.WriteLine("[SEARCH TEST] Test completed successfully!");
    }

    private void CreateCustomTonieWithTitle(
        MainWindowViewModel viewModel,
        string title,
        string rfidUid,
        string audioPath,
        uint? customAudioId)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[SEARCH TEST] Creating tonie '{title}' with RFID {rfidUid}...");

        // Parse RFID and create directory structure
        var customTonieService = new CustomTonieCreationService(
            new TonieFileService(),
            new TonieMetadataService()
        );

        var tonieFileService = new TonieFileService();
        var reversedUid = tonieFileService.ReverseUidBytes(rfidUid);

        Console.WriteLine($"[SEARCH TEST] Reversed UID: {reversedUid}, Audio ID: {(customAudioId.HasValue ? $"0x{customAudioId.Value:X8} (custom)" : "auto-generated from timestamp")}");

        // Create the tonie file
        var (generatedHash, targetFile) = customTonieService.CreateCustomTonieFile(
            _contentDir,
            reversedUid,
            new[] { audioPath },
            rfidUid,
            customAudioId
        );

        Console.WriteLine($"[SEARCH TEST] Tonie file created at: {targetFile}");
        Console.WriteLine($"[SEARCH TEST] Hash: {generatedHash}");

        // Get the audio ID from the created file
        var createdTonie = TonieAudio.FromFile(targetFile, readAudio: false);
        var audioId = createdTonie.Header.AudioId;
        Console.WriteLine($"[SEARCH TEST] Audio ID from file: 0x{audioId:X8}");

        // Manually register in customTonies.json with the specific title
        var metadataService = new TonieMetadataService();
        var titleWithRfid = $"{title} [RFID: {rfidUid}]";

        // Load existing customTonies.json or create new
        var customToniesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "customTonies.json");
        JArray customTonies;

        if (File.Exists(customToniesPath))
        {
            var json = File.ReadAllText(customToniesPath);
            customTonies = JArray.Parse(json);
        }
        else
        {
            customTonies = new JArray();
        }

        // Check if entry already exists (use lowercase keys to match TonieMetadataService format)
        var existingEntry = customTonies.FirstOrDefault(e =>
        {
            var hashArray = e["hash"] as JArray;
            return hashArray != null && hashArray.Any(h => h.ToString() == generatedHash);
        }) as JObject;

        if (existingEntry != null)
        {
            // Update existing entry
            existingEntry["title"] = titleWithRfid;
        }
        else
        {
            // Add new entry with full structure matching TonieMetadataService format
            var newEntry = new JObject
            {
                ["no"] = customTonies.Count.ToString(),
                ["model"] = "",
                ["audio_id"] = new JArray { audioId.ToString() },
                ["hash"] = new JArray { generatedHash },
                ["title"] = titleWithRfid,
                ["series"] = title,
                ["episodes"] = titleWithRfid,
                ["tracks"] = new JArray(),
                ["release"] = "",
                ["language"] = "en-us",
                ["category"] = "custom",
                ["pic"] = "",
                ["directory"] = reversedUid
            };
            customTonies.Add(newEntry);
        }

        File.WriteAllText(customToniesPath, customTonies.ToString(Formatting.Indented));

        Console.WriteLine($"[SEARCH TEST] Registered in customTonies.json as '{titleWithRfid}'");
        Console.WriteLine($"[SEARCH TEST] Tonie creation took {sw.ElapsedMilliseconds}ms");
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
                Console.WriteLine($"[SEARCH TEST] Warning: Could not delete test directory: {ex.Message}");
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
                Console.WriteLine($"[SEARCH TEST] Warning: Could not delete customTonies.json: {ex.Message}");
            }
        }
    }
}

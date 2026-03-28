using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TeddyBench.Avalonia.Services;
using TeddyBench.Avalonia.ViewModels;
using TeddyBench.Avalonia.Dialogs;
using TonieFile;
using Xunit;

namespace TeddyBench.Avalonia.Tests;

/// <summary>
/// Tests for TRASHCAN recovery dialog functionality
/// Uses temporary test directories instead of hardcoded SD card paths
/// </summary>
public class TrashcanDialogTests : IDisposable
{
    private readonly string _testSdCardPath;
    private readonly string _contentPath;
    private readonly string _trashcanPath;
    private readonly string _customTonieJsonPath;

    public TrashcanDialogTests()
    {
        // Set up temporary test directory structure
        _testSdCardPath = Path.Combine(Path.GetTempPath(), $"TeddyBench_Trashcan_Test_{Guid.NewGuid():N}");
        _contentPath = Path.Combine(_testSdCardPath, "CONTENT");
        _trashcanPath = Path.Combine(_testSdCardPath, "TRASHCAN");

        Directory.CreateDirectory(_contentPath);
        Directory.CreateDirectory(_trashcanPath);

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
            if (Directory.Exists(_testSdCardPath))
            {
                Directory.Delete(_testSdCardPath, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Helper method to create a test Tonie file
    /// </summary>
    private void CreateTestTonieFile(string directory, string fileName, uint audioId)
    {
        var track1Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "track1.mp3");

        // Create the directory if it doesn't exist
        Directory.CreateDirectory(directory);

        // Create a test Tonie file
        var tonie = new TonieAudio(
            sources: new[] { track1Path },
            audioId: audioId,
            bitRate: 96000,
            useVbr: false,
            prefixLocation: null
        );

        var filePath = Path.Combine(directory, fileName);
        File.WriteAllBytes(filePath, tonie.FileContent);
    }

    [AvaloniaFact]
    public async Task TrashcanService_WithTestDirectory_ShouldScanTrashcan()
    {
        Console.WriteLine("=== Testing TrashcanService.ScanTrashcanAsync ===");

        // Create test deleted Tonie files in TRASHCAN
        var trashcanSubDir1 = Path.Combine(_trashcanPath, "DeletedTonie_001");
        var trashcanSubDir2 = Path.Combine(_trashcanPath, "DeletedTonie_002");
        CreateTestTonieFile(trashcanSubDir1, "tonie1.043", 0x12345678u);
        CreateTestTonieFile(trashcanSubDir2, "tonie2.043", 0x87654321u);

        Console.WriteLine($"✓ Created test files in TRASHCAN: {_trashcanPath}");

        // Create services
        var metadataService = new TonieMetadataService();
        var trashcanService = new TrashcanService(metadataService);

        // Scan TRASHCAN
        var deletedTonies = await trashcanService.ScanTrashcanAsync(_testSdCardPath);

        // Should not throw and should return a list
        Assert.NotNull(deletedTonies);
        Assert.Equal(2, deletedTonies.Count);

        Console.WriteLine($"Found {deletedTonies.Count} deleted Tonie(s) in TRASHCAN");

        // Verify they have proper data
        foreach (var tonie in deletedTonies)
        {
            Console.WriteLine($"  - {tonie.DisplayName} (UID: {tonie.Uid}, Hash: {tonie.Hash[..8]}...)");

            Assert.NotEmpty(tonie.FilePath);
            Assert.NotEmpty(tonie.Hash);
            Assert.NotEmpty(tonie.Uid);
            Assert.NotEmpty(tonie.DisplayName);
            Assert.True(tonie.DeletionDate > DateTime.MinValue);
        }

        Console.WriteLine("=== Test completed ===");
    }

    [AvaloniaFact]
    public async Task TrashcanDialog_ViewModel_ShouldInitializeWithoutErrors()
    {
        Console.WriteLine("=== Testing TrashcanManagerDialogViewModel initialization ===");

        // Create test deleted Tonie files in TRASHCAN
        var trashcanSubDir = Path.Combine(_trashcanPath, "DeletedTonie_001");
        CreateTestTonieFile(trashcanSubDir, "test.043", 0x11223344u);

        Console.WriteLine($"✓ Created test file in TRASHCAN: {_trashcanPath}");

        // Create a test window
        var window = new Window();
        window.Show();

        Exception? caughtException = null;

        try
        {
            // Create metadata service
            var metadataService = new TonieMetadataService();

            // Create the ViewModel - this triggers scanning
            var viewModel = new TrashcanManagerDialogViewModel(window, _testSdCardPath, metadataService);

            // Wait for async loading
            await Task.Delay(2000);

            // Verify initialization
            Assert.NotNull(viewModel.DeletedTonies);
            Assert.False(viewModel.IsLoading);
            Assert.NotEmpty(viewModel.StatusText);

            Console.WriteLine($"✓ ViewModel initialized successfully");
            Console.WriteLine($"  Status: {viewModel.StatusText}");
            Console.WriteLine($"  Deleted Tonies: {viewModel.DeletedTonies.Count}");
        }
        catch (Exception ex)
        {
            caughtException = ex;
            Console.WriteLine($"✗ ViewModel initialization failed: {ex.Message}");
        }

        // Assert no exceptions
        Assert.Null(caughtException);

        // Cleanup
        window.Close();

        Console.WriteLine("=== Test completed ===");
    }

    [AvaloniaFact]
    public async Task RfidInputDialog_ShouldValidateUidCorrectly()
    {
        Console.WriteLine("=== Testing RfidInputDialog validation ===");

        // Create a test window
        var parentWindow = new Window();
        parentWindow.Show();

        Exception? caughtException = null;

        try
        {
            // Create the RFID input dialog with empty prefix (TRASHCAN restoration scenario)
            var dialog = new RfidInputDialog("", _contentPath);

            // Get the ViewModel
            var viewModel = dialog.DataContext as RfidInputDialogViewModel;
            Assert.NotNull(viewModel);

            Console.WriteLine("Step 1: Testing empty UID (should be invalid)");
            Assert.False(viewModel.CanSubmit);
            Assert.NotEmpty(viewModel.ErrorMessage);
            Console.WriteLine($"  ✓ Error message: {viewModel.ErrorMessage}");

            Console.WriteLine("Step 2: Testing partial UID (should be invalid)");
            viewModel.RfidUid = "0EED";
            Assert.False(viewModel.CanSubmit);
            Assert.Contains("8 characters", viewModel.ErrorMessage);
            Console.WriteLine($"  ✓ Error message: {viewModel.ErrorMessage}");

            Console.WriteLine("Step 3: Testing invalid characters (should be invalid)");
            viewModel.RfidUid = "0EEDXXYZ";
            Assert.False(viewModel.CanSubmit);
            Console.WriteLine($"  ✓ Error message: {viewModel.ErrorMessage}");

            Console.WriteLine("Step 4: Testing valid UID (should be valid)");
            viewModel.RfidUid = "0EED33EA";
            Assert.True(viewModel.CanSubmit);
            Assert.Empty(viewModel.ErrorMessage);
            Console.WriteLine("  ✓ Valid UID accepted");

            Console.WriteLine("✓ All validation tests passed!");
        }
        catch (Exception ex)
        {
            caughtException = ex;
            Console.WriteLine($"✗ Test failed: {ex.Message}");
        }

        // Assert no exceptions
        Assert.Null(caughtException);

        // Cleanup
        try
        {
            parentWindow.Close();
        }
        catch (InvalidOperationException)
        {
            // Ignore Avalonia headless rendering cleanup errors
            // These are not related to the actual test logic
        }

        Console.WriteLine("=== Test completed ===");
    }
}
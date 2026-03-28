using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TonieFile;
using System.Text.RegularExpressions;
using TeddyBench.Avalonia.Services;
using TeddyBench.Avalonia.Models;
using TeddyBench.Avalonia.Dialogs;
using TeddyBench.Avalonia.Utilities;
using System.Diagnostics;
using System.Threading;

namespace TeddyBench.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Window _window;
    private readonly TonieMetadataService _metadataService;
    private readonly LiveFlagService _liveFlagService;
    private readonly ConfigurationService _configService;
    private readonly TonieFileService _tonieFileService;
    private readonly DirectoryScanService _scanService;
    private readonly TonieSortService _sortService;
    private readonly CustomTonieCreationService _customTonieService;
    private readonly TonieTrackInfoService _trackInfoService;
    private bool _isPlayerDialogOpen = false;
    private bool _isModifyDialogOpen = false;
    private bool _isRenameDialogOpen = false;
    private bool _isDeleteDialogOpen = false;
    private bool _isShortcutsDialogOpen = false;
    private CancellationTokenSource? _searchDebounceToken;
    private List<TonieFileItem> _allTonieFiles = new();

    /// <summary>
    /// Returns true if any dialog is currently open.
    /// This is used to disable main window shortcuts when a dialog is active.
    /// </summary>
    public bool IsAnyDialogOpen => _isPlayerDialogOpen || _isModifyDialogOpen ||
                                    _isRenameDialogOpen || _isDeleteDialogOpen ||
                                    _isShortcutsDialogOpen;

    /// <summary>
    /// Returns true if no dialog is currently open.
    /// This is used to enable/disable main window controls.
    /// </summary>
    public bool IsNoDialogOpen => !IsAnyDialogOpen;

    public MainWindowViewModel(Window window)
    {
        _window = window;
        _metadataService = new TonieMetadataService();
        _liveFlagService = new LiveFlagService();
        _configService = new ConfigurationService();
        _tonieFileService = new TonieFileService();
        _scanService = new DirectoryScanService(_metadataService, _liveFlagService);
        _sortService = new TonieSortService();
        _customTonieService = new CustomTonieCreationService(_tonieFileService, _metadataService);
        _trackInfoService = new TonieTrackInfoService(_metadataService);

        // Hook up scan service events
        _scanService.ProgressUpdate += (s, message) => StatusText = message;
        _scanService.RequestImageDownload += (s, data) => _ = DownloadImageForTonieAsync(data.Hash, data.PicUrl, data.FilePath);

        // Initialize sort options from provider
        SortOptions = new ObservableCollection<SortOptionItem>(SortOptionsProvider.GetAllSortOptions());

        // Load sort option from configuration
        var savedSortOption = _configService.LoadSortOption();
        CurrentSortOption = SortOptions.FirstOrDefault(s => s.Option == savedSortOption) ?? SortOptions[0];

        // Try to download tonies.json on startup if it doesn't exist
        _ = InitializeMetadataAsync();
    }

    private async Task InitializeMetadataAsync()
    {
        try
        {
            // Check if tonies.json exists, if not download it
            var toniesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tonies.json");
            if (!File.Exists(toniesPath))
            {
                StatusText = "Downloading Tonie metadata database...";
                var success = await _metadataService.DownloadTonieJsonAsync();
                if (success)
                {
                    StatusText = "Metadata downloaded successfully. Ready.";
                }
                else
                {
                    StatusText = "Failed to download metadata. Using local cache if available.";
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error initializing metadata: {ex.Message}";
        }
    }

    /// <summary>
    /// Automatically opens the directory picker on startup and attempts to navigate to an SD card if found.
    /// </summary>
    public async Task AutoOpenDirectoryOnStartup()
    {
        try
        {
            // Wait a brief moment for the window to fully initialize
            await Task.Delay(100);

            // Get the storage provider from the window
            var storageProvider = _window.StorageProvider;

            // Try to detect an SD card automatically and use it as suggested location
            IStorageFolder? suggestedStartLocation = null;
            string? sdCardPath = SdCardDetector.FindFirstSdCard();

            if (!string.IsNullOrEmpty(sdCardPath))
            {
                // Found an SD card! Use it as the suggested start location
                StatusText = $"Auto-detected SD card at {sdCardPath}. Opening directory picker...";
                suggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(sdCardPath);
            }
            else
            {
                // No SD card found, try removable storage paths
                StatusText = "Opening directory picker...";

                var removableStoragePaths = SdCardDetector.GetAllRemovableStoragePaths();

                if (removableStoragePaths.Count > 0)
                {
                    // Try to use first removable storage as suggested location
                    suggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(removableStoragePaths[0]);
                }
            }

            if (suggestedStartLocation == null)
            {
                // Fall back to user's home directory
                var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                suggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(homePath);
            }

            await Task.Delay(200); // Brief delay to show the message

            // Configure folder picker options
            var options = new FolderPickerOpenOptions
            {
                Title = "Select Tonie Files Directory",
                AllowMultiple = false,
                SuggestedStartLocation = suggestedStartLocation
            };

            // Show the folder picker dialog (user must click Open to confirm)
            var result = await storageProvider.OpenFolderPickerAsync(options);

            if (result.Count > 0)
            {
                // Get the selected folder path
                var selectedFolder = result[0];
                var folderPath = selectedFolder.TryGetLocalPath();

                if (!string.IsNullOrEmpty(folderPath))
                {
                    await ScanDirectory(folderPath);
                }
                else
                {
                    StatusText = "Could not access the selected folder. Ready.";
                }
            }
            else
            {
                StatusText = "No folder selected. Ready.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Startup error: {ex.Message}";
        }
    }

    [ObservableProperty]
    private string? _currentDirectory;

    [ObservableProperty]
    private string _statusText = "Ready. Open a directory to get started.";

    [ObservableProperty]
    private bool _isScanning = false;

    [ObservableProperty]
    private ObservableCollection<TonieFileItem> _tonieFiles = new();

    [ObservableProperty]
    private TonieFileItem? _selectedFile;

    [ObservableProperty]
    private string _selectedFileDetails = string.Empty;

    [ObservableProperty]
    private string _currentAudioId = string.Empty;

    [ObservableProperty]
    private string _audioLength = string.Empty;

    [ObservableProperty]
    private int _chaptersCount = 0;

    [ObservableProperty]
    private string _fileHash = string.Empty;

    [ObservableProperty]
    private bool _hashValid = false;

    [ObservableProperty]
    private bool _hasDetailedInfo = false;

    [ObservableProperty]
    private System.Collections.IList? _selectedItems;

    [ObservableProperty]
    private ObservableCollection<SortOptionItem> _sortOptions = new();

    [ObservableProperty]
    private SortOptionItem? _currentSortOption;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isSearchActive = false;

    public bool HasSelectedFile => SelectedFile != null && (SelectedItems?.Count ?? 0) <= 1;
    public bool HasValidDirectory => !string.IsNullOrEmpty(CurrentDirectory);
    public bool HasMultipleSelection => (SelectedItems?.Count ?? 0) > 1;

    partial void OnSelectedFileChanged(TonieFileItem? value)
    {
        // Clear detailed info when selecting a new file
        // User must click "Show Info" to see detailed Tonie information
        SelectedFileDetails = string.Empty;
        CurrentAudioId = string.Empty;
        AudioLength = string.Empty;
        ChaptersCount = 0;
        FileHash = string.Empty;
        HashValid = false;
        HasDetailedInfo = false;

        OnPropertyChanged(nameof(HasSelectedFile));
    }

    partial void OnCurrentDirectoryChanged(string? value)
    {
        OnPropertyChanged(nameof(HasValidDirectory));
    }

    partial void OnCurrentSortOptionChanged(SortOptionItem? value)
    {
        if (value != null)
        {
            // Save the sort option to config file
            _configService.SaveSortOption(value.Option);

            if (TonieFiles.Count > 0)
            {
                ApplySorting();
            }
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Cancel any pending search
        _searchDebounceToken?.Cancel();
        _searchDebounceToken = new CancellationTokenSource();

        var token = _searchDebounceToken.Token;

        // Debounce the search with 250ms delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(250, token);

            if (!token.IsCancellationRequested)
            {
                // Execute search on UI thread
                await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ApplySearchFilter();
                });
            }
        });
    }

    [RelayCommand]
    private async Task OpenDirectory()
    {
        try
        {
            StatusText = "Opening directory picker...";

            // Get the storage provider from the window
            var storageProvider = _window.StorageProvider;

            // Configure folder picker options
            var options = new FolderPickerOpenOptions
            {
                Title = "Select Tonie Files Directory",
                AllowMultiple = false
            };

            // Show the folder picker dialog
            var result = await storageProvider.OpenFolderPickerAsync(options);

            if (result.Count > 0)
            {
                // Get the selected folder path
                var selectedFolder = result[0];
                var folderPath = selectedFolder.TryGetLocalPath();

                if (!string.IsNullOrEmpty(folderPath))
                {
                    await ScanDirectory(folderPath);
                }
                else
                {
                    StatusText = "Could not access the selected folder";
                }
            }
            else
            {
                StatusText = "No folder selected";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddCustomTonie()
    {
        if (string.IsNullOrEmpty(CurrentDirectory))
        {
            StatusText = "Please open a directory first (must be Toniebox SD card CONTENT folder)";
            return;
        }

        try
        {
            // Check if we should prompt for Audio ID
            bool shouldPromptForAudioId = _configService.LoadAudioIdPrompt();
            uint? customAudioId = null;

            if (shouldPromptForAudioId)
            {
                // Show Audio ID prompt dialog
                var audioIdDialog = new HexInputDialog();
                var audioIdResult = await audioIdDialog.ShowDialog<bool?>(_window);

                if (audioIdResult == true)
                {
                    customAudioId = audioIdDialog.GetValue();
                    if (customAudioId == null)
                    {
                        StatusText = "Error: Invalid Audio ID";
                        return;
                    }
                }
                // If cancelled, customAudioId remains null and will be auto-generated
            }

            // Load RFID prefix from config file (4 characters in reverse byte order)
            string rfidPrefix = _configService.LoadRfidPrefix();

            // Get RFID UID from user using RfidInputDialog
            var rfidDialog = new RfidInputDialog(rfidPrefix, CurrentDirectory);
            var rfidResult = await rfidDialog.ShowDialog<bool?>(_window);

            if (rfidResult != true)
            {
                StatusText = "Operation cancelled";
                return;
            }

            // Parse RFID UID
            string uidInput = rfidDialog.GetRfidUid()?.Replace(" ", "").Replace(":", "").ToUpper() ?? "";

            // Validate RFID UID
            var (isValid, errorMessage) = _customTonieService.ValidateRfidUid(uidInput);
            if (!isValid)
            {
                StatusText = $"Error: {errorMessage}";
                return;
            }

            // Reverse the RFID UID for directory naming
            string reversedUid = _tonieFileService.ReverseUidBytes(uidInput);

            // Select audio files
            var storageProvider = _window.StorageProvider;

            // Try to start in the Music directory
            var musicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            var suggestedLocation = await storageProvider.TryGetFolderFromPathAsync(musicPath);

            var filePickerOptions = new FilePickerOpenOptions
            {
                Title = "Select Audio Files",
                AllowMultiple = true,
                SuggestedStartLocation = suggestedLocation,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Audio Files") { Patterns = new[] { "*.mp3", "*.ogg", "*.flac", "*.wav", "*.m4a", "*.aac", "*.wma" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            };

            var selectedFiles = await storageProvider.OpenFilePickerAsync(filePickerOptions);

            if (selectedFiles.Count == 0)
            {
                StatusText = "No audio files selected";
                return;
            }

            // Fix file paths - ensure they start with '/' on Linux
            string[] audioPaths = selectedFiles
                .Select(f => f.TryGetLocalPath())
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p =>
                {
                    // On Linux, TryGetLocalPath() sometimes returns paths without leading '/'
                    if (OperatingSystem.IsLinux() && !p!.StartsWith("/"))
                    {
                        return "/" + p;
                    }
                    return p!;
                })
                .ToArray();

            // Show track sorting dialog
            var trackSortDialog = new TrackSortDialog(audioPaths);
            var sortResult = await trackSortDialog.ShowDialog<bool?>(_window);

            if (sortResult != true)
            {
                StatusText = "Operation cancelled";
                return;
            }

            // Get the sorted file paths
            string[] sortedAudioPaths = trackSortDialog.GetSortedFilePaths() ?? audioPaths;

            // Determine the folder name from source files
            string sourceFolderName = _tonieFileService.GetSourceFolderName(sortedAudioPaths);

            // Show progress dialog
            var progressDialog = new ProgressDialog();
            var progressViewModel = new ProgressDialogViewModel(progressDialog, sortedAudioPaths.Length);
            progressDialog.DataContext = progressViewModel;

            // Create encode callback that reports to progress dialog
            var encodeCallback = new Services.AvaloniaEncodeCallback(progressViewModel);

            StatusText = $"Encoding {sortedAudioPaths.Length} file(s)...";
            IsScanning = true;

            string generatedHash = string.Empty;
            string targetFile = string.Empty;

            // Start encoding in background task
            var encodingTask = Task.Run(() =>
            {
                // Create custom Tonie file using the service with progress callback
                // Audio ID will be automatically set to file creation timestamp (or use customAudioId if provided)
                (generatedHash, targetFile) = _customTonieService.CreateCustomTonieFile(
                    CurrentDirectory,
                    reversedUid,
                    sortedAudioPaths,
                    uidInput,
                    customAudioId,
                    encodeCallback);

                // Notify completion
                progressViewModel.Complete();

                StatusText = $"Successfully created custom Tonie: {reversedUid}/500304E0";
            });

            // Show the progress dialog (non-blocking, but waits for encoding to finish)
            await progressDialog.ShowDialog(_window);

            // Register custom tonie in metadata
            if (!string.IsNullOrEmpty(generatedHash))
            {
                // Get the actual audio ID used (either custom or file creation timestamp)
                uint actualAudioId = customAudioId ?? (uint)((DateTimeOffset)new FileInfo(targetFile).CreationTimeUtc).ToUnixTimeSeconds();
                _customTonieService.RegisterCustomTonie(generatedHash, sourceFolderName, uidInput, actualAudioId, sortedAudioPaths, reversedUid, targetFile);
            }

            // Refresh the directory to show the new Tonie
            await ScanDirectory(CurrentDirectory);
        }
        catch (Exception ex)
        {
            StatusText = $"Error creating custom Tonie: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task DecodeSelected()
    {
        if (SelectedFile == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        try
        {
            // Show folder picker dialog with default to home folder
            var storageProvider = _window.StorageProvider;

            var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var options = new FolderPickerOpenOptions
            {
                Title = "Select Destination Folder for Decoded Files",
                AllowMultiple = false,
                SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(homePath)
            };

            var result = await storageProvider.OpenFolderPickerAsync(options);

            if (result.Count == 0)
            {
                StatusText = "Decode cancelled - no folder selected";
                return;
            }

            var selectedFolder = result[0];
            var baseFolderPath = selectedFolder.TryGetLocalPath();

            if (string.IsNullOrEmpty(baseFolderPath))
            {
                StatusText = "Error: Could not access the selected folder";
                return;
            }

            // Sanitize the title - remove all bracketed content
            string sanitizedTitle = StringHelper.SanitizeTitle(SelectedFile.DisplayName);

            // Create subfolder with sanitized title
            var outputDir = Path.Combine(baseFolderPath, sanitizedTitle);
            Directory.CreateDirectory(outputDir);

            StatusText = $"Decoding {SelectedFile.FileName}...";

            var audio = TonieAudio.FromFile(SelectedFile.FilePath);
            audio.DumpAudioFiles(outputDir, Path.GetFileName(SelectedFile.FilePath), false, Array.Empty<string>(), null);

            StatusText = $"Successfully decoded to {outputDir}";
        }
        catch (Exception ex)
        {
            StatusText = $"Decode error: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ShowInfo()
    {
        if (SelectedFile == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        try
        {
            // Ensure track info is saved to customTonies.json (if custom tonie)
            _trackInfoService.EnsureTrackInfo(SelectedFile.FilePath, SelectedFile.Hash);

            var audio = TonieAudio.FromFile(SelectedFile.FilePath, true);

            // Calculate total audio duration from highest granule position
            audio.CalculateStatistics(out _, out _, out _, out _, out _, out _, out ulong highestGranule);
            string audioDuration = FormatDuration(highestGranule);

            // Store the audio ID and other details for UI binding
            CurrentAudioId = audio.Header.AudioId.ToString();
            AudioLength = audioDuration;
            ChaptersCount = audio.Header.AudioChapters.Length;
            FileHash = BitConverter.ToString(audio.Header.Hash).Replace("-", "");
            HashValid = audio.HashCorrect;
            HasDetailedInfo = true;

            // Keep the old SelectedFileDetails for backward compatibility (empty now, or can be removed)
            SelectedFileDetails = string.Empty;

            StatusText = "File information loaded";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading info: {ex.Message}";
            SelectedFileDetails = string.Empty;
            CurrentAudioId = string.Empty;
            AudioLength = string.Empty;
            ChaptersCount = 0;
            FileHash = string.Empty;
            HashValid = false;
            HasDetailedInfo = false;
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        if (!string.IsNullOrEmpty(CurrentDirectory))
        {
            await ScanDirectory(CurrentDirectory);
        }
    }

    [RelayCommand]
    private void Exit()
    {
        // Save configuration before exiting
        if (CurrentSortOption != null)
        {
            _configService.SaveSortOption(CurrentSortOption.Option);
        }

        Environment.Exit(0);
    }

    [RelayCommand]
    private void About()
    {
        StatusText = "TeddyBench Avalonia - Cross-platform Tonie File Manager";
    }

    [RelayCommand]
    private async Task ShowShortcuts()
    {
        if (_isShortcutsDialogOpen)
            return;

        try
        {
            _isShortcutsDialogOpen = true;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
            var shortcutsDialog = new Dialogs.ShortcutsDialog();
            await shortcutsDialog.ShowDialog(_window);
        }
        finally
        {
            _isShortcutsDialogOpen = false;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
        }
    }

    [RelayCommand]
    private async Task OpenTrashcanManager()
    {
        if (string.IsNullOrEmpty(CurrentDirectory))
        {
            StatusText = "Please select a directory first";
            return;
        }

        try
        {
            // Determine SD card root (assuming CurrentDirectory is CONTENT folder or a subfolder)
            var sdCardPath = CurrentDirectory;
            if (sdCardPath.Contains("CONTENT"))
            {
                // Navigate up to SD card root
                sdCardPath = Directory.GetParent(sdCardPath)?.FullName ?? sdCardPath;
                if (Path.GetFileName(sdCardPath) == "CONTENT")
                {
                    sdCardPath = Directory.GetParent(sdCardPath)?.FullName ?? sdCardPath;
                }
            }

            var trashcanDialog = new Dialogs.TrashcanManagerDialog();
            var viewModel = new Dialogs.TrashcanManagerDialogViewModel(trashcanDialog, sdCardPath, _metadataService);
            trashcanDialog.DataContext = viewModel;
            await trashcanDialog.ShowDialog(_window);

            // If changes were made, refresh the current view
            if (viewModel.DialogResult)
            {
                await Refresh();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening TRASHCAN manager: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectTonie(TonieFileItem item)
    {
        // Deselect all items
        foreach (var tonieFile in TonieFiles)
        {
            tonieFile.IsSelected = false;
        }

        // Select the clicked item
        item.IsSelected = true;
        SelectedFile = item;
    }

    public void UpdateSelectionState()
    {
        OnPropertyChanged(nameof(HasSelectedFile));
        OnPropertyChanged(nameof(HasMultipleSelection));
    }

    /// <summary>
    /// Formats a granule position (at 48000 Hz) as hh:mm:ss or mm:ss
    /// </summary>
    private string FormatDuration(ulong granule)
    {
        // Granules are at 48000 Hz (48000 granules per second)
        double totalSeconds = granule / 48000.0;

        int hours = (int)(totalSeconds / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        int seconds = (int)(totalSeconds % 60);

        if (hours > 0)
        {
            return $"{hours}:{minutes:D2}:{seconds:D2}";
        }
        else
        {
            return $"{minutes}:{seconds:D2}";
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        IsSearchActive = false;
    }

    public void HandleSearchInput(string input)
    {
        SearchText = input;
        IsSearchActive = !string.IsNullOrWhiteSpace(input);
    }

    private void ApplySearchFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // No search text - restore all files
            TonieFiles.Clear();
            foreach (var file in _allTonieFiles)
            {
                TonieFiles.Add(file);
            }
            ApplySorting();
            return;
        }

        string searchLower = SearchText.ToLower();

        // Filter files based on:
        // 1. Display name (main title)
        // 2. Audio ID (if search text is hex format)
        // 3. UID/Containing folder name (both directions)
        var filtered = _allTonieFiles.Where(file =>
        {
            // Search in display name
            if (file.DisplayName.ToLower().Contains(searchLower))
                return true;

            // Search in directory name (UID)
            if (file.DirectoryName.ToLower().Contains(searchLower))
                return true;

            // If search looks like a UID (8 hex chars), also try reversed version
            // This allows searching by actual RFID (e.g., 1CC26DB1) to find folder B16DC21C
            if (searchLower.Length == 8 && Regex.IsMatch(searchLower, "^[0-9a-f]+$"))
            {
                string reversedSearch = ReverseByteOrder(searchLower);
                if (file.DirectoryName.ToLower().Contains(reversedSearch))
                    return true;
            }

            // Try to parse as hex and search in Audio ID
            if (searchLower.StartsWith("0x"))
            {
                // User typed hex format with 0x prefix
                if (file.InfoText.ToLower().Contains(searchLower))
                    return true;
            }
            else if (Regex.IsMatch(searchLower, "^[0-9a-f]+$"))
            {
                // User typed hex digits without 0x prefix
                string hexSearch = "0x" + searchLower;
                if (file.InfoText.ToLower().Contains(hexSearch))
                    return true;
            }

            return false;
        }).ToList();

        TonieFiles.Clear();
        foreach (var file in filtered)
        {
            TonieFiles.Add(file);
        }

        ApplySorting();
    }

    private string ReverseByteOrder(string hexString)
    {
        // Reverse byte order: "1CC26DB1" -> "B16DC21C"
        if (hexString.Length % 2 != 0)
            return hexString;

        string reversed = "";
        for (int i = hexString.Length - 2; i >= 0; i -= 2)
        {
            reversed += hexString.Substring(i, 2);
        }
        return reversed;
    }

    [RelayCommand]
    private void ToggleLiveFlag(TonieFileItem? file)
    {
        if (file == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        try
        {
            StatusText = $"Checking LIVE flag for {file.FileName}...";

            // Check current state and toggle
            bool currentlyHidden = _liveFlagService.GetHiddenAttribute(file.FilePath);
            bool newState = !currentlyHidden;

            StatusText = newState ? $"Adding LIVE flag to {file.FileName}..." : $"Removing LIVE flag from {file.FileName}...";
            bool success = _liveFlagService.SetHiddenAttribute(file.FilePath, newState);

            if (success)
            {
                file.IsLive = newState;
                StatusText = newState ? $"Added LIVE flag to {file.FileName}" : $"Removed LIVE flag from {file.FileName}";

                // Update DisplayName
                var titleWithoutLive = file.DisplayName.Replace("[LIVE] ", "");
                file.DisplayName = file.IsLive ? $"[LIVE] {titleWithoutLive}" : titleWithoutLive;
            }
            else
            {
                StatusText = $"Failed to toggle LIVE flag for {file.FileName}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error toggling LIVE flag: {ex.Message}";
        }
    }


    [RelayCommand]
    private async Task DecodeTonie(TonieFileItem? file)
    {
        if (file == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        try
        {
            // Show folder picker dialog with default to home folder
            var storageProvider = _window.StorageProvider;

            var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var options = new FolderPickerOpenOptions
            {
                Title = "Select Destination Folder for Decoded Files",
                AllowMultiple = false,
                SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(homePath)
            };

            var result = await storageProvider.OpenFolderPickerAsync(options);

            if (result.Count == 0)
            {
                StatusText = "Decode cancelled - no folder selected";
                return;
            }

            var selectedFolder = result[0];
            var baseFolderPath = selectedFolder.TryGetLocalPath();

            if (string.IsNullOrEmpty(baseFolderPath))
            {
                StatusText = "Error: Could not access the selected folder";
                return;
            }

            // Sanitize the title - remove all bracketed content
            string sanitizedTitle = StringHelper.SanitizeTitle(file.DisplayName);

            // Create subfolder with sanitized title
            var outputDir = Path.Combine(baseFolderPath, sanitizedTitle);
            Directory.CreateDirectory(outputDir);

            StatusText = $"Decoding {file.FileName}...";

            var audio = TonieAudio.FromFile(file.FilePath);
            audio.DumpAudioFiles(outputDir, Path.GetFileName(file.FilePath), false, Array.Empty<string>(), null);

            StatusText = $"Successfully decoded to {outputDir}";
        }
        catch (Exception ex)
        {
            StatusText = $"Decode error: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ShowTonieInfo(TonieFileItem? file)
    {
        if (file == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        try
        {
            // Set as selected file so details show
            SelectedFile = file;

            var audio = TonieAudio.FromFile(file.FilePath, true);

            // Calculate total audio duration from highest granule position
            audio.CalculateStatistics(out _, out _, out _, out _, out _, out _, out ulong highestGranule);
            string audioDuration = FormatDuration(highestGranule);

            // Store the audio ID and other details for UI binding
            CurrentAudioId = audio.Header.AudioId.ToString();
            AudioLength = audioDuration;
            ChaptersCount = audio.Header.AudioChapters.Length;
            FileHash = BitConverter.ToString(audio.Header.Hash).Replace("-", "");
            HashValid = audio.HashCorrect;
            HasDetailedInfo = true;

            // Keep the old SelectedFileDetails for backward compatibility (empty now, or can be removed)
            SelectedFileDetails = string.Empty;

            StatusText = "File information loaded";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading info: {ex.Message}";
            SelectedFileDetails = string.Empty;
            CurrentAudioId = string.Empty;
            AudioLength = string.Empty;
            ChaptersCount = 0;
            FileHash = string.Empty;
            HashValid = false;
            HasDetailedInfo = false;
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteSelectedTonie(TonieFileItem? file)
    {
        if (file == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        if (_isDeleteDialogOpen)
            return;

        try
        {
            _isDeleteDialogOpen = true;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
            // Show confirmation dialog using ConfirmDeleteDialog
            var confirmDialog = new ConfirmDeleteDialog(file.DisplayName);
            var result = await confirmDialog.ShowDialog<bool?>(_window);

            if (result != true)
            {
                StatusText = "Delete cancelled";
                return;
            }

            // Read the hash before deleting (if possible)
            string? hash = null;
            try
            {
                var audio = TonieAudio.FromFile(file.FilePath, false);
                hash = BitConverter.ToString(audio.Header.Hash).Replace("-", "");
            }
            catch
            {
                // Ignore - hash will be null
            }

            // Delete the file and directory
            var fileInfo = new FileInfo(file.FilePath);
            var directory = fileInfo.Directory;

            StatusText = $"Deleting {file.FileName}...";

            fileInfo.Delete();

            if (directory != null && directory.Exists)
            {
                // Delete the directory if it's empty or force delete
                try
                {
                    directory.Delete(true); // true = recursive delete
                }
                catch
                {
                    // Ignore directory deletion errors
                }
            }

            // Remove from customTonies.json if we got the hash
            if (!string.IsNullOrEmpty(hash))
            {
                _metadataService.RemoveCustomTonie(hash);
            }

            StatusText = $"Successfully deleted {file.DisplayName}";

            // Refresh the directory to update the view
            if (!string.IsNullOrEmpty(CurrentDirectory))
            {
                await ScanDirectory(CurrentDirectory);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error deleting file: {ex.Message}";
        }
        finally
        {
            _isDeleteDialogOpen = false;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
        }
    }

    [RelayCommand]
    private async Task RenameSelectedTonie(TonieFileItem? file)
    {
        if (file == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        // Only allow renaming custom tonies
        if (!file.IsCustomTonie)
        {
            StatusText = "Only custom tonies can be renamed. Official tonies from the database cannot be renamed.";
            return;
        }

        if (_isRenameDialogOpen)
            return;

        try
        {
            _isRenameDialogOpen = true;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
            // Get the hash first
            string? hash = null;
            try
            {
                var audio = TonieAudio.FromFile(file.FilePath, false);
                hash = BitConverter.ToString(audio.Header.Hash).Replace("-", "");
            }
            catch (Exception ex)
            {
                StatusText = $"Error reading file: {ex.Message}";
                return;
            }

            if (string.IsNullOrEmpty(hash))
            {
                StatusText = "Error: Could not read file hash";
                return;
            }

            // Extract the current title without [LIVE] and [RFID: ...] prefixes
            string currentTitle = StringHelper.SanitizeTitle(file.DisplayName);

            // Show rename dialog using RenameTonieDialog
            var renameDialog = new RenameTonieDialog(currentTitle);
            var result = await renameDialog.ShowDialog<bool?>(_window);

            if (result != true)
            {
                StatusText = "Rename cancelled";
                return;
            }

            string newTitle = renameDialog.GetNewTitle()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(newTitle))
            {
                StatusText = "Error: Title cannot be empty";
                return;
            }

            // Extract RFID from current title if present (to preserve it)
            string? rfidPart = null;
            var rfidMatch = Regex.Match(file.DisplayName, @"\[RFID:\s*([0-9A-F]{8})\]", RegexOptions.IgnoreCase);
            if (rfidMatch.Success)
            {
                rfidPart = $" [RFID: {rfidMatch.Groups[1].Value}]";
            }

            // Construct the full new title with RFID (if it existed)
            string fullNewTitle = newTitle + (rfidPart ?? "");

            // Get existing metadata to preserve other fields
            var existingMetadata = _metadataService.GetCustomTonieMetadata(hash);
            if (existingMetadata != null)
            {
                // Update only the title, preserving all other fields
                existingMetadata.Title = fullNewTitle;
                existingMetadata.Episodes = fullNewTitle; // Also update episodes to match
                _metadataService.UpdateCustomTonie(hash, existingMetadata);
            }

            // Update the DisplayName in the UI
            bool isLive = file.IsLive;
            file.DisplayName = isLive ? $"[LIVE] {fullNewTitle}" : fullNewTitle;

            StatusText = $"Successfully renamed tonie to '{newTitle}'";
        }
        catch (Exception ex)
        {
            StatusText = $"Error renaming tonie: {ex.Message}";
        }
        finally
        {
            _isRenameDialogOpen = false;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task EditMetadata(TonieFileItem? file)
    {
        if (file == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        // Only allow editing metadata for custom tonies
        if (!file.IsCustomTonie)
        {
            StatusText = "Only custom tonies can have their metadata edited.";
            return;
        }

        if (_isRenameDialogOpen)
            return;

        try
        {
            _isRenameDialogOpen = true;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));

            // Get the hash first
            string? hash = null;
            try
            {
                var audio = TonieAudio.FromFile(file.FilePath, false);
                hash = BitConverter.ToString(audio.Header.Hash).Replace("-", "");
            }
            catch (Exception ex)
            {
                StatusText = $"Error reading file: {ex.Message}";
                return;
            }

            if (string.IsNullOrEmpty(hash))
            {
                StatusText = "Error: Could not read file hash";
                return;
            }

            // Get the existing metadata
            var existingMetadata = _metadataService.GetCustomTonieMetadata(hash);
            if (existingMetadata == null)
            {
                StatusText = "Error: Could not find metadata for this custom tonie";
                return;
            }

            // Show edit metadata dialog
            var editDialog = new EditMetadataDialog(existingMetadata);
            var result = await editDialog.ShowDialog<bool?>(_window);

            if (result != true)
            {
                StatusText = "Metadata edit cancelled";
                return;
            }

            var updatedMetadata = editDialog.GetUpdatedMetadata();
            if (updatedMetadata == null)
            {
                StatusText = "Error: Could not get updated metadata";
                return;
            }

            // Update in customTonies.json
            _metadataService.UpdateCustomTonie(hash, updatedMetadata);

            // Update the DisplayName in the UI
            bool isLive = file.IsLive;
            string newDisplayName = !string.IsNullOrEmpty(updatedMetadata.Title)
                ? updatedMetadata.Title
                : updatedMetadata.Series;
            file.DisplayName = isLive ? $"[LIVE] {newDisplayName}" : newDisplayName;

            StatusText = $"Successfully updated metadata for '{newDisplayName}'";
        }
        catch (Exception ex)
        {
            StatusText = $"Error editing metadata: {ex.Message}";
        }
        finally
        {
            _isRenameDialogOpen = false;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BulkEditMetadata()
    {
        // Get selected items
        var selectedFiles = SelectedItems?.Cast<TonieFileItem>().ToList();
        if (selectedFiles == null || selectedFiles.Count == 0)
        {
            StatusText = "Please select at least one custom tonie";
            return;
        }

        // Filter to only custom tonies
        var customTonies = selectedFiles.Where(f => f.IsCustomTonie).ToList();
        if (customTonies.Count == 0)
        {
            StatusText = "No custom tonies selected. Only custom tonies can have their metadata edited.";
            return;
        }

        if (_isRenameDialogOpen)
            return;

        try
        {
            _isRenameDialogOpen = true;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));

            // Show bulk edit dialog
            var bulkEditDialog = new BulkEditMetadataDialog(customTonies.Count);
            var result = await bulkEditDialog.ShowDialog<bool?>(_window);

            if (result != true)
            {
                StatusText = "Bulk edit cancelled";
                return;
            }

            var (series, category, language) = bulkEditDialog.GetUpdatedFields();

            // Check if any fields were provided
            if (string.IsNullOrWhiteSpace(series) &&
                string.IsNullOrWhiteSpace(category) &&
                string.IsNullOrWhiteSpace(language))
            {
                StatusText = "No fields to update";
                return;
            }

            // Get hashes for all custom tonies
            var hashes = new List<string>();
            foreach (var file in customTonies)
            {
                try
                {
                    var audio = TonieAudio.FromFile(file.FilePath, false);
                    var hash = BitConverter.ToString(audio.Header.Hash).Replace("-", "");
                    hashes.Add(hash);
                }
                catch
                {
                    // Skip files that can't be read
                    continue;
                }
            }

            if (hashes.Count == 0)
            {
                StatusText = "Error: Could not read any of the selected files";
                return;
            }

            // Perform bulk update
            int updatedCount = _metadataService.BulkUpdateCustomTonies(hashes, series, category, language);

            StatusText = $"Successfully updated {updatedCount} custom tonie(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error during bulk edit: {ex.Message}";
        }
        finally
        {
            _isRenameDialogOpen = false;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RemoveAllLiveFlags()
    {
        IsScanning = true;
        int removedCount = 0;
        int errorCount = 0;
        int totalFiles = TonieFiles.Count;
        int filesProcessed = 0;

        try
        {
            StatusText = "Scanning for LIVE flags...";
            await Task.Delay(100);

            foreach (var tonieFile in TonieFiles)
            {
                filesProcessed++;

                // Skip files that aren't marked as LIVE - they can't have the flag
                // (Official tonies from database are never LIVE, only custom files)
                if (!tonieFile.IsLive)
                {
                    continue;
                }

                try
                {
                    // This file is marked as LIVE, so remove the flag
                    // Show friendly name without [LIVE] prefix for clarity
                    var displayName = tonieFile.DisplayName.Replace("[LIVE] ", "");
                    StatusText = $"Removing LIVE flag: {displayName}... ({filesProcessed}/{totalFiles})";
                    await Task.Delay(1);

                    bool success = _liveFlagService.SetHiddenAttribute(tonieFile.FilePath, false);
                    if (success)
                    {
                        tonieFile.IsLive = false;

                        // Update DisplayName to remove [LIVE] prefix
                        var titleWithoutLive = tonieFile.DisplayName.Replace("[LIVE] ", "");
                        tonieFile.DisplayName = titleWithoutLive;

                        removedCount++;
                    }
                    else
                    {
                        errorCount++;
                    }
                }
                catch
                {
                    errorCount++;
                }
            }

            if (errorCount > 0)
            {
                StatusText = $"Removed LIVE flag from {removedCount} tonie(s), {errorCount} error(s)";
            }
            else if (removedCount > 0)
            {
                StatusText = $"Successfully removed LIVE flag from {removedCount} tonie(s)";
            }
            else
            {
                StatusText = "No LIVE flags found to remove";
            }
        }
        finally
        {
            IsScanning = false;
        }

        await Task.CompletedTask;
    }

    private async Task ScanDirectory(string directory)
    {
        IsScanning = true;
        TonieFiles.Clear();
        _allTonieFiles.Clear();
        StatusText = $"Directory selected: {directory}";

        await Task.Delay(100); // Brief delay to ensure UI updates

        try
        {
            // Check if this looks like a Toniebox SD card root and navigate to CONTENT folder
            directory = _scanService.CheckForContentDirectory(directory);
            if (directory.EndsWith("CONTENT"))
            {
                StatusText = $"Detected Toniebox SD card, navigating to CONTENT folder...";
                await Task.Delay(100);
            }

            CurrentDirectory = directory;

            // Use DirectoryScanService to scan the directory
            var scannedFiles = await _scanService.ScanDirectoryAsync(directory);

            // Store all files and populate InfoText with Audio ID for search
            foreach (var file in scannedFiles)
            {
                // Try to read Audio ID for search purposes
                try
                {
                    var audio = TonieAudio.FromFile(file.FilePath, false);
                    file.InfoText = $"0x{audio.Header.AudioId:X8}";
                }
                catch
                {
                    file.InfoText = string.Empty;
                }

                _allTonieFiles.Add(file);
                TonieFiles.Add(file);
            }

            // Apply sorting after loading files
            ApplySorting();

            // Clear search when loading new directory
            SearchText = string.Empty;
            IsSearchActive = false;
        }
        catch (Exception ex)
        {
            StatusText = $"Error scanning directory: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void ApplySorting()
    {
        if (CurrentSortOption == null || TonieFiles.Count == 0)
            return;

        // Use TonieSortService to sort the files
        var sortedList = _sortService.SortTonieFiles(TonieFiles, CurrentSortOption.Option);

        // Replace TonieFiles collection with sorted list
        TonieFiles.Clear();
        foreach (var item in sortedList)
        {
            TonieFiles.Add(item);
        }
    }


    private async Task DownloadImageForTonieAsync(string hash, string picUrl, string filePath)
    {
        try
        {
            var imagePath = await _metadataService.DownloadAndCacheImageAsync(hash, picUrl);

            if (!string.IsNullOrEmpty(imagePath))
            {
                // Find the corresponding TonieFileItem and update its ImagePath
                var item = TonieFiles.FirstOrDefault(t => t.FilePath == filePath);
                if (item != null)
                {
                    item.ImagePath = imagePath;
                    // Trigger property change notification
                    OnPropertyChanged(nameof(TonieFiles));
                }
            }
        }
        catch
        {
            // Ignore image download errors
        }
    }

    public void SaveConfigurationOnExit()
    {
        // Save current sort option before exiting
        if (CurrentSortOption != null)
        {
            _configService.SaveSortOption(CurrentSortOption.Option);
        }
    }

    [RelayCommand]
    private async Task PlayTonie(TonieFileItem? file)
    {
        if (file == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        // Prevent opening multiple player dialogs
        if (_isPlayerDialogOpen)
        {
            return;
        }

        try
        {
            _isPlayerDialogOpen = true;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
            var dialog = new Dialogs.PlayerDialog();
            var viewModel = new PlayerDialogViewModel(file.FilePath, file.DisplayName, _trackInfoService, file.Hash, dialog);
            dialog.DataContext = viewModel;
            await dialog.ShowDialog(_window);
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening player: {ex.Message}";
        }
        finally
        {
            _isPlayerDialogOpen = false;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
        }
    }

    [RelayCommand]
    private async Task AssignNewUid(TonieFileItem? file)
    {
        if (file == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        if (string.IsNullOrEmpty(CurrentDirectory))
        {
            StatusText = "Error: No directory is currently open";
            return;
        }

        try
        {
            // Get the current UID from the file path
            var fileInfo = new FileInfo(file.FilePath);
            var directory = fileInfo.Directory;
            if (directory == null)
            {
                StatusText = "Error: Could not determine current UID";
                return;
            }

            // The directory name is the reversed UID (first 8 chars)
            string currentReversedUid = directory.Name;

            // Reverse it back to get the display UID
            string currentDisplayUid = _tonieFileService.ReverseUidBytes(currentReversedUid);

            // Load RFID prefix from config file (4 characters in reverse byte order)
            string rfidPrefix = _configService.LoadRfidPrefix();

            // Show RFID input dialog with current UID pre-filled
            var rfidDialog = new RfidInputDialog(currentDisplayUid, CurrentDirectory);
            var rfidResult = await rfidDialog.ShowDialog<bool?>(_window);

            if (rfidResult != true)
            {
                StatusText = "Operation cancelled";
                return;
            }

            // Parse new RFID UID
            string newUidInput = rfidDialog.GetRfidUid()?.Replace(" ", "").Replace(":", "").ToUpper() ?? "";

            // Check if the UID has changed
            if (newUidInput == currentDisplayUid)
            {
                StatusText = "UID unchanged";
                return;
            }

            // Validate new RFID UID
            var (isValid, errorMessage) = _customTonieService.ValidateRfidUid(newUidInput);
            if (!isValid)
            {
                StatusText = $"Error: {errorMessage}";
                return;
            }

            // Reverse the new UID for directory naming
            string newReversedUid = _tonieFileService.ReverseUidBytes(newUidInput);

            // Build new paths
            string newDirPath = Path.Combine(CurrentDirectory, newReversedUid);
            string newFilePath = Path.Combine(newDirPath, "500304E0");

            // Check if new location already exists
            if (File.Exists(newFilePath))
            {
                StatusText = $"Error: A tonie with UID '{newUidInput}' already exists";
                return;
            }

            StatusText = $"Moving tonie from {currentDisplayUid} to {newUidInput}...";

            // Read the hash before moving (for updating customTonies.json if needed)
            string? hash = null;
            bool isCustomTonie = file.IsCustomTonie;
            try
            {
                var audio = TonieAudio.FromFile(file.FilePath, false);
                hash = BitConverter.ToString(audio.Header.Hash).Replace("-", "");
            }
            catch
            {
                // Ignore - hash will be null
            }

            // Create new directory
            Directory.CreateDirectory(newDirPath);

            // Move the file
            File.Move(file.FilePath, newFilePath);

            // Try to delete old directory if it's empty
            try
            {
                if (directory.GetFiles().Length == 0 && directory.GetDirectories().Length == 0)
                {
                    directory.Delete();
                }
            }
            catch
            {
                // Ignore errors deleting old directory
            }

            // Update RFID in customTonies.json if this is a custom tonie
            if (isCustomTonie && !string.IsNullOrEmpty(hash))
            {
                // Get the current custom tonie name from customTonies.json
                string? currentCustomName = _metadataService.GetCustomTonieName(hash);

                if (!string.IsNullOrEmpty(currentCustomName))
                {
                    // Extract the title part (without RFID)
                    string titlePart = currentCustomName;
                    var rfidMatch = Regex.Match(currentCustomName, @"^(.*?)\s*\[RFID:\s*[0-9A-F]{8}\]$", RegexOptions.IgnoreCase);
                    if (rfidMatch.Success)
                    {
                        titlePart = rfidMatch.Groups[1].Value.Trim();
                    }

                    // Create new name with updated RFID
                    string newCustomName = $"{titlePart} [RFID: {newUidInput}]";

                    // Get existing metadata to preserve other fields
                    var existingMetadata = _metadataService.GetCustomTonieMetadata(hash);
                    if (existingMetadata != null)
                    {
                        // Update only the title, preserving all other fields
                        existingMetadata.Title = newCustomName;
                        existingMetadata.Episodes = newCustomName; // Also update episodes to match
                        _metadataService.UpdateCustomTonie(hash, existingMetadata);
                    }
                }
            }

            StatusText = $"Successfully reassigned UID from {currentDisplayUid} to {newUidInput}";

            // Refresh the directory to show the updated tonie
            if (!string.IsNullOrEmpty(CurrentDirectory))
            {
                await ScanDirectory(CurrentDirectory);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error reassigning UID: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ChangeAudioId(TonieFileItem? file)
    {
        if (file == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        if (string.IsNullOrEmpty(CurrentDirectory))
        {
            StatusText = "Error: No directory is currently open";
            return;
        }

        try
        {
            // Load the Tonie file to get the current Audio ID
            var tonieAudio = TonieAudio.FromFile(file.FilePath);
            uint currentAudioId = tonieAudio.Header.AudioId;

            // Show the Change Audio ID dialog
            var dialog = new ChangeAudioIdDialog(currentAudioId);
            var result = await dialog.ShowDialog<bool?>(_window);

            if (result != true)
            {
                StatusText = "Operation cancelled";
                return;
            }

            // Get the new Audio ID from the dialog
            uint? newAudioId = dialog.GetAudioId();
            if (newAudioId == null)
            {
                StatusText = "Error: Invalid Audio ID";
                return;
            }

            // Check if the Audio ID has changed
            if (newAudioId.Value == currentAudioId)
            {
                StatusText = "Audio ID unchanged";
                return;
            }

            StatusText = $"Updating Audio ID from 0x{currentAudioId:X8} to 0x{newAudioId.Value:X8}...";

            // Update the Audio ID in the header
            tonieAudio.Header.AudioId = newAudioId.Value;

            // Recalculate the hash of the audio data
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(tonieAudio.Audio);
                tonieAudio.Header.Hash = hash;
            }

            // Update the file content with the new header
            tonieAudio.UpdateFileContent();

            // Write the updated file back to disk
            File.WriteAllBytes(file.FilePath, tonieAudio.FileContent);

            StatusText = $"Successfully changed Audio ID to 0x{newAudioId.Value:X8}";

            // Refresh the file info if it's currently displayed
            if (HasDetailedInfo && SelectedFile?.FilePath == file.FilePath)
            {
                await ShowTonieInfo(file);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error changing Audio ID: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteMultipleTonie()
    {
        if (SelectedItems == null || SelectedItems.Count == 0)
        {
            StatusText = "No items selected";
            return;
        }

        if (_isDeleteDialogOpen)
            return;

        try
        {
            _isDeleteDialogOpen = true;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
            // Create a list of items to delete (copy to avoid modification during enumeration)
            var itemsToDelete = SelectedItems.Cast<TonieFileItem>().ToList();
            int count = itemsToDelete.Count;

            // Show confirmation dialog
            var confirmDialog = new ConfirmDeleteDialog($"{count} tonies");
            var result = await confirmDialog.ShowDialog<bool?>(_window);

            if (result != true)
            {
                StatusText = "Delete cancelled";
                return;
            }

            StatusText = $"Deleting {count} tonies...";
            IsScanning = true;

            int successCount = 0;
            int errorCount = 0;

            foreach (var file in itemsToDelete)
            {
                try
                {
                    // Read the hash before deleting (if possible)
                    string? hash = null;
                    try
                    {
                        var audio = TonieAudio.FromFile(file.FilePath, false);
                        hash = BitConverter.ToString(audio.Header.Hash).Replace("-", "");
                    }
                    catch
                    {
                        // Ignore - hash will be null
                    }

                    // Delete the file and directory
                    var fileInfo = new FileInfo(file.FilePath);
                    var directory = fileInfo.Directory;

                    fileInfo.Delete();

                    if (directory != null && directory.Exists)
                    {
                        // Delete the directory if it's empty or force delete
                        try
                        {
                            directory.Delete(true); // true = recursive delete
                        }
                        catch
                        {
                            // Ignore directory deletion errors
                        }
                    }

                    // Remove from customTonies.json if we got the hash
                    if (!string.IsNullOrEmpty(hash))
                    {
                        _metadataService.RemoveCustomTonie(hash);
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    StatusText = $"Error deleting {file.DisplayName}: {ex.Message}";
                    errorCount++;
                }
            }

            StatusText = $"Successfully deleted {successCount} tonie(s)" + (errorCount > 0 ? $", {errorCount} error(s)" : "");

            // Refresh the directory to update the view
            if (!string.IsNullOrEmpty(CurrentDirectory))
            {
                await ScanDirectory(CurrentDirectory);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error during bulk delete: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            _isDeleteDialogOpen = false;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
        }
    }

    [RelayCommand]
    private async Task RemoveMultipleLiveFlags()
    {
        if (SelectedItems == null || SelectedItems.Count == 0)
        {
            StatusText = "No items selected";
            return;
        }

        IsScanning = true;
        int removedCount = 0;
        int errorCount = 0;
        var itemsToProcess = SelectedItems.Cast<TonieFileItem>().ToList();
        int totalFiles = itemsToProcess.Count;

        try
        {
            StatusText = $"Removing LIVE flags from {totalFiles} selected tonie(s)...";
            await Task.Delay(100);

            foreach (var tonieFile in itemsToProcess)
            {
                try
                {
                    // Check if the file has LIVE flag
                    bool hasLiveFlag = _liveFlagService.GetHiddenAttribute(tonieFile.FilePath);

                    if (hasLiveFlag)
                    {
                        // Remove the flag
                        bool success = _liveFlagService.SetHiddenAttribute(tonieFile.FilePath, false);
                        if (success)
                        {
                            tonieFile.IsLive = false;

                            // Update DisplayName to remove [LIVE] prefix
                            var titleWithoutLive = tonieFile.DisplayName.Replace("[LIVE] ", "");
                            tonieFile.DisplayName = titleWithoutLive;

                            removedCount++;
                        }
                        else
                        {
                            errorCount++;
                        }
                    }
                }
                catch
                {
                    errorCount++;
                }
            }

            if (errorCount > 0)
            {
                StatusText = $"Removed LIVE flag from {removedCount} tonie(s), {errorCount} error(s)";
            }
            else if (removedCount > 0)
            {
                StatusText = $"Successfully removed LIVE flag from {removedCount} tonie(s)";
            }
            else
            {
                StatusText = "No LIVE flags found in selected tonies";
            }
        }
        finally
        {
            IsScanning = false;
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ModifyContents(TonieFileItem? file)
    {
        if (file == null)
        {
            StatusText = "Please select a file first";
            return;
        }

        if (_isModifyDialogOpen)
            return;

        try
        {
            _isModifyDialogOpen = true;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
            // Read the original file to get its hash and audio ID
            TonieAudio originalAudio;
            try
            {
                originalAudio = TonieAudio.FromFile(file.FilePath, false);
            }
            catch (Exception ex)
            {
                StatusText = $"Error reading tonie file: {ex.Message}";
                return;
            }

            string oldHash = BitConverter.ToString(originalAudio.Header.Hash).Replace("-", "");
            uint audioId = originalAudio.Header.AudioId;
            uint? newAudioId = null; // Will be set if user changes it in the dialog

            // Check if this is a non-custom tonie and show warning
            if (!file.IsCustomTonie)
            {
                var confirmDialog = new ConfirmModifyTonieDialog(file.DisplayName, audioId);
                var confirmResult = await confirmDialog.ShowDialog<bool?>(_window);

                if (confirmResult != true)
                {
                    StatusText = "Modification cancelled";
                    return;
                }

                // Get the new audio ID from the dialog
                var viewModel = confirmDialog.DataContext as ConfirmModifyTonieDialogViewModel;
                if (viewModel?.NewAudioId.HasValue == true)
                {
                    newAudioId = viewModel.NewAudioId.Value;
                }
            }

            // Create temporary directory for decoded files
            string tempDir = Path.Combine(Path.GetTempPath(), $"TeddyBench_Modify_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                StatusText = $"Decoding {file.FileName} to temporary storage...";
                await Task.Delay(100); // Brief delay for UI update

                // Decode the tonie to temp directory
                await Task.Run(() =>
                {
                    var audio = TonieAudio.FromFile(file.FilePath);
                    audio.DumpAudioFiles(tempDir, Path.GetFileName(file.FilePath), false, Array.Empty<string>(), null);
                });

                // Get the decoded files and create placeholder names
                var decodedFiles = Directory.GetFiles(tempDir, "*.ogg")
                    .OrderBy(f => f)
                    .ToArray();

                if (decodedFiles.Length == 0)
                {
                    StatusText = "Error: No audio tracks found in tonie file";
                    Directory.Delete(tempDir, true);
                    return;
                }

                // Store the temp directory path to detect original files later
                string tempDirPath = tempDir;

                StatusText = $"Opening track editor with {decodedFiles.Length} track(s)...";

                // Show track sorting dialog
                var trackSortDialog = new TrackSortDialog(decodedFiles);
                var sortResult = await trackSortDialog.ShowDialog<bool?>(_window);

                if (sortResult != true)
                {
                    StatusText = "Modification cancelled";
                    Directory.Delete(tempDir, true);
                    return;
                }

                // Get the sorted/modified file paths
                string[]? sortedAudioPaths = trackSortDialog.GetSortedFilePaths();

                if (sortedAudioPaths == null || sortedAudioPaths.Length == 0)
                {
                    StatusText = "Error: At least one track is required";
                    Directory.Delete(tempDir, true);
                    return;
                }

                // Show progress dialog
                var progressDialog = new ProgressDialog();
                var progressViewModel = new ProgressDialogViewModel(progressDialog, sortedAudioPaths.Length);
                progressDialog.DataContext = progressViewModel;

                // Create encode callback that reports to progress dialog
                var encodeCallback = new Services.AvaloniaEncodeCallback(progressViewModel);

                StatusText = $"Encoding {sortedAudioPaths.Length} track(s)...";
                IsScanning = true;

                string newHash = string.Empty;

                // Start encoding in background task
                var encodingTask = Task.Run(() =>
                {
                    // Build track sources using LOSSLESS approach
                    // Original tracks: use track index (no re-encoding!)
                    // New tracks: use file path (encode once)
                    var trackSources = new List<HybridTonieEncodingService.TrackSourceInfo>();

                    for (int i = 0; i < sortedAudioPaths.Length; i++)
                    {
                        string path = sortedAudioPaths[i];
                        bool isOriginal = path.StartsWith(tempDirPath);

                        if (isOriginal)
                        {
                            // Original track: Extract track index from filename
                            // Filename format: "500304E0 - Track #01.ogg"
                            var fileName = Path.GetFileName(path);
                            var match = System.Text.RegularExpressions.Regex.Match(fileName, @"Track #(\d+)\.ogg");

                            if (match.Success && int.TryParse(match.Groups[1].Value, out int trackNum))
                            {
                                // LOSSLESS: Pass track index, NOT file path!
                                // This avoids re-encoding the decoded OGG file
                                trackSources.Add(new HybridTonieEncodingService.TrackSourceInfo
                                {
                                    IsOriginal = true,
                                    OriginalTrackIndex = trackNum - 1  // 0-based index
                                    // AudioFilePath is null - service will extract raw data directly!
                                });
                            }
                            else
                            {
                                // Fallback: if we can't parse track number, treat as new file
                                // (This shouldn't happen with DumpAudioFiles output, but be safe)
                                trackSources.Add(new HybridTonieEncodingService.TrackSourceInfo
                                {
                                    IsOriginal = false,
                                    AudioFilePath = path
                                });
                            }
                        }
                        else
                        {
                            // New track: Pass file path for encoding
                            trackSources.Add(new HybridTonieEncodingService.TrackSourceInfo
                            {
                                IsOriginal = false,
                                AudioFilePath = path
                            });
                        }
                    }

                    // Use hybrid encoding with LOSSLESS approach for original tracks
                    var hybridEncoder = new HybridTonieEncodingService();
                    var (fileContent, hash) = hybridEncoder.EncodeHybridTonie(trackSources, audioId, file.FilePath, 96, encodeCallback);
                    newHash = hash;

                    // Overwrite the original file with the encoded content
                    File.WriteAllBytes(file.FilePath, fileContent);

                    // If the audio ID needs to be changed (for official tonies), update the stream serial number
                    if (newAudioId.HasValue && newAudioId.Value != audioId)
                    {
                        StatusText = $"Updating Audio ID to {newAudioId.Value:X8}...";

                        // Load the newly written tonie file
                        var modifiedTonie = TonieAudio.FromFile(file.FilePath);

                        // Update the stream serial number in the Ogg stream
                        byte[] updatedAudio = modifiedTonie.UpdateStreamSerialNumber(newAudioId.Value);

                        // Update the header with the new audio ID
                        modifiedTonie.Header.AudioId = newAudioId.Value;

                        // Rebuild the file content with the updated audio and header
                        modifiedTonie.Audio = updatedAudio;
                        modifiedTonie.FileContent = new byte[updatedAudio.Length + 0x1000];
                        Array.Copy(updatedAudio, 0, modifiedTonie.FileContent, 0x1000, updatedAudio.Length);
                        modifiedTonie.UpdateFileContent();

                        // Write the updated file
                        File.WriteAllBytes(file.FilePath, modifiedTonie.FileContent);

                        // Recalculate hash with the new audio data
                        using (var sha1 = System.Security.Cryptography.SHA1.Create())
                        {
                            byte[] newHashBytes = sha1.ComputeHash(updatedAudio);
                            newHash = BitConverter.ToString(newHashBytes).Replace("-", "");
                        }
                    }

                    // Notify completion
                    progressViewModel.Complete();

                    StatusText = $"Successfully modified {file.FileName}";
                });

                // Show the progress dialog (non-blocking, but waits for encoding to finish)
                await progressDialog.ShowDialog(_window);

                // Update customTonies.json with new hash
                // Determine the title for the metadata
                string metadataTitle = file.DisplayName;

                // If it was an official tonie, extract just the title without [LIVE] prefix
                if (!file.IsCustomTonie)
                {
                    metadataTitle = StringHelper.SanitizeTitle(file.DisplayName);

                    // Try to extract RFID from the directory structure
                    var fileInfo = new FileInfo(file.FilePath);
                    var directory = fileInfo.Directory;
                    if (directory?.Parent != null)
                    {
                        // Directory name is the reversed RFID
                        string reversedRfid = directory.Name;
                        // Reverse it back for display
                        string displayRfid = _tonieFileService.ReverseUidBytes(reversedRfid);
                        metadataTitle = $"{metadataTitle} [RFID: {displayRfid}]";
                    }
                }

                _metadataService.UpdateTonieHash(oldHash, newHash, metadataTitle);

                StatusText = $"Successfully modified {file.DisplayName}";

                // Clean up temp directory
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // Ignore temp directory cleanup errors
                }

                // Refresh the directory to show the updated tonie
                if (!string.IsNullOrEmpty(CurrentDirectory))
                {
                    await ScanDirectory(CurrentDirectory);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error modifying tonie: {ex.Message}";

                // Clean up temp directory on error
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                    // Ignore temp directory cleanup errors
                }
            }
            finally
            {
                IsScanning = false;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            _isModifyDialogOpen = false;
            OnPropertyChanged(nameof(IsAnyDialogOpen));
            OnPropertyChanged(nameof(IsNoDialogOpen));
        }
    }

    [RelayCommand]
    private async Task CopyDirectoryPath()
    {
        if (SelectedFile == null)
        {
            StatusText = "No file selected";
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(SelectedFile.FilePath))
            {
                StatusText = "Unable to determine file path";
                return;
            }

            // Use FileInfo to get the full directory path
            var fileInfo = new FileInfo(SelectedFile.FilePath);
            var directory = fileInfo.Directory?.FullName;

            if (string.IsNullOrEmpty(directory))
            {
                StatusText = "Unable to determine directory path";
                return;
            }

            var clipboard = _window.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(directory);
                StatusText = "Directory path copied to clipboard";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error copying to clipboard: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CopyAudioId()
    {
        if (string.IsNullOrEmpty(CurrentAudioId))
        {
            StatusText = "No Audio ID available. Click 'Show Info' first.";
            return;
        }

        try
        {
            var clipboard = _window.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(CurrentAudioId);
                StatusText = "Audio ID copied to clipboard";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error copying to clipboard: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenContainingFolder()
    {
        if (SelectedFile == null)
        {
            StatusText = "No file selected";
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(SelectedFile.FilePath))
            {
                StatusText = "Unable to determine file path";
                return;
            }

            // Use FileInfo to get the full directory path
            var fileInfo = new FileInfo(SelectedFile.FilePath);
            var directory = fileInfo.Directory?.FullName;

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                StatusText = $"Directory does not exist: {directory}";
                return;
            }

            // Cross-platform directory opening
            if (OperatingSystem.IsWindows())
            {
                Process.Start("explorer.exe", directory);
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", directory);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", directory);
            }
            else
            {
                StatusText = "Opening folders is not supported on this platform";
            }

            StatusText = "Opened containing folder";
        }
        catch (Exception ex)
        {
            StatusText = $"Error opening folder: {ex.Message}";
        }
    }
}

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
using TeddyBench.Avalonia.Models;

namespace TeddyBench.Avalonia.Dialogs;

public partial class TrackSortDialogViewModel : ObservableObject
{
    private readonly Window _parentWindow;

    [ObservableProperty]
    private ObservableCollection<AudioTrackItem> _tracks = new();

    [ObservableProperty]
    private ObservableCollection<AudioTrackItem> _selectedTracks = new();

    [ObservableProperty]
    private bool _canMoveUp = false;

    [ObservableProperty]
    private bool _canMoveDown = false;

    [ObservableProperty]
    private bool _canRemove = false;

    public bool DialogResult { get; private set; }

    public TrackSortDialogViewModel(Window parentWindow, string[] audioPaths)
    {
        _parentWindow = parentWindow;

        // Sort by folder grouping - files from the same folder stay together
        var sortedPaths = SortByFolderThenFilename(audioPaths);
        for (int i = 0; i < sortedPaths.Length; i++)
        {
            var (artist, title, trackNumberTag) = ReadAudioMetadata(sortedPaths[i]);
            Tracks.Add(new AudioTrackItem
            {
                TrackNumber = i + 1,
                FileName = Path.GetFileName(sortedPaths[i]),
                FilePath = sortedPaths[i],
                Artist = artist,
                Title = title,
                TrackNumberTag = trackNumberTag
            });
        }
    }

    partial void OnSelectedTracksChanged(ObservableCollection<AudioTrackItem> value)
    {
        UpdateButtonStates();
    }

    public void UpdateButtonStates()
    {
        var selectedIndices = SelectedTracks
            .Select(t => Tracks.IndexOf(t))
            .OrderBy(i => i)
            .ToList();

        CanMoveUp = selectedIndices.Count > 0 && selectedIndices[0] > 0;
        CanMoveDown = selectedIndices.Count > 0 && selectedIndices[selectedIndices.Count - 1] < Tracks.Count - 1;
        CanRemove = selectedIndices.Count > 0;
    }

    [RelayCommand]
    private void MoveUp()
    {
        var selectedIndices = SelectedTracks
            .Select(t => Tracks.IndexOf(t))
            .OrderBy(i => i)
            .ToList();

        if (selectedIndices.Count == 0 || selectedIndices[0] == 0)
            return;

        var selectedItems = selectedIndices.Select(i => Tracks[i]).ToList();

        for (int i = 0; i < selectedIndices.Count; i++)
        {
            int index = selectedIndices[i];
            if (index > 0)
            {
                var item = Tracks[index];
                Tracks.RemoveAt(index);
                Tracks.Insert(index - 1, item);
            }
        }

        // Update track numbers
        for (int i = 0; i < Tracks.Count; i++)
        {
            Tracks[i].TrackNumber = i + 1;
        }

        // Restore selection
        SelectedTracks.Clear();
        foreach (var item in selectedItems)
        {
            SelectedTracks.Add(item);
        }

        UpdateButtonStates();
    }

    [RelayCommand]
    private void MoveDown()
    {
        var selectedIndices = SelectedTracks
            .Select(t => Tracks.IndexOf(t))
            .OrderByDescending(i => i)
            .ToList();

        if (selectedIndices.Count == 0 || selectedIndices[0] == Tracks.Count - 1)
            return;

        var selectedItems = selectedIndices.Select(i => Tracks[i]).ToList();

        for (int i = 0; i < selectedIndices.Count; i++)
        {
            int index = selectedIndices[i];
            if (index < Tracks.Count - 1)
            {
                var item = Tracks[index];
                Tracks.RemoveAt(index);
                Tracks.Insert(index + 1, item);
            }
        }

        // Update track numbers
        for (int i = 0; i < Tracks.Count; i++)
        {
            Tracks[i].TrackNumber = i + 1;
        }

        // Restore selection
        SelectedTracks.Clear();
        foreach (var item in selectedItems)
        {
            SelectedTracks.Add(item);
        }

        UpdateButtonStates();
    }

    [RelayCommand]
    private async Task AddFiles()
    {
        try
        {
            var storageProvider = _parentWindow.StorageProvider;

            // Try to start in the Music directory
            var musicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            var suggestedLocation = await storageProvider.TryGetFolderFromPathAsync(musicPath);

            var filePickerOptions = new FilePickerOpenOptions
            {
                Title = "Add Audio Files",
                AllowMultiple = true,
                SuggestedStartLocation = suggestedLocation,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Audio Files") { Patterns = new[] { "*.mp3", "*.ogg", "*.flac", "*.wav", "*.m4a", "*.aac", "*.wma" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            };

            var selectedFiles = await storageProvider.OpenFilePickerAsync(filePickerOptions);

            if (selectedFiles.Count > 0)
            {
                // Fix file paths - ensure they start with '/' on Linux
                var newAudioPaths = selectedFiles
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

                // Sort new files by folder grouping before adding
                var sortedNewPaths = SortByFolderThenFilename(newAudioPaths);

                // Add sorted files to the end of the list
                foreach (var path in sortedNewPaths)
                {
                    var (artist, title, trackNumberTag) = ReadAudioMetadata(path);
                    Tracks.Add(new AudioTrackItem
                    {
                        TrackNumber = Tracks.Count + 1,
                        FileName = Path.GetFileName(path),
                        FilePath = path,
                        Artist = artist,
                        Title = title,
                        TrackNumberTag = trackNumberTag
                    });
                }

                // Update track numbers
                for (int i = 0; i < Tracks.Count; i++)
                {
                    Tracks[i].TrackNumber = i + 1;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    [RelayCommand]
    private void Remove()
    {
        if (SelectedTracks.Count == 0)
            return;

        // Prevent removing all tracks - need at least one
        if (SelectedTracks.Count >= Tracks.Count)
        {
            return;
        }

        var itemsToRemove = SelectedTracks.ToList();

        // Remove selected items
        foreach (var item in itemsToRemove)
        {
            Tracks.Remove(item);
        }

        // Update track numbers
        for (int i = 0; i < Tracks.Count; i++)
        {
            Tracks[i].TrackNumber = i + 1;
        }

        // Clear selection
        SelectedTracks.Clear();
        UpdateButtonStates();
    }

    [RelayCommand]
    private void Encode()
    {
        DialogResult = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }

    public string[] GetSortedFilePaths()
    {
        return Tracks.Select(t => t.FilePath).ToArray();
    }

    /// <summary>
    /// Sorts files by folder first (maintaining folder order), then by ID3 track number within each folder.
    /// Falls back to alphabetical filename sorting if ID3 tags are unavailable or invalid.
    /// This ensures files from the same folder stay grouped together, respecting multi-disc albums.
    /// </summary>
    private string[] SortByFolderThenFilename(string[] paths)
    {
        // Group files by their parent directory
        var groupedByFolder = paths
            .GroupBy(p => Path.GetDirectoryName(p) ?? "")
            .Select((group, index) => new
            {
                FolderPath = group.Key,
                OriginalOrder = index, // Preserve the order folders appear in the input
                Files = SortFilesInFolder(group.ToArray()) // Sort files within folder using ID3 tags
            })
            .OrderBy(g => g.OriginalOrder) // Maintain folder order
            .SelectMany(g => g.Files) // Flatten back to a single list
            .ToArray();

        return groupedByFolder;
    }

    /// <summary>
    /// Sorts files within a folder by track number metadata (supports MP3, FLAC, OGG, M4A, AAC, WMA, WAV),
    /// falling back to filename if metadata is unavailable.
    /// </summary>
    private string[] SortFilesInFolder(string[] filesInFolder)
    {
        try
        {
            // Try to read metadata from all supported audio files
            var fileTuples = filesInFolder.Select(f =>
            {
                uint? trackNumber = null;
                try
                {
                    // TagLibSharp supports: MP3, FLAC, OGG, M4A, AAC, WMA, WAV, and many more
                    using (var file = TagLib.File.Create(f))
                    {
                        trackNumber = file.Tag.Track;
                    }
                }
                catch
                {
                    // Failed to read metadata - will fall back to filename sorting
                }
                return new { FilePath = f, TrackNumber = trackNumber };
            }).ToList();

            // Check if any files have valid track numbers
            var filesWithTracks = fileTuples.Where(t => t.TrackNumber.HasValue && t.TrackNumber.Value > 0).ToList();

            if (filesWithTracks.Count > 0)
            {
                // Sort files with track numbers by track number
                var sortedWithTracks = filesWithTracks.OrderBy(t => t.TrackNumber!.Value).Select(t => t.FilePath).ToList();

                // Files without track numbers are sorted by filename and appended
                var filesWithoutTracks = fileTuples
                    .Where(t => !t.TrackNumber.HasValue || t.TrackNumber.Value == 0)
                    .OrderBy(t => Path.GetFileName(t.FilePath))
                    .Select(t => t.FilePath)
                    .ToList();

                return sortedWithTracks.Concat(filesWithoutTracks).ToArray();
            }
            else
            {
                // No valid track numbers found - fall back to filename sorting
                return filesInFolder.OrderBy(p => Path.GetFileName(p)).ToArray();
            }
        }
        catch
        {
            // Failed to sort by metadata - fall back to filename sorting
            return filesInFolder.OrderBy(p => Path.GetFileName(p)).ToArray();
        }
    }

    /// <summary>
    /// Reads artist, title, and track number metadata from an audio file using TagLibSharp.
    /// Returns (null, null, null) if metadata cannot be read or is empty.
    /// </summary>
    private (string? artist, string? title, uint? trackNumber) ReadAudioMetadata(string filePath)
    {
        try
        {
            // TagLibSharp supports: MP3, FLAC, OGG, M4A, AAC, WMA, WAV, and many more
            using (var file = TagLib.File.Create(filePath))
            {
                var artist = file.Tag.FirstPerformer ?? file.Tag.FirstAlbumArtist;
                var title = file.Tag.Title;
                var trackNumber = file.Tag.Track;

                // Return null if strings are empty or whitespace
                artist = string.IsNullOrWhiteSpace(artist) ? null : artist;
                title = string.IsNullOrWhiteSpace(title) ? null : title;

                // Return null if track number is 0 (not set)
                var trackNumberResult = (trackNumber > 0) ? trackNumber : (uint?)null;

                return (artist, title, trackNumberResult);
            }
        }
        catch
        {
            // Failed to read metadata - return null values
            return (null, null, null);
        }
    }
}

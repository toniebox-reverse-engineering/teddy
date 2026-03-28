using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TonieFile;
using TeddyBench.Avalonia.Models;

namespace TeddyBench.Avalonia.Services;

/// <summary>
/// Service for scanning directories and discovering Tonie files.
/// </summary>
public class DirectoryScanService
{
    private readonly TonieMetadataService _metadataService;
    private readonly LiveFlagService _liveFlagService;

    public DirectoryScanService(TonieMetadataService metadataService, LiveFlagService liveFlagService)
    {
        _metadataService = metadataService;
        _liveFlagService = liveFlagService;
    }

    /// <summary>
    /// Event for progress updates during scanning.
    /// </summary>
    public event EventHandler<string>? ProgressUpdate;

    /// <summary>
    /// Event for requesting image downloads in the background.
    /// </summary>
    public event EventHandler<(string Hash, string PicUrl, string FilePath)>? RequestImageDownload;

    /// <summary>
    /// Checks if a directory looks like a Toniebox SD card root (has CONTENT directory).
    /// If so, returns the path to the CONTENT directory.
    /// </summary>
    public string CheckForContentDirectory(string directory)
    {
        var contentDir = Path.Combine(directory, "CONTENT");
        if (Directory.Exists(contentDir))
        {
            return contentDir;
        }
        return directory;
    }

    /// <summary>
    /// Scans a directory for Tonie files and returns a list of TonieFileItem objects.
    /// </summary>
    public async Task<List<TonieFileItem>> ScanDirectoryAsync(string directory)
    {
        var tonieFiles = new List<TonieFileItem>();

        var dirInfo = new DirectoryInfo(directory);
        ProgressUpdate?.Invoke(this, $"Scanning for Tonie files...");
        await Task.Delay(100);

        int filesProcessed = 0;
        var subdirs = dirInfo.GetDirectories();
        int totalDirs = subdirs.Length;

        // Scan for Tonie files in subdirectories (format: XXXXXXXX/YYYYYYY0304E0)
        foreach (var subDir in subdirs)
        {
            filesProcessed++;
            ProgressUpdate?.Invoke(this, $"Reading Tonie files... ({filesProcessed}/{totalDirs} directories)");

            var files = subDir.GetFiles("*0304E0");
            foreach (var file in files)
            {
                var tonieItem = await ProcessTonieFileAsync(file, subDir.Name, filesProcessed, totalDirs);
                if (tonieItem != null)
                {
                    tonieFiles.Add(tonieItem);
                }
            }
        }

        // Also scan for renamed files in current directory
        var renamedFiles = dirInfo.GetFiles().Where(f =>
        {
            var match = Regex.Match(f.Name, @"(?<prod>[0-9]{8}|[0-9]{2}-[0-9]{4}) - [0-9A-F]{8} - (?<name>.*)");
            return match.Success;
        });

        foreach (var file in renamedFiles)
        {
            tonieFiles.Add(new TonieFileItem
            {
                FileName = file.Name,
                FilePath = file.FullName,
                DisplayName = file.Name,
                InfoText = $"Size: {file.Length / 1024} KB"
            });
        }

        ProgressUpdate?.Invoke(this, $"Scan complete: Found {tonieFiles.Count} Tonie file(s)");
        return tonieFiles;
    }

    private async Task<TonieFileItem?> ProcessTonieFileAsync(FileInfo file, string directoryName, int filesProcessed, int totalDirs)
    {
        // Try to get metadata for this Tonie
        string title = $"{directoryName}/{file.Name}";
        string? imagePath = null;
        string hash = string.Empty;
        bool isLive = false;
        bool isKnownTonie = false;
        bool isCustomTonie = false;

        // Report progress: Reading file header
        ProgressUpdate?.Invoke(this, $"Reading {directoryName}/{file.Name}... ({filesProcessed}/{totalDirs})");
        await Task.Delay(1); // Brief delay to ensure UI updates

        try
        {
            // First, read just the header (fast: ~1ms)
            // Only read header for fast scanning (no full audio data)
            var audioHeader = TonieAudio.FromFile(file.FullName, false);
            hash = BitConverter.ToString(audioHeader.Header.Hash).Replace("-", "");
            var audioId = audioHeader.Header.AudioId;

            // Get metadata without calculating tracks (tracks will be populated on-demand during playback)
            var (metaTitle, metaImage, metaIsCustom) = _metadataService.GetTonieInfo(hash, directoryName, audioId, null);

            if (!string.IsNullOrEmpty(metaTitle) && metaTitle != "Unknown Tonie")
            {
                title = metaTitle;
                imagePath = metaImage;
                isCustomTonie = metaIsCustom;
                isKnownTonie = !metaIsCustom; // Official tonies are not custom tonies

                // If image is not cached, try to download it asynchronously
                if (imagePath == null && !isCustomTonie)
                {
                    var picUrl = _metadataService.GetPicUrl(hash);
                    if (!string.IsNullOrEmpty(picUrl))
                    {
                        // Request download in background
                        RequestImageDownload?.Invoke(this, (hash, picUrl, file.FullName));
                    }
                }
            }
        }
        catch
        {
            // If we can't read the file, just use the filename
        }

        // Only check LIVE flag for custom/unknown tonies (not in database)
        // Official tonies from the database never have the LIVE flag
        if (!isKnownTonie)
        {
            // Show more detailed progress for LIVE flag check
            var displayFileName = title != $"{directoryName}/{file.Name}" ? title : file.Name;
            ProgressUpdate?.Invoke(this, $"Checking attributes: {displayFileName}... ({filesProcessed}/{totalDirs})");
            await Task.Delay(1); // Brief delay to ensure UI updates
            isLive = await _liveFlagService.GetHiddenAttributeAsync(file.FullName);
        }

        // Add [LIVE] prefix if file has Hidden attribute
        var displayTitle = isLive ? $"[LIVE] {title}" : title;

        return new TonieFileItem
        {
            FileName = file.Name,
            FilePath = file.FullName,
            DisplayName = displayTitle,
            DirectoryName = directoryName,
            InfoText = $"Size: {file.Length / 1024} KB",
            ImagePath = imagePath,
            IsLive = isLive,
            IsCustomTonie = isCustomTonie,
            Hash = hash
        };
    }

    /// <summary>
    /// Formats duration in seconds to a human-readable string.
    /// Format: m:ss for durations < 1 hour, h:mm:ss for longer durations
    /// </summary>
    private string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);

        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
        else
        {
            return $"{ts.Minutes}:{ts.Seconds:D2}";
        }
    }
}
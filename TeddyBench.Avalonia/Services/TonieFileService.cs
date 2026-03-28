using System;
using System.IO;
using System.Linq;
using TonieFile;
using TagLib;

namespace TeddyBench.Avalonia.Services;

/// <summary>
/// Service for Tonie file operations (encode, decode, get info).
/// </summary>
public class TonieFileService
{
    /// <summary>
    /// Decodes a Tonie file to audio files in the specified directory.
    /// </summary>
    public void DecodeTonieFile(string tonieFilePath, string outputDirectory, string fileName)
    {
        var audio = TonieAudio.FromFile(tonieFilePath);
        audio.DumpAudioFiles(outputDirectory, fileName, false, Array.Empty<string>(), null);
    }

    /// <summary>
    /// Gets information about a Tonie file.
    /// </summary>
    public TonieFileInfo GetTonieFileInfo(string tonieFilePath)
    {
        var audio = TonieAudio.FromFile(tonieFilePath, false);

        return new TonieFileInfo
        {
            AudioId = audio.Header.AudioId,
            AudioLength = audio.Header.AudioLength,
            ChapterCount = audio.Header.AudioChapters.Length,
            Hash = BitConverter.ToString(audio.Header.Hash).Replace("-", ""),
            HashValid = audio.HashCorrect
        };
    }

    /// <summary>
    /// Encodes audio files into a custom Tonie file.
    /// </summary>
    /// <param name="audioPaths">Audio file paths in the desired order</param>
    /// <param name="audioId">Audio ID (from RFID)</param>
    /// <param name="bitRate">Bitrate in kbps (default 96)</param>
    /// <param name="callback">Optional callback for progress reporting</param>
    /// <returns>The encoded Tonie file content and hash</returns>
    public (byte[] FileContent, string Hash) EncodeCustomTonie(string[] audioPaths, uint audioId, int bitRate = 96, TonieAudio.EncodeCallback? callback = null)
    {
        bool useVbr = false;
        TonieAudio generated = new TonieAudio(audioPaths, audioId, bitRate * 1000, useVbr, null, callback);

        // Get the hash for customTonies.json
        string hash = BitConverter.ToString(generated.Header.Hash).Replace("-", "");

        return (generated.FileContent, hash);
    }

    /// <summary>
    /// Determines the source folder name from audio file paths (for custom Tonie naming).
    /// Priority: Album Artist - Album Title, then Artist - Album Title, then Album Title, then folder name.
    /// </summary>
    public string GetSourceFolderName(string[] audioPaths)
    {
        // Try to read metadata from audio files
        foreach (var audioPath in audioPaths)
        {
            var (albumArtist, artist, albumTitle) = ReadAlbumMetadata(audioPath);

            // Priority 1: Album Artist - Album Title
            if (!string.IsNullOrWhiteSpace(albumArtist) && !string.IsNullOrWhiteSpace(albumTitle))
            {
                return $"{albumArtist} - {albumTitle}";
            }

            // Priority 2: Artist - Album Title
            if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(albumTitle))
            {
                return $"{artist} - {albumTitle}";
            }

            // Priority 3: Album Title only
            if (!string.IsNullOrWhiteSpace(albumTitle))
            {
                return albumTitle;
            }
        }

        // Fallback: Use folder name with most files, alphabetically first on tie
        var folderGroups = audioPaths
            .Select(p => Path.GetDirectoryName(p))
            .Where(d => !string.IsNullOrEmpty(d))
            .GroupBy(d => d)
            .Select(g => new { FolderPath = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => Path.GetFileName(x.FolderPath))
            .ToList();

        return folderGroups.Count > 0
            ? Path.GetFileName(folderGroups[0].FolderPath) ?? "Custom Tonie"
            : "Custom Tonie";
    }

    /// <summary>
    /// Reads album-level metadata from an audio file using TagLibSharp.
    /// Returns (null, null, null) if metadata cannot be read or is empty.
    /// </summary>
    private (string? albumArtist, string? artist, string? albumTitle) ReadAlbumMetadata(string filePath)
    {
        try
        {
            using (var file = TagLib.File.Create(filePath))
            {
                var albumArtist = file.Tag.FirstAlbumArtist;
                var artist = file.Tag.FirstPerformer;
                var albumTitle = file.Tag.Album;

                // Return null if strings are empty or whitespace
                albumArtist = string.IsNullOrWhiteSpace(albumArtist) ? null : albumArtist;
                artist = string.IsNullOrWhiteSpace(artist) ? null : artist;
                albumTitle = string.IsNullOrWhiteSpace(albumTitle) ? null : albumTitle;

                return (albumArtist, artist, albumTitle);
            }
        }
        catch
        {
            // Failed to read metadata - return null values
            return (null, null, null);
        }
    }

    /// <summary>
    /// Reverses byte order of a hex UID string.
    /// Example: "0EED5104" -> "0451ED0E"
    /// </summary>
    public string ReverseUidBytes(string uid)
    {
        string reversed = "";
        for (int i = uid.Length - 2; i >= 0; i -= 2)
        {
            reversed += uid.Substring(i, 2);
        }
        return reversed;
    }
}

/// <summary>
/// Information about a Tonie file.
/// </summary>
public class TonieFileInfo
{
    public uint AudioId { get; set; }
    public int AudioLength { get; set; }
    public int ChapterCount { get; set; }
    public string Hash { get; set; } = string.Empty;
    public bool HashValid { get; set; }
}
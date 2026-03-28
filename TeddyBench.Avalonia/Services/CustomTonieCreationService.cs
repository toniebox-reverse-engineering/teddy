using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TeddyBench.Avalonia.Services;

/// <summary>
/// Service for creating custom Tonie files.
/// Handles RFID validation, encoding, and file system operations.
/// </summary>
public class CustomTonieCreationService
{
    private readonly TonieFileService _tonieFileService;
    private readonly TonieMetadataService _metadataService;
    private static uint _lastGeneratedTimestamp = 0;
    private static readonly object _timestampLock = new object();

    public CustomTonieCreationService(TonieFileService tonieFileService, TonieMetadataService metadataService)
    {
        _tonieFileService = tonieFileService;
        _metadataService = metadataService;
    }

    /// <summary>
    /// Validates an RFID UID input string.
    /// </summary>
    public (bool IsValid, string ErrorMessage) ValidateRfidUid(string uidInput)
    {
        if (string.IsNullOrWhiteSpace(uidInput))
        {
            return (false, "RFID UID is required (must match RFID tag on figurine)");
        }

        if (uidInput.Length != 8 || !Regex.IsMatch(uidInput, "^[0-9A-F]{8}$"))
        {
            return (false, "RFID UID must be exactly 8 hexadecimal characters");
        }

        return (true, string.Empty);
    }


    /// <summary>
    /// Creates a custom Tonie file and saves it to the specified directory.
    /// If audioId is null, the Audio ID is automatically set to Unix timestamp minus 0x50000000 (custom tonie offset).
    /// </summary>
    /// <param name="targetDirectory">Base directory (typically CONTENT folder)</param>
    /// <param name="reversedUid">The reversed RFID UID for the directory name</param>
    /// <param name="sortedAudioPaths">Sorted audio file paths</param>
    /// <param name="originalUid">The original UID for metadata (user-entered format)</param>
    /// <param name="audioId">Optional audio ID. If null, uses Unix timestamp of file creation.</param>
    /// <param name="callback">Optional callback for progress reporting</param>
    /// <returns>The generated hash and target file path</returns>
    public (string Hash, string FilePath) CreateCustomTonieFile(
        string targetDirectory,
        string reversedUid,
        string[] sortedAudioPaths,
        string originalUid,
        uint? audioId = null,
        TonieFile.TonieAudio.EncodeCallback? callback = null)
    {
        // Create directory structure
        // Directory: reversedUid (e.g., "0451ED0E"), File: "500304E0" (constant suffix)
        string dirName = reversedUid;
        string fileName = "500304E0";

        string targetDir = Path.Combine(targetDirectory, dirName);
        Directory.CreateDirectory(targetDir);

        string targetFile = Path.Combine(targetDir, fileName);

        // Determine the Audio ID
        uint finalAudioId;
        if (audioId.HasValue)
        {
            // Use the provided Audio ID
            finalAudioId = audioId.Value;
        }
        else
        {
            // Generate unique timestamp-based Audio ID for custom tonies
            // Custom tonies use timestamps offset by -0x50000000 to distinguish them from official tonies
            // Use a lock and counter to ensure uniqueness when multiple calls happen in the same second
            lock (_timestampLock)
            {
                uint currentTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Apply the custom tonie offset
                uint offsetTimestamp = currentTimestamp - 0x50000000;

                // Ensure we never generate the same timestamp twice
                if (offsetTimestamp <= _lastGeneratedTimestamp)
                {
                    // If current timestamp is same or earlier, increment by 1
                    finalAudioId = _lastGeneratedTimestamp + 1;
                }
                else
                {
                    finalAudioId = offsetTimestamp;
                }

                _lastGeneratedTimestamp = finalAudioId;
            }
        }

        // Encode the audio files with the determined Audio ID
        var (fileContent, hash) = _tonieFileService.EncodeCustomTonie(sortedAudioPaths, finalAudioId, 96, callback);

        // Write the final encoded content
        File.WriteAllBytes(targetFile, fileContent);

        return (hash, targetFile);
    }

    /// <summary>
    /// Registers a custom Tonie in the metadata database.
    /// </summary>
    /// <param name="hash">The hash of the Tonie file</param>
    /// <param name="sourceFolderName">The source folder name to use as title</param>
    /// <param name="originalUid">The original RFID UID (user-entered format)</param>
    /// <param name="audioId">The audio ID</param>
    /// <param name="trackPaths">Array of track file paths</param>
    /// <param name="directory">The directory name where the tonie is stored (e.g., "EA33ED0E")</param>
    /// <param name="tonieFilePath">Path to the created Tonie file (for calculating duration)</param>
    public void RegisterCustomTonie(string hash, string sourceFolderName, string originalUid, uint audioId, string[] trackPaths, string directory, string tonieFilePath)
    {
        string customTitle = $"{sourceFolderName} [RFID: {originalUid}]";

        // Extract track names from file paths
        var tracks = trackPaths.Select(path => System.IO.Path.GetFileNameWithoutExtension(path)).ToList();

        _metadataService.AddCustomTonie(hash, customTitle, audioId, tracks, directory);
    }
}

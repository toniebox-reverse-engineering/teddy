using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TeddyBench.Avalonia.Models;
using TonieFile;

namespace TeddyBench.Avalonia.Services
{
    public class TrashcanService
    {
        private readonly TonieMetadataService _metadataService;
        private static uint _lastGeneratedTimestamp = 0;
        private static readonly object _timestampLock = new object();

        public TrashcanService(TonieMetadataService metadataService)
        {
            _metadataService = metadataService;
        }

        /// <summary>
        /// Scans the TRASHCAN directory for deleted Tonie files.
        /// </summary>
        /// <param name="sdCardPath">Path to the SD card root</param>
        /// <returns>List of deleted Tonie items</returns>
        public async Task<List<DeletedTonieItem>> ScanTrashcanAsync(string sdCardPath)
        {
            var deletedTonies = new List<DeletedTonieItem>();

            try
            {
                var trashcanPath = Path.Combine(sdCardPath, "TRASHCAN");

                if (!Directory.Exists(trashcanPath))
                {
                    return deletedTonies;
                }

                // Get all subdirectories in TRASHCAN
                var subdirs = Directory.GetDirectories(trashcanPath);

                foreach (var subdir in subdirs)
                {
                    var dirName = Path.GetFileName(subdir);

                    // Get all .043 files in this directory
                    var files = Directory.GetFiles(subdir, "*.043");

                    foreach (var file in files)
                    {
                        try
                        {
                            var deletedTonie = await ParseDeletedTonieAsync(file, dirName);
                            if (deletedTonie != null)
                            {
                                deletedTonies.Add(deletedTonie);
                            }
                        }
                        catch
                        {
                            // Skip files that can't be parsed
                        }
                    }
                }
            }
            catch
            {
                // Return empty list on error
            }

            return deletedTonies;
        }

        /// <summary>
        /// Parses a single deleted Tonie file from TRASHCAN.
        /// </summary>
        private async Task<DeletedTonieItem?> ParseDeletedTonieAsync(string filePath, string trashcanDirectory)
        {
            try
            {
                // Parse the Tonie file
                var tonie = await Task.Run(() => TonieAudio.FromFile(filePath, false));

                // Get deletion date from file modification time
                var fileInfo = new FileInfo(filePath);
                var deletionDate = fileInfo.LastWriteTime;

                // Get metadata
                var hash = BitConverter.ToString(tonie.Header.Hash).Replace("-", "");
                var audioId = tonie.Header.AudioId;
                var (title, imagePath, isCustom) = _metadataService.GetTonieInfo(hash, rfidFolder: null, audioId);

                // Get directory from customTonies.json metadata if available
                // The Toniebox uses an internal UID for TRASHCAN that's different from the RFID tag UID
                // We store the actual directory name in customTonies.json for reliable restoration
                string uid = "Unknown";
                if (isCustom)
                {
                    var metadata = _metadataService.GetCustomTonieMetadata(hash);
                    if (metadata != null && !string.IsNullOrEmpty(metadata.Directory))
                    {
                        // Use the stored directory name (e.g., "EA33ED0E")
                        // Reverse it to get user-facing RFID format (e.g., "0EED33EA")
                        uid = ReverseByteOrder(metadata.Directory);
                    }
                    else
                    {
                        // Fallback: Try to extract RFID from title if Directory field is not present
                        // This handles old entries that were created before the Directory field was added
                        var rfidMatch = System.Text.RegularExpressions.Regex.Match(
                            title,
                            @"\[RFID:\s*([0-9A-F]{8})\]",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        );
                        if (rfidMatch.Success)
                        {
                            uid = rfidMatch.Groups[1].Value.ToUpperInvariant();
                        }
                    }
                }

                // Calculate duration using CalculateStatistics
                tonie.CalculateStatistics(out _, out _, out _, out _, out _, out _, out ulong highestGranule);
                var totalSeconds = highestGranule / 48000.0;
                var duration = FormatDuration(totalSeconds);

                var deletedTonie = new DeletedTonieItem
                {
                    FilePath = filePath,
                    DisplayName = title,
                    Uid = uid,
                    DeletionDate = deletionDate,
                    Hash = hash,
                    ImagePath = imagePath,
                    AudioId = $"0x{tonie.Header.AudioId:X8}",
                    Duration = duration,
                    TrackCount = tonie.Header.AudioChapters?.Length ?? 0,
                    IsCustomTonie = isCustom,
                    TrashcanDirectory = trashcanDirectory
                };

                return deletedTonie;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Formats duration in seconds to a human-readable string.
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

        /// <summary>
        /// Checks if a hash already exists in CONTENT and returns the existing file's location.
        /// Scans all files in CONTENT directory to find matching hash.
        /// </summary>
        /// <param name="hash">The hash to check</param>
        /// <param name="sdCardPath">Path to the SD card root</param>
        /// <returns>Tuple of (exists, rfidUid, filePath) - rfidUid and filePath are null if not found</returns>
        public (bool exists, string? rfidUid, string? filePath) CheckHashConflict(string hash, string sdCardPath)
        {
            hash = hash.ToUpperInvariant();

            // First try customTonies.json (fast path)
            var metadata = _metadataService.GetCustomTonieMetadata(hash);
            if (metadata != null && !string.IsNullOrEmpty(metadata.Directory))
            {
                // Check if the file physically exists at the stored directory
                var contentPath = Path.Combine(sdCardPath, "CONTENT", metadata.Directory);
                var filePath = Path.Combine(contentPath, "500304E0");

                if (File.Exists(filePath))
                {
                    // File exists - extract RFID from directory (reverse byte order)
                    var rfidUid = ReverseByteOrder(metadata.Directory);
                    return (true, rfidUid, filePath);
                }
            }

            // Fallback: Scan all files in CONTENT directory to find matching hash
            // This handles cases where file exists but not in customTonies.json yet
            var contentDir = Path.Combine(sdCardPath, "CONTENT");
            if (!Directory.Exists(contentDir))
            {
                return (false, null, null);
            }

            foreach (var rfidDir in Directory.GetDirectories(contentDir))
            {
                var tonieFile = Path.Combine(rfidDir, "500304E0");
                if (File.Exists(tonieFile))
                {
                    try
                    {
                        // Read just the header to get the hash
                        var tonie = TonieAudio.FromFile(tonieFile, false);
                        var fileHash = BitConverter.ToString(tonie.Header.Hash).Replace("-", "").ToUpperInvariant();

                        if (fileHash == hash)
                        {
                            // Found matching hash - extract RFID from directory name
                            var dirName = Path.GetFileName(rfidDir);
                            var rfidUid = ReverseByteOrder(dirName);
                            return (true, rfidUid, tonieFile);
                        }
                    }
                    catch
                    {
                        // Skip files we can't read
                        continue;
                    }
                }
            }

            return (false, null, null);
        }

        /// <summary>
        /// Restores a deleted Tonie from TRASHCAN back to CONTENT.
        /// </summary>
        /// <param name="deletedTonie">The deleted Tonie to restore</param>
        /// <param name="sdCardPath">Path to the SD card root</param>
        /// <param name="allowOverwrite">If true, overwrites existing file at target location</param>
        /// <returns>True if restore was successful</returns>
        public async Task<(bool success, string message)> RestoreTonieAsync(DeletedTonieItem deletedTonie, string sdCardPath, bool allowOverwrite = false)
        {
            try
            {
                // FIRST: Check if we know where to restore this file
                if (deletedTonie.Uid == "Unknown")
                {
                    // We don't know the RFID directory, need user input
                    return (false, "MISSING_METADATA");
                }

                // SECOND: Determine if this is a custom tonie that needs re-encoding
                // Strategy: Check if hash exists in tonies.json (official database)
                var officialTonie = _metadataService.GetTonieByHash(deletedTonie.Hash);
                bool isOfficialTonie = officialTonie != null;

                // For custom tonies, check if we have the original Audio ID
                if (!isOfficialTonie)
                {
                    var metadata = _metadataService.GetCustomTonieMetadata(deletedTonie.Hash);
                    bool hasOriginalAudioId = metadata != null && metadata.AudioId != null && metadata.AudioId.Count > 0;

                    // If it's not official and we don't have the Audio ID, we need to re-encode
                    if (!hasOriginalAudioId)
                    {
                        return (false, "MISSING_METADATA");
                    }
                }

                // THIRD: Check for hash conflict (same audio content already exists)
                var (hashExists, existingRfid, existingFilePath) = CheckHashConflict(deletedTonie.Hash, sdCardPath);
                if (hashExists && existingFilePath != null && existingRfid != null)
                {
                    // Hash conflict: Same audio content exists somewhere
                    // Always show hash conflict dialog, even if it's at the same location
                    // Reason: User might have manually placed a file there and wants options
                    return (false, $"HASH_CONFLICT:{existingRfid}");
                }

                // Calculate the reversed UID for the directory name
                var reversedUid2 = ReverseByteOrder(deletedTonie.Uid);

                // Create target directory path
                var contentPath = Path.Combine(sdCardPath, "CONTENT", reversedUid2);
                var targetFilePath = Path.Combine(contentPath, "500304E0");

                // Check if target already exists (file path conflict, not hash conflict)
                if (File.Exists(targetFilePath) && !allowOverwrite)
                {
                    return (false, $"CONFLICT:{reversedUid2}");
                }

                // Create directory if it doesn't exist
                if (!Directory.Exists(contentPath))
                {
                    Directory.CreateDirectory(contentPath);
                }

                // Copy the file (don't delete from TRASHCAN yet, safer to keep backup)
                // Use overwrite parameter to control behavior
                await Task.Run(() => File.Copy(deletedTonie.FilePath, targetFilePath, allowOverwrite));

                // Restore original Audio ID if it's a custom tonie with known AudioID
                if (deletedTonie.IsCustomTonie)
                {
                    // Get metadata from customTonies.json
                    var tonieMetadata = _metadataService.GetCustomTonieMetadata(deletedTonie.Hash);

                    if (tonieMetadata != null && tonieMetadata.AudioId != null && tonieMetadata.AudioId.Count > 0)
                    {
                        // Parse the original Audio ID from customTonies.json
                        // Try decimal first (current format), then hex (legacy format)
                        uint originalAudioId;
                        bool parsed = uint.TryParse(tonieMetadata.AudioId[0], out originalAudioId);
                        if (!parsed)
                        {
                            // Try parsing as hex (legacy format from older versions)
                            parsed = uint.TryParse(tonieMetadata.AudioId[0], System.Globalization.NumberStyles.HexNumber, null, out originalAudioId);
                        }

                        if (parsed)
                        {
                            // Read the restored file with audio data
                            var restoredTonie = await Task.Run(() => TonieAudio.FromFile(targetFilePath, readAudio: true));

                            // Update the stream serial number (Audio ID) in the Ogg audio data
                            // This properly updates both the header AND the Ogg stream serial number
                            // resetGranulePositions = false because we want to preserve the original granule positions
                            byte[] updatedAudioData = await Task.Run(() => restoredTonie.UpdateStreamSerialNumber(originalAudioId, resetGranulePositions: false));

                            // Create a new TonieAudio with the updated audio data
                            var correctedTonie = new TonieAudio();
                            correctedTonie.Audio = updatedAudioData;
                            correctedTonie.Header.AudioLength = updatedAudioData.Length;
                            correctedTonie.Header.AudioId = originalAudioId;
                            correctedTonie.Header.AudioChapters = restoredTonie.Header.AudioChapters;

                            // Compute hash of the updated audio data
                            using var sha1 = System.Security.Cryptography.SHA1.Create();
                            correctedTonie.Header.Hash = sha1.ComputeHash(updatedAudioData);

                            // Build the file content (header + audio)
                            correctedTonie.FileContent = new byte[updatedAudioData.Length + 0x1000];
                            Array.Copy(updatedAudioData, 0, correctedTonie.FileContent, 0x1000, updatedAudioData.Length);
                            correctedTonie.UpdateFileContent();

                            // Write the file back with the corrected Audio ID in both header and Ogg stream
                            await Task.Run(() => File.WriteAllBytes(targetFilePath, correctedTonie.FileContent));
                        }
                    }
                }

                // Delete from TRASHCAN after successful restoration
                try
                {
                    File.Delete(deletedTonie.FilePath);

                    // Try to remove empty directory
                    var trashcanDir = Path.GetDirectoryName(deletedTonie.FilePath);
                    if (trashcanDir != null && Directory.Exists(trashcanDir))
                    {
                        var remainingFiles = Directory.GetFiles(trashcanDir);
                        if (remainingFiles.Length == 0)
                        {
                            Directory.Delete(trashcanDir);
                        }
                    }
                }
                catch
                {
                    // If deletion fails, don't fail the whole restoration
                    // The file was successfully restored, deletion is just cleanup
                }

                return (true, $"Successfully restored to {reversedUid2}/500304E0");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to restore: {ex.Message}");
            }
        }

        /// <summary>
        /// Moves a Tonie file from one RFID location to another.
        /// Updates the directory field in customTonies.json.
        /// </summary>
        /// <param name="hash">The hash of the Tonie to move</param>
        /// <param name="oldRfidUid">Current RFID UID (user format, e.g., "0EED4242")</param>
        /// <param name="newRfidUid">New RFID UID (user format, e.g., "0EED5151")</param>
        /// <param name="sdCardPath">Path to the SD card root</param>
        /// <returns>Success status and message</returns>
        public async Task<(bool success, string message)> MoveTonieToNewRfidAsync(
            string hash, string oldRfidUid, string newRfidUid, string sdCardPath)
        {
            try
            {
                // Convert UIDs to directory format (reversed byte order)
                var oldReversedUid = ReverseByteOrder(oldRfidUid);
                var newReversedUid = ReverseByteOrder(newRfidUid);

                // Build paths
                var oldPath = Path.Combine(sdCardPath, "CONTENT", oldReversedUid, "500304E0");
                var newDirPath = Path.Combine(sdCardPath, "CONTENT", newReversedUid);
                var newPath = Path.Combine(newDirPath, "500304E0");

                // Verify source file exists
                if (!File.Exists(oldPath))
                {
                    return (false, $"Source file not found at {oldReversedUid}/500304E0");
                }

                // Check if target already exists
                if (File.Exists(newPath))
                {
                    return (false, $"Target location {newReversedUid}/500304E0 already exists");
                }

                // Create target directory
                if (!Directory.Exists(newDirPath))
                {
                    Directory.CreateDirectory(newDirPath);
                }

                // Move the file
                await Task.Run(() => File.Move(oldPath, newPath));

                // Try to remove old empty directory
                var oldDirPath = Path.Combine(sdCardPath, "CONTENT", oldReversedUid);
                if (Directory.Exists(oldDirPath))
                {
                    var remainingFiles = Directory.GetFiles(oldDirPath);
                    if (remainingFiles.Length == 0)
                    {
                        Directory.Delete(oldDirPath);
                    }
                }

                // Update customTonies.json to point to new directory
                var metadata = _metadataService.GetCustomTonieMetadata(hash);
                if (metadata != null)
                {
                    metadata.Directory = newReversedUid;
                    _metadataService.UpdateCustomTonie(hash, metadata);
                }

                return (true, $"Successfully moved to {newReversedUid}/500304E0");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to move file: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores a deleted Tonie by updating its Audio ID without re-encoding.
        /// This preserves the original audio encoding and produces deterministic hashes.
        /// Used when the original Audio ID is unknown (e.g., from another user's SD card).
        /// </summary>
        /// <param name="deletedTonie">The deleted Tonie to restore</param>
        /// <param name="sdCardPath">Path to the SD card root</param>
        /// <param name="newRfidUid">New RFID UID (user format)</param>
        /// <param name="customTitle">Optional custom title for customTonies.json</param>
        /// <param name="audioId">Optional Audio ID (if null, generates from timestamp - 0x50000000)</param>
        /// <returns>Success status and message</returns>
        public async Task<(bool success, string message)> RestoreAsNewCustomTonieAsync(
            DeletedTonieItem deletedTonie,
            string sdCardPath,
            string newRfidUid,
            string? customTitle = null,
            uint? audioId = null)
        {
            try
            {
                // Generate Audio ID if not provided (custom tonie format: timestamp - 0x50000000)
                // Use a lock and counter to ensure uniqueness when multiple calls happen in the same second
                uint finalAudioId;
                if (audioId.HasValue)
                {
                    finalAudioId = audioId.Value;
                }
                else
                {
                    lock (_timestampLock)
                    {
                        uint currentTimestamp = (uint)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 0x50000000);

                        // Ensure we never generate the same timestamp twice
                        if (currentTimestamp <= _lastGeneratedTimestamp)
                        {
                            // If current timestamp is same or earlier, increment by 1
                            finalAudioId = _lastGeneratedTimestamp + 1;
                        }
                        else
                        {
                            finalAudioId = currentTimestamp;
                        }

                        _lastGeneratedTimestamp = finalAudioId;
                    }
                }

                // Calculate reversed UID for directory name
                var reversedUid = ReverseByteOrder(newRfidUid);

                // Create target directory path
                var contentPath = Path.Combine(sdCardPath, "CONTENT", reversedUid);
                var targetFilePath = Path.Combine(contentPath, "500304E0");

                // Check if target already exists
                if (File.Exists(targetFilePath))
                {
                    return (false, $"Target location {reversedUid}/500304E0 already exists");
                }

                // Create directory if it doesn't exist
                if (!Directory.Exists(contentPath))
                {
                    Directory.CreateDirectory(contentPath);
                }

                // Load the original Tonie file from TRASHCAN with audio data
                var originalTonie = await Task.Run(() => TonieAudio.FromFile(deletedTonie.FilePath, readAudio: true));

                // Update the stream serial number (Audio ID) in the audio data without re-encoding
                // This preserves the exact audio encoding, making hashes deterministic
                // Note: resetGranulePositions = false because the tonie already has proper granule positions
                byte[] updatedAudioData = await Task.Run(() => originalTonie.UpdateStreamSerialNumber(finalAudioId, resetGranulePositions: false));

                // Create a new TonieAudio with the updated audio data
                var newTonie = new TonieAudio();
                newTonie.Audio = updatedAudioData;
                newTonie.Header.AudioLength = updatedAudioData.Length;
                newTonie.Header.AudioId = finalAudioId;
                newTonie.Header.AudioChapters = originalTonie.Header.AudioChapters;

                // Compute hash of the updated audio data
                using var sha1 = System.Security.Cryptography.SHA1.Create();
                newTonie.Header.Hash = sha1.ComputeHash(updatedAudioData);

                // Build the file content (header + audio)
                newTonie.FileContent = new byte[updatedAudioData.Length + 0x1000];
                Array.Copy(updatedAudioData, 0, newTonie.FileContent, 0x1000, updatedAudioData.Length);
                newTonie.UpdateFileContent();

                // Write the new Tonie file
                await Task.Run(() => File.WriteAllBytes(targetFilePath, newTonie.FileContent));

                // Calculate hash for customTonies.json
                var newHash = BitConverter.ToString(newTonie.Header.Hash).Replace("-", "");

                // Determine title for customTonies.json
                string tonieTitle = customTitle ?? deletedTonie.DisplayName;

                // Add RFID to title if not already present
                if (!tonieTitle.Contains("[RFID:"))
                {
                    tonieTitle = $"{tonieTitle} [RFID: {newRfidUid}]";
                }

                // Get track names (if available from original file)
                List<string>? tracks = null;
                var originalMetadata = _metadataService.GetCustomTonieMetadata(deletedTonie.Hash);
                if (originalMetadata != null && originalMetadata.Tracks != null && originalMetadata.Tracks.Count > 0)
                {
                    tracks = originalMetadata.Tracks;
                }

                // Register in customTonies.json
                _metadataService.AddCustomTonie(newHash, tonieTitle, finalAudioId, tracks, reversedUid);

                // Delete from TRASHCAN after successful restoration
                try
                {
                    File.Delete(deletedTonie.FilePath);

                    // Try to remove empty directory
                    var trashcanDir = Path.GetDirectoryName(deletedTonie.FilePath);
                    if (trashcanDir != null && Directory.Exists(trashcanDir))
                    {
                        var remainingFiles = Directory.GetFiles(trashcanDir);
                        if (remainingFiles.Length == 0)
                        {
                            Directory.Delete(trashcanDir);
                        }
                    }
                }
                catch
                {
                    // If deletion fails, don't fail the whole restoration
                }

                return (true, $"Successfully restored as new custom Tonie to {reversedUid}/500304E0");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to restore with updated Audio ID: {ex.Message}");
            }
        }

        /// <summary>
        /// Permanently deletes a file from TRASHCAN.
        /// </summary>
        public async Task<(bool success, string message)> PermanentlyDeleteAsync(DeletedTonieItem deletedTonie)
        {
            try
            {
                await Task.Run(() => File.Delete(deletedTonie.FilePath));

                // Try to remove empty directory
                var directory = Path.GetDirectoryName(deletedTonie.FilePath);
                if (directory != null && Directory.Exists(directory))
                {
                    var remainingFiles = Directory.GetFiles(directory);
                    if (remainingFiles.Length == 0)
                    {
                        Directory.Delete(directory);
                    }
                }

                return (true, "File permanently deleted from TRASHCAN");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to delete: {ex.Message}");
            }
        }

        /// <summary>
        /// Reverses byte order of a hex string.
        /// Example: "04AE797906E4" -> "E4067979AE04"
        /// </summary>
        private string ReverseByteOrder(string hexString)
        {
            if (string.IsNullOrEmpty(hexString) || hexString.Length % 2 != 0)
            {
                return hexString;
            }

            string reversed = "";
            for (int i = hexString.Length - 2; i >= 0; i -= 2)
            {
                reversed += hexString.Substring(i, 2);
            }
            return reversed;
        }
    }
}
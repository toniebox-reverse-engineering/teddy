using System;
using System.Collections.Generic;
using System.Linq;
using TonieFile;

namespace TeddyBench.Avalonia.Services;

/// <summary>
/// Service for populating and caching track information for Tonie files.
/// Reads full audio data to calculate track durations and saves to customTonies.json.
/// </summary>
public class TonieTrackInfoService
{
    private readonly TonieMetadataService _metadataService;

    public TonieTrackInfoService(TonieMetadataService metadataService)
    {
        _metadataService = metadataService;
    }

    /// <summary>
    /// Ensures track information is populated for a Tonie file.
    /// If track info is already saved in customTonies.json, returns immediately.
    /// Otherwise, reads the full audio data, calculates tracks, and saves to customTonies.json.
    /// </summary>
    /// <param name="tonieFilePath">Path to the Tonie file</param>
    /// <param name="hash">Hash of the Tonie (if already known, to avoid recalculation)</param>
    /// <returns>List of track names with durations (e.g., "Track 01 - 2:45")</returns>
    public List<string> EnsureTrackInfo(string tonieFilePath, string? hash = null)
    {
        // Read header if hash not provided
        if (string.IsNullOrEmpty(hash))
        {
            var audioHeader = TonieAudio.FromFile(tonieFilePath, false);
            hash = BitConverter.ToString(audioHeader.Header.Hash).Replace("-", "");
        }

        // Check if track info already exists
        var existingMetadata = _metadataService.GetCustomTonieMetadata(hash);
        if (existingMetadata != null && existingMetadata.Tracks != null && existingMetadata.Tracks.Count > 0)
        {
            // Track info already populated
            return existingMetadata.Tracks;
        }

        // Track info doesn't exist - read full audio and calculate
        var tracks = CalculateTrackInfo(tonieFilePath);

        // Save to customTonies.json if this is a custom tonie
        if (existingMetadata != null && tracks.Count > 0)
        {
            existingMetadata.Tracks = tracks;
            _metadataService.UpdateCustomTonie(hash, existingMetadata);
        }

        return tracks;
    }

    /// <summary>
    /// Calculates track information by reading the full audio data.
    /// </summary>
    private List<string> CalculateTrackInfo(string tonieFilePath)
    {
        var trackList = new List<string>();

        try
        {
            var audio = TonieAudio.FromFile(tonieFilePath, true);

            if (audio.Header.AudioChapters.Length > 0)
            {
                var positions = audio.ParsePositions();

                // Calculate duration for each track
                for (int i = 0; i < audio.Header.AudioChapters.Length; i++)
                {
                    // positions[0] is always 0, positions[1..N] are chapter starts, positions[N+1] is end
                    ulong startGranule = positions[i + 1];
                    ulong endGranule = i + 2 < positions.Length ? positions[i + 2] : positions[positions.Length - 1];
                    double durationSeconds = (endGranule - startGranule) / 48000.0;
                    string formattedDuration = FormatDuration(durationSeconds);
                    trackList.Add($"Track {i + 1:D2} - {formattedDuration}");
                }
            }
            else if (audio.Header.AudioChapters.Length == 0)
            {
                // Single track (no chapters)
                audio.CalculateStatistics(out _, out _, out _, out _, out _, out _, out ulong highestGranule);
                double totalSeconds = highestGranule / 48000.0;
                string formattedDuration = FormatDuration(totalSeconds);
                trackList.Add($"Track 01 - {formattedDuration}");
            }
        }
        catch
        {
            // If we can't read the file or calculate tracks, return empty list
        }

        return trackList;
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

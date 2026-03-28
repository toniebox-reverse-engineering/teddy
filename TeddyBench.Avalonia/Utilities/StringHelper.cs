using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TeddyBench.Avalonia.Utilities;

/// <summary>
/// Helper class for string manipulation operations.
/// </summary>
public static class StringHelper
{
    /// <summary>
    /// Sanitizes a title by removing all bracketed content (e.g., "[LIVE]", "[RFID: ...]").
    /// Examples:
    ///   "[LIVE] Chet faker - built on glass [RFID: 003311aa66bb]" -> "Chet faker - built on glass"
    ///   "My Tonie [RFID: 12345678]" -> "My Tonie"
    /// </summary>
    public static string SanitizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "decoded";

        // Remove all text within square brackets (including the brackets themselves)
        // This regex matches opening bracket, any content, closing bracket
        string sanitized = Regex.Replace(title, @"\s*\[.*?\]\s*", " ");

        // Clean up extra whitespace
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();

        // If the result is empty after sanitization, use a default name
        if (string.IsNullOrWhiteSpace(sanitized))
            return "decoded";

        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        return sanitized;
    }

    /// <summary>
    /// Extracts RFID from a display name if present.
    /// Returns null if no RFID pattern is found.
    /// </summary>
    public static string? ExtractRfid(string displayName)
    {
        var rfidMatch = Regex.Match(displayName, @"\[RFID:\s*([0-9A-F]{8})\]", RegexOptions.IgnoreCase);
        if (rfidMatch.Success)
        {
            return rfidMatch.Groups[1].Value;
        }
        return null;
    }
}

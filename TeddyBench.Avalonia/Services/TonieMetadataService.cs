using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TeddyBench.Avalonia.Models;

namespace TeddyBench.Avalonia.Services
{
    public class TonieMetadataService
    {
        private List<TonieMetadata> _toniesDb = new();
        private List<TonieMetadata> _customTonies = new();
        private readonly string _basePath;
        private readonly string _cachePath;
        private readonly string _customToniesPath;
        private readonly HttpClient _httpClient;
        private const string TonieJsonUrl = "https://api.revvox.de/tonies.json?source=TeddyBench.Avalonia&version=1.0";
        private bool _customToniesModified = false;

        public TonieMetadataService()
        {
            // Assume files are in the app directory
            _basePath = AppDomain.CurrentDomain.BaseDirectory;
            _cachePath = Path.Combine(_basePath, "cache");
            _customToniesPath = Path.Combine(_basePath, "customTonies.json");
            _httpClient = new HttpClient();

            // Create cache directory if it doesn't exist
            if (!Directory.Exists(_cachePath))
            {
                Directory.CreateDirectory(_cachePath);
            }

            LoadMetadata();
        }

        private void LoadMetadata()
        {
            try
            {
                // Load tonies.json
                var toniesPath = FindFile("tonies.json");
                if (!string.IsNullOrEmpty(toniesPath) && File.Exists(toniesPath))
                {
                    var json = File.ReadAllText(toniesPath);
                    _toniesDb = JsonConvert.DeserializeObject<List<TonieMetadata>>(json) ?? new();
                }

                // Load customTonies.json
                var customPath = FindFile("customTonies.json");
                if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
                {
                    var json = File.ReadAllText(customPath);
                    // Try to load as array (new format), fallback to old format
                    try
                    {
                        _customTonies = JsonConvert.DeserializeObject<List<TonieMetadata>>(json) ?? new();
                    }
                    catch
                    {
                        // Try old format (Dictionary<string, string>)
                        var oldFormat = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (oldFormat != null)
                        {
                            _customTonies = MigrateOldCustomToniesFormat(oldFormat);
                            _customToniesModified = true;
                            SaveCustomTonies(); // Save in new format
                        }
                    }
                }
            }
            catch
            {
                // Ignore metadata loading errors
            }
        }

        private string? FindFile(string filename)
        {
            // Try multiple locations
            var locations = new[]
            {
                Path.Combine(_basePath, filename),
                Path.Combine(Directory.GetCurrentDirectory(), filename),
                Path.Combine(Directory.GetCurrentDirectory(), "..", filename),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", filename),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", filename)
            };

            return locations.FirstOrDefault(File.Exists);
        }

        private string ReverseByteOrder(string hexString)
        {
            // Reverse byte order: "0451ED0E" -> "0EED5104"
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

        /// <summary>
        /// Migrates old format (Dictionary) to new format (List of TonieMetadata).
        /// Old format: { "HASH": "Title [RFID: 0EED5104]" }
        /// New format: Array of TonieMetadata objects
        /// </summary>
        private List<TonieMetadata> MigrateOldCustomToniesFormat(Dictionary<string, string> oldFormat)
        {
            var newFormat = new List<TonieMetadata>();
            int counter = 0;

            foreach (var kvp in oldFormat)
            {
                var metadata = new TonieMetadata
                {
                    No = counter.ToString(),
                    Hash = new List<string> { kvp.Key.ToUpperInvariant() },
                    Title = kvp.Value,
                    Series = "Custom Tonie",
                    Episodes = kvp.Value,
                    Category = "custom",
                    Language = "en-us"
                };

                newFormat.Add(metadata);
                counter++;
            }

            return newFormat;
        }

        /// <summary>
        /// Gets a tonie from the official tonies.json database by hash.
        /// Returns null if not found (meaning it's a custom tonie).
        /// </summary>
        public TonieMetadata? GetTonieByHash(string hash)
        {
            hash = hash.ToUpperInvariant();
            return _toniesDb.FirstOrDefault(t =>
                t.Hash != null && t.Hash.Any(h => h.Equals(hash, StringComparison.OrdinalIgnoreCase)));
        }

        public (string title, string? imagePath, bool isCustom) GetTonieInfo(string hash, string? rfidFolder = null, uint? audioId = null, List<string>? tracks = null)
        {
            hash = hash.ToUpperInvariant();

            // Try custom tonies first
            var customTonie = _customTonies.FirstOrDefault(t =>
                t.Hash != null && t.Hash.Any(h => h.Equals(hash, StringComparison.OrdinalIgnoreCase)));

            if (customTonie != null)
            {
                // Update existing entry with new metadata if missing
                bool needsUpdate = false;

                if (audioId.HasValue && (customTonie.AudioId == null || customTonie.AudioId.Count == 0))
                {
                    customTonie.AudioId = new List<string> { audioId.Value.ToString() };
                    needsUpdate = true;
                }

                if (tracks != null && tracks.Count > 0 && (customTonie.Tracks == null || customTonie.Tracks.Count == 0))
                {
                    customTonie.Tracks = tracks;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    _customToniesModified = true;
                    SaveCustomTonies();
                }

                var title = !string.IsNullOrEmpty(customTonie.Title) ? customTonie.Title : customTonie.Series;
                return (title, GetCachedImage(hash), true);
            }

            // Try tonies database
            var tonie = _toniesDb.FirstOrDefault(t =>
                t.Hash != null && t.Hash.Any(h => h.Equals(hash, StringComparison.OrdinalIgnoreCase)));

            if (tonie != null)
            {
                var title = !string.IsNullOrEmpty(tonie.Title) ? tonie.Title : tonie.Series;
                return (title, GetCachedImage(hash), false);
            }

            // Not found in either database - create a custom entry
            // The rfidFolder is in reversed byte order, so reverse it back for display
            var customTitle = !string.IsNullOrEmpty(rfidFolder)
                ? $"Custom Tonie [RFID: {ReverseByteOrder(rfidFolder)}]"
                : $"Custom Tonie [{hash.Substring(0, 8)}]";
            AddCustomTonie(hash, customTitle, audioId, tracks, rfidFolder);
            return (customTitle, null, true);
        }

        public void AddCustomTonie(string hash, string title, uint? audioId = null, List<string>? tracks = null, string? directory = null)
        {
            hash = hash.ToUpperInvariant();

            // Check if already exists
            var existing = _customTonies.FirstOrDefault(t =>
                t.Hash != null && t.Hash.Any(h => h.Equals(hash, StringComparison.OrdinalIgnoreCase)));

            if (existing == null)
            {
                var metadata = new TonieMetadata
                {
                    No = _customTonies.Count.ToString(),
                    Hash = new List<string> { hash },
                    Title = title,
                    Series = "Custom Tonie",
                    Episodes = title,
                    Category = "custom",
                    Language = "en-us",
                    AudioId = audioId.HasValue ? new List<string> { audioId.Value.ToString() } : new List<string>(),
                    Tracks = tracks ?? new List<string>(),
                    Directory = directory
                };

                _customTonies.Add(metadata);
                _customToniesModified = true;

                // Save immediately
                SaveCustomTonies();
            }
        }

        public void UpdateCustomTonie(string hash, TonieMetadata updatedMetadata)
        {
            hash = hash.ToUpperInvariant();

            var existing = _customTonies.FirstOrDefault(t =>
                t.Hash != null && t.Hash.Any(h => h.Equals(hash, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                // Update all fields
                existing.Title = updatedMetadata.Title;
                existing.Series = updatedMetadata.Series;
                existing.Episodes = updatedMetadata.Episodes;
                existing.Tracks = updatedMetadata.Tracks;
                existing.AudioId = updatedMetadata.AudioId;
                existing.Language = updatedMetadata.Language;
                existing.Category = updatedMetadata.Category;
                existing.Model = updatedMetadata.Model;
                existing.Release = updatedMetadata.Release;
                existing.Pic = updatedMetadata.Pic;
                existing.Directory = updatedMetadata.Directory;

                _customToniesModified = true;

                // Save immediately
                SaveCustomTonies();
            }
        }

        public void RemoveCustomTonie(string hash)
        {
            hash = hash.ToUpperInvariant();

            var existing = _customTonies.FirstOrDefault(t =>
                t.Hash != null && t.Hash.Any(h => h.Equals(hash, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                _customTonies.Remove(existing);
                _customToniesModified = true;

                // Renumber the remaining entries
                for (int i = 0; i < _customTonies.Count; i++)
                {
                    _customTonies[i].No = i.ToString();
                }

                // Save immediately
                SaveCustomTonies();
            }
        }

        public string? GetCustomTonieName(string hash)
        {
            hash = hash.ToUpperInvariant();

            var customTonie = _customTonies.FirstOrDefault(t =>
                t.Hash != null && t.Hash.Any(h => h.Equals(hash, StringComparison.OrdinalIgnoreCase)));

            if (customTonie != null)
            {
                return !string.IsNullOrEmpty(customTonie.Title) ? customTonie.Title : customTonie.Series;
            }

            return null;
        }

        /// <summary>
        /// Gets the full metadata for a custom Tonie by hash
        /// </summary>
        public TonieMetadata? GetCustomTonieMetadata(string hash)
        {
            hash = hash.ToUpperInvariant();

            return _customTonies.FirstOrDefault(t =>
                t.Hash != null && t.Hash.Any(h => h.Equals(hash, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Updates a custom Tonie's hash key (used when modifying a tonie, which changes its hash).
        /// If oldHash is in customTonies, removes it and adds newHash with the same metadata.
        /// If oldHash is not in customTonies (official tonie), adds newHash with the provided title.
        /// </summary>
        public void UpdateTonieHash(string oldHash, string newHash, string title)
        {
            oldHash = oldHash.ToUpperInvariant();
            newHash = newHash.ToUpperInvariant();

            var existing = _customTonies.FirstOrDefault(t =>
                t.Hash != null && t.Hash.Any(h => h.Equals(oldHash, StringComparison.OrdinalIgnoreCase)));

            // If the old hash exists, update it with the new hash
            if (existing != null)
            {
                existing.Hash = new List<string> { newHash };
            }
            else
            {
                // Old hash not in customTonies (it was an official tonie), add new entry
                AddCustomTonie(newHash, title);
            }

            _customToniesModified = true;
            SaveCustomTonies();
        }

        /// <summary>
        /// Bulk updates specific fields for multiple custom tonies.
        /// Only updates fields that are not null.
        /// </summary>
        public int BulkUpdateCustomTonies(List<string> hashes, string? series, string? category, string? language)
        {
            int updatedCount = 0;

            foreach (var hash in hashes)
            {
                var hashUpper = hash.ToUpperInvariant();
                var existing = _customTonies.FirstOrDefault(t =>
                    t.Hash != null && t.Hash.Any(h => h.Equals(hashUpper, StringComparison.OrdinalIgnoreCase)));

                if (existing != null)
                {
                    bool updated = false;

                    if (!string.IsNullOrWhiteSpace(series))
                    {
                        existing.Series = series.Trim();
                        updated = true;
                    }

                    if (!string.IsNullOrWhiteSpace(category))
                    {
                        existing.Category = category.Trim();
                        updated = true;
                    }

                    if (!string.IsNullOrWhiteSpace(language))
                    {
                        existing.Language = language.Trim();
                        updated = true;
                    }

                    if (updated)
                    {
                        updatedCount++;
                        _customToniesModified = true;
                    }
                }
            }

            if (_customToniesModified)
            {
                SaveCustomTonies();
            }

            return updatedCount;
        }

        private void SaveCustomTonies()
        {
            if (!_customToniesModified)
            {
                return;
            }

            try
            {
                // Save as array format (matching teddycloud format)
                var json = JsonConvert.SerializeObject(_customTonies, Formatting.Indented);
                File.WriteAllText(_customToniesPath, json);
                _customToniesModified = false;
            }
            catch
            {
                // Ignore save errors
            }
        }

        public (string title, string? imagePath) GetTonieInfoByAudioId(uint audioId)
        {
            var audioIdStr = audioId.ToString();

            // Try tonies database by audio_id
            var tonie = _toniesDb.FirstOrDefault(t =>
                t.AudioId != null && t.AudioId.Any(id => id.Equals(audioIdStr, StringComparison.OrdinalIgnoreCase)));

            if (tonie != null)
            {
                var title = !string.IsNullOrEmpty(tonie.Title) ? tonie.Title : tonie.Series;
                var hash = tonie.Hash?.FirstOrDefault();
                return (title, hash != null ? GetCachedImage(hash) : null);
            }

            return ($"Audio ID: {audioId}", null);
        }

        private string? GetCachedImage(string hash)
        {
            hash = hash.ToUpperInvariant();

            // Try multiple cache locations
            var cacheLocations = new[]
            {
                Path.Combine(_basePath, "cache"),
                Path.Combine(Directory.GetCurrentDirectory(), "cache"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "cache"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "cache"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "cache")
            };

            foreach (var cacheDir in cacheLocations)
            {
                if (Directory.Exists(cacheDir))
                {
                    var imagePath = Path.Combine(cacheDir, $"{hash}.png");
                    if (File.Exists(imagePath))
                    {
                        return imagePath;
                    }
                }
            }

            return null;
        }

        public async Task<bool> DownloadTonieJsonAsync()
        {
            try
            {
                var jsonContent = await _httpClient.GetStringAsync(TonieJsonUrl);

                var toniesPath = Path.Combine(_basePath, "tonies.json");
                await File.WriteAllTextAsync(toniesPath, jsonContent);

                // Reload metadata after download
                _toniesDb = JsonConvert.DeserializeObject<List<TonieMetadata>>(jsonContent) ?? new();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> DownloadAndCacheImageAsync(string hash, string? picUrl)
        {
            if (string.IsNullOrEmpty(picUrl))
            {
                return null;
            }

            hash = hash.ToUpperInvariant();
            var cacheFileName = Path.Combine(_cachePath, $"{hash}.png");

            // If already cached, return the cached path
            if (File.Exists(cacheFileName))
            {
                return cacheFileName;
            }

            try
            {
                var imageBytes = await _httpClient.GetByteArrayAsync(picUrl);
                await File.WriteAllBytesAsync(cacheFileName, imageBytes);
                return cacheFileName;
            }
            catch
            {
                return null;
            }
        }

        public string? GetPicUrl(string hash)
        {
            hash = hash.ToUpperInvariant();

            // Try custom tonies first
            var customTonie = _customTonies.FirstOrDefault(t =>
                t.Hash != null && t.Hash.Any(h => h.Equals(hash, StringComparison.OrdinalIgnoreCase)));

            if (customTonie != null)
            {
                return customTonie.Pic; // May be null or a URL
            }

            // Try tonies database
            var tonie = _toniesDb.FirstOrDefault(t =>
                t.Hash != null && t.Hash.Any(h => h.Equals(hash, StringComparison.OrdinalIgnoreCase)));

            return tonie?.Pic;
        }
    }
}
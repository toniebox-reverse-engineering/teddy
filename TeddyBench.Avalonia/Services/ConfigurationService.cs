using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TeddyBench.Avalonia.Models;

namespace TeddyBench.Avalonia.Services;

/// <summary>
/// Service for managing application configuration (appsettings.json).
/// </summary>
public class ConfigurationService
{
    private readonly string _configPath;

    public ConfigurationService()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    }

    /// <summary>
    /// Loads the sort option from configuration.
    /// </summary>
    /// <returns>The saved sort option, or DisplayName as default.</returns>
    public SortOption LoadSortOption()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var configJson = File.ReadAllText(_configPath);
                var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(configJson);

                if (config != null && config.ContainsKey("SortOption"))
                {
                    var sortOptionString = config["SortOption"].ToString();
                    if (Enum.TryParse<SortOption>(sortOptionString, out var sortOption))
                    {
                        return sortOption;
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        // Default to DisplayName if not found or error
        return SortOption.DisplayName;
    }

    /// <summary>
    /// Saves the sort option to configuration.
    /// </summary>
    public void SaveSortOption(SortOption sortOption)
    {
        try
        {
            // Read existing config
            Dictionary<string, object> config;
            if (File.Exists(_configPath))
            {
                var configJson = File.ReadAllText(_configPath);
                config = JsonConvert.DeserializeObject<Dictionary<string, object>>(configJson) ?? new Dictionary<string, object>();
            }
            else
            {
                config = new Dictionary<string, object>();
            }

            // Update sort option
            config["SortOption"] = sortOption.ToString();

            // Write back to file with formatting
            var updatedJson = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(_configPath, updatedJson);
        }
        catch
        {
            // Ignore errors
        }
    }

    /// <summary>
    /// Loads the RFID prefix from configuration.
    /// </summary>
    /// <returns>The RFID prefix (4 characters in reverse byte order), or "0EED" as default.</returns>
    public string LoadRfidPrefix()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var configJson = File.ReadAllText(_configPath);
                var config = JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson);
                if (config != null && config.ContainsKey("RfidPrefix"))
                {
                    return config["RfidPrefix"];
                }
            }
        }
        catch
        {
            // Ignore errors, use default
        }

        return "0EED"; // Default value (ED0E reversed)
    }

    /// <summary>
    /// Loads the AudioIdPrompt setting from configuration.
    /// </summary>
    /// <returns>True if user should be prompted for Audio ID, false to auto-generate (default).</returns>
    public bool LoadAudioIdPrompt()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var configJson = File.ReadAllText(_configPath);
                var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(configJson);

                if (config != null && config.ContainsKey("AudioIdPrompt"))
                {
                    if (config["AudioIdPrompt"] is bool boolValue)
                    {
                        return boolValue;
                    }
                    // Handle string "true"/"false" values
                    if (bool.TryParse(config["AudioIdPrompt"]?.ToString(), out bool parsedValue))
                    {
                        return parsedValue;
                    }
                }
            }
        }
        catch
        {
            // Ignore errors, use default
        }

        return false; // Default value (auto-generate Audio ID)
    }
}

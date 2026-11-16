using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("TeddyBench.Core.Tests")]

namespace TeddyBench.Core
{
    /// <summary>
    /// Locations of TeddyBench's own files (settings, tonies.json, customTonies.json, image cache).
    /// Windows: the current directory, exactly as before, so existing installations keep their files.
    /// Linux/macOS: XDG base directories ($XDG_CONFIG_HOME/TeddyBench, $XDG_CACHE_HOME/TeddyBench).
    /// </summary>
    public static class AppPaths
    {
        public const string AppName = "TeddyBench";

        private static string _DataDirectory;
        private static string _CacheDirectory;

        /// <summary>Settings, tonies.json and customTonies.json. Created on first access.</summary>
        public static string DataDirectory => EnsureExists(_DataDirectory ?? (_DataDirectory = DefaultDataDirectory()));

        /// <summary>Downloaded tonie images. Created on first access.</summary>
        public static string CacheDirectory => EnsureExists(_CacheDirectory ?? (_CacheDirectory = DefaultCacheDirectory()));

        public static string SettingsFile => Path.Combine(DataDirectory, "teddyBench.cfg");
        public static string TonieJsonFile => Path.Combine(DataDirectory, "tonies.json");
        public static string CustomTonieJsonFile => Path.Combine(DataDirectory, "customTonies.json");

        /// <summary>Overrides the locations, e.g. for tests or a portable installation. null keeps the default.</summary>
        public static void Configure(string dataDirectory, string cacheDirectory = null)
        {
            _DataDirectory = dataDirectory;
            _CacheDirectory = cacheDirectory;
        }

        internal static string DefaultDataDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.CurrentDirectory;
            }
            return Path.Combine(XdgDirectory("XDG_CONFIG_HOME", ".config"), AppName);
        }

        internal static string DefaultCacheDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(DataDirectory, "cache");
            }
            return Path.Combine(XdgDirectory("XDG_CACHE_HOME", ".cache"), AppName);
        }

        private static string XdgDirectory(string variable, string fallbackBelowHome)
        {
            string dir = Environment.GetEnvironmentVariable(variable);

            /* the XDG spec says relative paths are invalid and must be ignored */
            if (!string.IsNullOrEmpty(dir) && Path.IsPathRooted(dir))
            {
                return dir;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fallbackBelowHome);
        }

        private static string EnsureExists(string dir)
        {
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}

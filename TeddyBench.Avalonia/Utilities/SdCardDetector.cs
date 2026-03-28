using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace TeddyBench.Avalonia.Utilities;

/// <summary>
/// Utility class to detect SD cards and removable storage across different platforms.
/// </summary>
public static class SdCardDetector
{
    /// <summary>
    /// Attempts to find the first SD card or removable storage device.
    /// Returns the path if found, null otherwise.
    /// </summary>
    public static string? FindFirstSdCard()
    {
        if (OperatingSystem.IsWindows())
        {
            return FindSdCardWindows();
        }
        else if (OperatingSystem.IsLinux())
        {
            return FindSdCardLinux();
        }
        else if (OperatingSystem.IsMacOS())
        {
            return FindSdCardMacOS();
        }

        return null;
    }

    /// <summary>
    /// Get all potential SD card/removable storage paths for the current platform.
    /// </summary>
    public static List<string> GetAllRemovableStoragePaths()
    {
        if (OperatingSystem.IsWindows())
        {
            return GetRemovableStoragePathsWindows();
        }
        else if (OperatingSystem.IsLinux())
        {
            return GetRemovableStoragePathsLinux();
        }
        else if (OperatingSystem.IsMacOS())
        {
            return GetRemovableStoragePathsMacOS();
        }

        return new List<string>();
    }

    private static string? FindSdCardWindows()
    {
        try
        {
            var drives = DriveInfo.GetDrives();

            // Look for removable drives first
            var removableDrive = drives.FirstOrDefault(d =>
                d.IsReady &&
                d.DriveType == DriveType.Removable &&
                IsToniebox(d.RootDirectory.FullName));

            if (removableDrive != null)
                return removableDrive.RootDirectory.FullName;

            // Fallback: any removable drive
            removableDrive = drives.FirstOrDefault(d =>
                d.IsReady &&
                d.DriveType == DriveType.Removable);

            return removableDrive?.RootDirectory.FullName;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindSdCardLinux()
    {
        try
        {
            // Common mount points for removable media on Linux
            var mountPointBases = new[]
            {
                "/media",  // Ubuntu/Debian
                "/run/media", // Fedora/RHEL
                "/mnt"     // Generic mount point
            };

            foreach (var mountPointBase in mountPointBases)
            {
                if (!Directory.Exists(mountPointBase))
                    continue;

                // First level: user directories (e.g., /media/username)
                var userDirs = Directory.GetDirectories(mountPointBase);

                foreach (var userDir in userDirs)
                {
                    // Second level: actual mount points (e.g., /media/username/3238-3133)
                    var mountedDevices = Directory.GetDirectories(userDir);

                    // Check each mounted device
                    foreach (var device in mountedDevices)
                    {
                        // Prioritize Toniebox SD cards
                        if (IsToniebox(device))
                            return device;
                    }

                    // If no Toniebox found, return first device that looks like removable storage
                    foreach (var device in mountedDevices)
                    {
                        if (IsLikelyRemovableStorage(device))
                            return device;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindSdCardMacOS()
    {
        try
        {
            var volumesPath = "/Volumes";
            if (!Directory.Exists(volumesPath))
                return null;

            // Get all volumes except the system volume (Macintosh HD)
            var volumes = Directory.GetDirectories(volumesPath)
                .Where(v => !Path.GetFileName(v).StartsWith("Macintosh"))
                .Where(v => IsToniebox(v) || IsLikelyRemovableStorage(v))
                .OrderBy(v => v)
                .ToList();

            return volumes.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GetRemovableStoragePathsWindows()
    {
        try
        {
            var drives = DriveInfo.GetDrives();
            return drives
                .Where(d => d.IsReady && d.DriveType == DriveType.Removable)
                .Select(d => d.RootDirectory.FullName)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<string> GetRemovableStoragePathsLinux()
    {
        try
        {
            var paths = new List<string>();
            var mountPoints = new[] { "/media", "/run/media", "/mnt" };

            foreach (var mountPoint in mountPoints)
            {
                if (!Directory.Exists(mountPoint))
                    continue;

                var subdirs = Directory.GetDirectories(mountPoint, "*", SearchOption.AllDirectories)
                    .Where(d => IsLikelyRemovableStorage(d))
                    .ToList();

                paths.AddRange(subdirs);
            }

            return paths.Distinct().OrderBy(p => p).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<string> GetRemovableStoragePathsMacOS()
    {
        try
        {
            var volumesPath = "/Volumes";
            if (!Directory.Exists(volumesPath))
                return new List<string>();

            return Directory.GetDirectories(volumesPath)
                .Where(v => !Path.GetFileName(v).StartsWith("Macintosh"))
                .OrderBy(v => v)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Check if a directory looks like a Toniebox SD card.
    /// </summary>
    private static bool IsToniebox(string path)
    {
        try
        {
            // Look for CONTENT folder - characteristic of Toniebox SD cards
            var contentPath = Path.Combine(path, "CONTENT");
            return Directory.Exists(contentPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if a directory is likely removable storage.
    /// Heuristics: not a system directory, has typical mount characteristics.
    /// </summary>
    private static bool IsLikelyRemovableStorage(string path)
    {
        try
        {
            // Exclude system directories
            var dirName = Path.GetFileName(path).ToLower();
            var systemDirs = new[] { "boot", "dev", "etc", "proc", "sys", "tmp", "var", "usr", "lib", "bin", "sbin" };

            if (systemDirs.Contains(dirName))
                return false;

            // Must be a directory that exists and is accessible
            if (!Directory.Exists(path))
                return false;

            // Try to check if it's a mount point (has subdirectories or files)
            var hasContent = Directory.GetFileSystemEntries(path).Length > 0;

            return hasContent;
        }
        catch
        {
            return false;
        }
    }
}
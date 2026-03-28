using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeddyBench.Avalonia.Models;

namespace TeddyBench.Avalonia.Services;

/// <summary>
/// Service for sorting Tonie files.
/// </summary>
public class TonieSortService
{
    /// <summary>
    /// Sorts a list of Tonie files according to the specified sort option.
    /// </summary>
    public List<TonieFileItem> SortTonieFiles(IEnumerable<TonieFileItem> files, SortOption sortOption)
    {
        return sortOption switch
        {
            SortOption.DisplayName => SortByDisplayName(files),
            SortOption.DirectoryName => SortByDirectoryName(files),
            SortOption.Newest => SortByNewest(files),
            SortOption.Oldest => SortByOldest(files),
            SortOption.Customs => SortByCustoms(files),
            _ => files.ToList()
        };
    }

    private List<TonieFileItem> SortByDisplayName(IEnumerable<TonieFileItem> files)
    {
        // Remove [LIVE] prefix for sorting purposes
        return files.OrderBy(t => t.DisplayName.Replace("[LIVE] ", "")).ToList();
    }

    private List<TonieFileItem> SortByDirectoryName(IEnumerable<TonieFileItem> files)
    {
        return files.OrderBy(t => t.DirectoryName).ToList();
    }

    private List<TonieFileItem> SortByNewest(IEnumerable<TonieFileItem> files)
    {
        // Sort by file modification date (newest first)
        return files.OrderByDescending(t =>
        {
            try
            {
                return new FileInfo(t.FilePath).LastWriteTime;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }).ToList();
    }

    private List<TonieFileItem> SortByOldest(IEnumerable<TonieFileItem> files)
    {
        // Sort by file modification date (oldest first)
        return files.OrderBy(t =>
        {
            try
            {
                return new FileInfo(t.FilePath).LastWriteTime;
            }
            catch
            {
                return DateTime.MaxValue;
            }
        }).ToList();
    }

    private List<TonieFileItem> SortByCustoms(IEnumerable<TonieFileItem> files)
    {
        // Custom tonies first (entries in customTonies.json), then others
        // Sort both groups alphabetically
        return files
            .OrderBy(t => t.IsCustomTonie ? 0 : 1) // Customs first
            .ThenBy(t => t.DisplayName.Replace("[LIVE] ", "")) // Sort alphabetically within each group
            .ToList();
    }
}
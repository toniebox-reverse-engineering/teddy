using System.Collections.Generic;

namespace TeddyBench.Avalonia.Models;

/// <summary>
/// Provides the list of available sort options for the UI.
/// </summary>
public static class SortOptionsProvider
{
    /// <summary>
    /// Gets all available sort options with their display names.
    /// </summary>
    public static List<SortOptionItem> GetAllSortOptions()
    {
        return new List<SortOptionItem>
        {
            new SortOptionItem { Option = SortOption.DisplayName, DisplayText = "Display Name" },
            new SortOptionItem { Option = SortOption.DirectoryName, DisplayText = "Directory name" },
            new SortOptionItem { Option = SortOption.Newest, DisplayText = "Newest" },
            new SortOptionItem { Option = SortOption.Oldest, DisplayText = "Oldest" },
            new SortOptionItem { Option = SortOption.Customs, DisplayText = "Customs" }
        };
    }
}

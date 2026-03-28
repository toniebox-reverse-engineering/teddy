using CommunityToolkit.Mvvm.ComponentModel;

namespace TeddyBench.Avalonia.Models;

public partial class AudioTrackItem : ObservableObject
{
    [ObservableProperty]
    private int _trackNumber;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? _artist;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? _title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private uint? _trackNumberTag;

    /// <summary>
    /// Gets the display name for this track.
    /// Format: "TrackNumber - Artist - Title" if all tags are available,
    /// "Artist - Title" if only artist and title are available,
    /// or falls back to FileName if tags are missing.
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Title))
            {
                if (TrackNumberTag.HasValue && TrackNumberTag.Value > 0)
                {
                    return $"{TrackNumberTag:D2} - {Artist} - {Title}";
                }
                return $"{Artist} - {Title}";
            }
            return FileName;
        }
    }
}

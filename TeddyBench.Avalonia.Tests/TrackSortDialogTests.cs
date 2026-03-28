using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TeddyBench.Avalonia.Dialogs;
using Xunit;

namespace TeddyBench.Avalonia.Tests;

/// <summary>
/// Tests for TrackSortDialog functionality including ID3 tag display.
/// The test audio files (track1.mp3, track2.mp3, track3.mp3) are pre-tagged with:
/// - Artist: "DJ Neis"
/// - Titles: "440 Hz", "220 Hz", "880 Hz" (representing sine wave frequencies)
/// - Track numbers: 1, 2, 3
/// </summary>
public class TrackSortDialogTests
{
    [AvaloniaFact]
    public async Task TrackSortDialog_WithArtistAndTitleTags_ShouldDisplayFormattedNames()
    {
        // Arrange: Use pre-tagged test audio files
        var track1Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "track1.mp3");
        var track2Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "track2.mp3");
        var track3Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "track3.mp3");

        var audioPaths = new[] { track1Path, track2Path, track3Path };

        // Act: Create TrackSortDialog
        var window = new Window();
        var dialog = new TrackSortDialog(audioPaths);
        var viewModel = dialog.DataContext as TrackSortDialogViewModel;

        // Assert: Verify tracks are loaded
        Assert.NotNull(viewModel);
        Assert.Equal(3, viewModel.Tracks.Count);

        // Assert: Verify DisplayName shows "TrackNumber - Artist - Title" format for track1
        var track1 = viewModel.Tracks[0];
        Assert.Equal("01 - DJ Neis - 440 Hz", track1.DisplayName);
        Assert.Equal("DJ Neis", track1.Artist);
        Assert.Equal("440 Hz", track1.Title);
        Assert.Equal(1u, track1.TrackNumberTag);
        Assert.Equal("track1.mp3", track1.FileName);

        // Assert: Verify DisplayName shows "TrackNumber - Artist - Title" format for track2
        var track2 = viewModel.Tracks[1];
        Assert.Equal("02 - DJ Neis - 220 Hz", track2.DisplayName);
        Assert.Equal("DJ Neis", track2.Artist);
        Assert.Equal("220 Hz", track2.Title);
        Assert.Equal(2u, track2.TrackNumberTag);
        Assert.Equal("track2.mp3", track2.FileName);

        // Assert: Verify DisplayName shows "TrackNumber - Artist - Title" format for track3
        var track3 = viewModel.Tracks[2];
        Assert.Equal("03 - DJ Neis - 880 Hz", track3.DisplayName);
        Assert.Equal("DJ Neis", track3.Artist);
        Assert.Equal("880 Hz", track3.Title);
        Assert.Equal(3u, track3.TrackNumberTag);
        Assert.Equal("track3.mp3", track3.FileName);

        await Task.CompletedTask;
    }
}

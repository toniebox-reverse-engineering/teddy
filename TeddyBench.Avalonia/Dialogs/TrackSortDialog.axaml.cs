using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.Linq;

namespace TeddyBench.Avalonia.Dialogs;

public partial class TrackSortDialog : TonieDialogBase
{
    private TrackSortDialogViewModel? _viewModel;

    public TrackSortDialog()
    {
        InitializeComponent();
    }

    public TrackSortDialog(string[] audioPaths) : this()
    {
        _viewModel = new TrackSortDialogViewModel(this, audioPaths);
        DataContext = _viewModel;

        // Wire up selection changed event
        var trackListBox = this.FindControl<ListBox>("TrackListBox");
        if (trackListBox != null)
        {
            trackListBox.SelectionChanged += (s, e) =>
            {
                if (_viewModel != null)
                {
                    // Update the ViewModel's selected tracks collection
                    _viewModel.SelectedTracks.Clear();
                    foreach (var item in trackListBox.SelectedItems?.OfType<Models.AudioTrackItem>() ?? Enumerable.Empty<Models.AudioTrackItem>())
                    {
                        _viewModel.SelectedTracks.Add(item);
                    }
                    _viewModel.UpdateButtonStates();
                }
            };
        }

        // Set focus to Encode button when dialog opens so mnemonics work immediately
        Opened += async (s, e) =>
        {
            await FocusButtonDelayed("EncodeButton");
        };
    }

    // Override to handle ESC specifically for this dialog (returns false instead of just closing)
    protected override void OnEscapePressed()
    {
        Close(false);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnEncodeClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    public string[]? GetSortedFilePaths()
    {
        return _viewModel?.GetSortedFilePaths();
    }
}

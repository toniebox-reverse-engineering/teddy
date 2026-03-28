using System;
using Avalonia.Controls;
using Avalonia.Input;
using TeddyBench.Avalonia.ViewModels;
using System.ComponentModel;

namespace TeddyBench.Avalonia.Dialogs;

public partial class PlayerDialog : TonieDialogBase
{
    public PlayerDialog()
    {
        InitializeComponent();

        // Set focus to Play button when dialog opens
        // This enables mnemonics while keeping Space key safe (just plays/pauses)
        Opened += async (s, e) =>
        {
            await FocusButtonDelayed("PlayPauseButton");
        };

        // Stop playback when window is closing
        Closing += (s, e) =>
        {
            if (DataContext is PlayerDialogViewModel vm)
            {
                vm.Cleanup();
            }
        };

        // Handle property changes to scroll to current track
        DataContextChanged += (s, e) =>
        {
            if (DataContext is PlayerDialogViewModel vm)
            {
                vm.PropertyChanged += ViewModel_PropertyChanged;
            }
        };
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerDialogViewModel.CurrentTrack))
        {
            if (DataContext is PlayerDialogViewModel vm && vm.CurrentTrack != null)
            {
                // Find the ListBox by name and scroll to the current track
                var listBox = this.FindControl<ListBox>("TrackListBox");
                if (listBox != null)
                {
                    listBox.ScrollIntoView(vm.CurrentTrack);
                }
            }
        }
    }
}

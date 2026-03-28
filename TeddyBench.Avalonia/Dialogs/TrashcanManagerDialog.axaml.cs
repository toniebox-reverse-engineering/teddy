using Avalonia.Controls;
using Avalonia.Interactivity;
using TeddyBench.Avalonia.Models;

namespace TeddyBench.Avalonia.Dialogs;

public partial class TrashcanManagerDialog : Window
{
    public TrashcanManagerDialog()
    {
        InitializeComponent();
    }

    private void OnRestoreClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is DeletedTonieItem tonie && DataContext is TrashcanManagerDialogViewModel vm)
        {
            vm.SelectedTonie = tonie;
            _ = vm.RestoreSelectedCommand.ExecuteAsync(null);
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is DeletedTonieItem tonie && DataContext is TrashcanManagerDialogViewModel vm)
        {
            vm.SelectedTonie = tonie;
            _ = vm.DeleteSelectedCommand.ExecuteAsync(null);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
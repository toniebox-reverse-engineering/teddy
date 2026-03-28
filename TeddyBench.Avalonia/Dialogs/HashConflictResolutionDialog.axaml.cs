using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TeddyBench.Avalonia.Dialogs;

public partial class HashConflictResolutionDialog : Window
{
    public HashConflictResolutionDialog()
    {
        InitializeComponent();
    }

    public HashConflictResolutionDialog(string tonieName, string existingRfidUid) : this()
    {
        DataContext = new HashConflictResolutionDialogViewModel(tonieName, existingRfidUid);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnMoveExistingClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(HashConflictResolutionResult.MoveExisting);
    }

    private void OnKeepExistingClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(HashConflictResolutionResult.KeepExisting);
    }

    private void OnCancelClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(HashConflictResolutionResult.Cancel);
    }
}

public enum HashConflictResolutionResult
{
    Cancel,
    MoveExisting,
    KeepExisting
}

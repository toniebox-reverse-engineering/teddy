using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TeddyBench.Avalonia.Dialogs;

public partial class ConflictResolutionDialog : Window
{
    public ConflictResolutionDialog()
    {
        InitializeComponent();
    }

    public ConflictResolutionDialog(string tonieName, string existingUid) : this()
    {
        DataContext = new ConflictResolutionDialogViewModel(tonieName, existingUid);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnOverwriteClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(ConflictResolutionResult.Overwrite);
    }

    private void OnNewUidClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(ConflictResolutionResult.NewUid);
    }

    private void OnCancelClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(ConflictResolutionResult.Cancel);
    }
}

public enum ConflictResolutionResult
{
    Cancel,
    Overwrite,
    NewUid
}
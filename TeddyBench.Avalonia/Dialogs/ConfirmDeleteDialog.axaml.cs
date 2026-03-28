using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace TeddyBench.Avalonia.Dialogs;

public partial class ConfirmDeleteDialog : Window
{
    public ConfirmDeleteDialog()
    {
        InitializeComponent();
    }

    public ConfirmDeleteDialog(string tonieName) : this()
    {
        DataContext = new ConfirmDeleteDialogViewModel(tonieName);

        // Note: Alt+Y and Alt+N are handled by button mnemonics automatically
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnYesClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnNoClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}

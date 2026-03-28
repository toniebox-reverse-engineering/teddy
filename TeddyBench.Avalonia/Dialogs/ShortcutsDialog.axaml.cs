using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace TeddyBench.Avalonia.Dialogs;

public partial class ShortcutsDialog : Window
{
    public ShortcutsDialog()
    {
        InitializeComponent();

        // Note: Alt+C for Close is handled by button mnemonic automatically
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCloseClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}

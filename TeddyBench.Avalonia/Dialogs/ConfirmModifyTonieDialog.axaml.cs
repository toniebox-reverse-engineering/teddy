using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace TeddyBench.Avalonia.Dialogs;

public partial class ConfirmModifyTonieDialog : Window
{
    public ConfirmModifyTonieDialog()
    {
        InitializeComponent();
    }

    public ConfirmModifyTonieDialog(string tonieName, uint currentAudioId) : this()
    {
        DataContext = new ConfirmModifyTonieDialogViewModel(tonieName, currentAudioId);

        // Note: Alt+C and Alt+N are handled by button mnemonics automatically
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

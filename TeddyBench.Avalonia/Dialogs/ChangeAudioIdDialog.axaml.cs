using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TeddyBench.Avalonia.Dialogs;

public partial class ChangeAudioIdDialog : Window
{
    private TextBox? _audioIdInput;

    public ChangeAudioIdDialog()
    {
        InitializeComponent();
    }

    public ChangeAudioIdDialog(uint currentAudioId)
    {
        InitializeComponent();
        DataContext = new ChangeAudioIdDialogViewModel(currentAudioId);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Focus the input when dialog opens
        this.Opened += (s, e) =>
        {
            _audioIdInput = this.FindControl<TextBox>("AudioIdInput");
            _audioIdInput?.Focus();
        };
    }

    private void OnOkClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    public uint? GetAudioId()
    {
        return (DataContext as ChangeAudioIdDialogViewModel)?.GetAudioId();
    }
}
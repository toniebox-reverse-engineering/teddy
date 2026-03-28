using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace TeddyBench.Avalonia.Dialogs;

public partial class RenameTonieDialog : Window
{
    public RenameTonieDialog()
    {
        InitializeComponent();
    }

    public RenameTonieDialog(string currentTitle) : this()
    {
        DataContext = new RenameTonieDialogViewModel(currentTitle);

        // Note: Alt+O and Alt+C are handled by button mnemonics automatically
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Focus the title input and select all text when dialog opens
        this.Opened += (s, e) =>
        {
            var titleInput = this.FindControl<TextBox>("TitleInput");
            if (titleInput != null)
            {
                titleInput.SelectAll();
                titleInput.Focus();
            }
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

    public string? GetNewTitle()
    {
        return (DataContext as RenameTonieDialogViewModel)?.NewTitle;
    }
}

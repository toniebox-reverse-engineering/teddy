using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TeddyBench.Avalonia.Models;

namespace TeddyBench.Avalonia.Dialogs;

public partial class EditMetadataDialog : Window
{
    public EditMetadataDialog()
    {
        InitializeComponent();
    }

    public EditMetadataDialog(TonieMetadata metadata) : this()
    {
        DataContext = new EditMetadataDialogViewModel(metadata);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Focus the title input when dialog opens
        this.Opened += (s, e) =>
        {
            var titleInput = this.FindControl<TextBox>("TitleInput");
            if (titleInput != null)
            {
                titleInput.Focus();
            }
        };
    }

    private void OnSaveClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    public TonieMetadata? GetUpdatedMetadata()
    {
        return (DataContext as EditMetadataDialogViewModel)?.GetUpdatedMetadata();
    }
}

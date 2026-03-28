using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TeddyBench.Avalonia.Dialogs;

public partial class BulkEditMetadataDialog : Window
{
    public BulkEditMetadataDialog()
    {
        InitializeComponent();
    }

    public BulkEditMetadataDialog(int selectedCount) : this()
    {
        DataContext = new BulkEditMetadataDialogViewModel(selectedCount);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnApplyClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    public (string? series, string? category, string? language) GetUpdatedFields()
    {
        var vm = DataContext as BulkEditMetadataDialogViewModel;
        if (vm == null)
            return (null, null, null);

        return (
            string.IsNullOrWhiteSpace(vm.Series) ? null : vm.Series.Trim(),
            string.IsNullOrWhiteSpace(vm.Category) ? null : vm.Category.Trim(),
            string.IsNullOrWhiteSpace(vm.Language) ? null : vm.Language.Trim()
        );
    }
}

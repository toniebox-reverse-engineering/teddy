using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TeddyBench.Avalonia.Dialogs;

public partial class ConfirmDeleteDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = string.Empty;

    public bool DialogResult { get; private set; }

    public ConfirmDeleteDialogViewModel(string tonieName)
    {
        Message = $"Are you sure you want to delete '{tonieName}'?\n\nThis will permanently delete the file and directory from the SD card.";
    }

    [RelayCommand]
    private void Yes()
    {
        DialogResult = true;
    }

    [RelayCommand]
    private void No()
    {
        DialogResult = false;
    }
}

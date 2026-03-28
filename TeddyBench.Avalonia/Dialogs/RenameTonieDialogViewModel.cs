using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TeddyBench.Avalonia.Dialogs;

public partial class RenameTonieDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _newTitle = string.Empty;

    public bool DialogResult { get; private set; }

    public RenameTonieDialogViewModel(string currentTitle)
    {
        NewTitle = currentTitle;
    }

    [RelayCommand]
    private void Ok()
    {
        DialogResult = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }
}

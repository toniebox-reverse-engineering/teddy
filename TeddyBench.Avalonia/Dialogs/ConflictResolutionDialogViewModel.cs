using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TeddyBench.Avalonia.Dialogs;

public partial class ConflictResolutionDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _details = string.Empty;

    public ConflictResolutionDialogViewModel(string tonieName, string existingUid)
    {
        Message = $"A Tonie already exists at location '{existingUid}/500304E0' on the SD card.";
        Details = $"The Tonie you're trying to restore ('{tonieName}') wants to use the same RFID UID. " +
                  "If you proceed with overwrite, the existing Tonie will be permanently replaced.";
    }

    [RelayCommand]
    private void Overwrite()
    {
        // Handled by dialog Close
    }

    [RelayCommand]
    private void NewUid()
    {
        // Handled by dialog Close
    }

    [RelayCommand]
    private void Cancel()
    {
        // Handled by dialog Close
    }
}
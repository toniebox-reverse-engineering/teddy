using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TeddyBench.Avalonia.Dialogs;

public partial class HashConflictResolutionDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _details = string.Empty;

    public HashConflictResolutionDialogViewModel(string tonieName, string existingRfidUid)
    {
        Message = $"This audio content already exists on the SD card!";
        Details = $"The Tonie '{tonieName}' you're trying to restore has the same audio content (same hash) " +
                  $"as a file that already exists at RFID location '{existingRfidUid}/500304E0'.\n\n" +
                  $"Since the hash uniquely identifies audio content + audio ID, you cannot have the same hash " +
                  $"pointing to two different RFID locations in customTonies.json.";
    }

    [RelayCommand]
    private void MoveExisting()
    {
        // Handled by dialog Close
    }

    [RelayCommand]
    private void KeepExisting()
    {
        // Handled by dialog Close
    }

    [RelayCommand]
    private void Cancel()
    {
        // Handled by dialog Close
    }
}

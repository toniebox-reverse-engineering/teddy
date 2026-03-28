using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TeddyBench.Avalonia.Dialogs;

public partial class ConfirmModifyTonieDialogViewModel : AudioIdManagementViewModelBase
{
    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _audioIdError = string.Empty;

    [ObservableProperty]
    private bool _isAudioIdValid;

    public bool DialogResult { get; private set; }
    public uint? NewAudioId { get; private set; }

    public ConfirmModifyTonieDialogViewModel(string tonieName, uint currentAudioId)
        : base(currentAudioId)
    {
        Message = $"You are about to modify \"{tonieName}\", which is an official Tonie from the database.\n\n" +
                  $"Modifying this tonie will convert it to a custom tonie. It will still work with the same figurine, " +
                  $"but you will lose the original metadata and image icon.\n\n" +
                  $"To enable playback on the Toniebox hardware, the Audio ID must be changed to a custom tonie ID.";

        ValidateAudioId();
    }

    protected override void OnAudioIdInputChangedCore()
    {
        ValidateAudioId();
    }

    private void ValidateAudioId()
    {
        AudioIdError = string.Empty;
        IsAudioIdValid = false;

        // Audio ID is required (not optional)
        if (!ValidateTimestampInput(AudioIdInput, allowEmpty: false, out uint timestamp))
        {
            if (string.IsNullOrWhiteSpace(AudioIdInput))
            {
                AudioIdError = "Audio ID timestamp is required";
            }
            else
            {
                AudioIdError = "Audio ID must be a valid Unix timestamp (decimal number, up to 10 digits)";
            }
            return;
        }

        IsAudioIdValid = true;
        NewAudioId = GetAudioId();
    }

    [RelayCommand]
    private void Yes()
    {
        if (!IsAudioIdValid)
        {
            AudioIdError = "Please enter a valid Audio ID before continuing";
            return;
        }

        DialogResult = true;
    }

    [RelayCommand]
    private void No()
    {
        DialogResult = false;
    }
}

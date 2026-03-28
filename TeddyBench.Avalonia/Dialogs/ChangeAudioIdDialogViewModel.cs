using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TeddyBench.Avalonia.Dialogs;

public partial class ChangeAudioIdDialogViewModel : AudioIdManagementViewModelBase
{
    [ObservableProperty]
    private string _audioIdError = string.Empty;

    [ObservableProperty]
    private bool _isAudioIdValid;

    public bool DialogResult { get; private set; }

    public ChangeAudioIdDialogViewModel(uint currentAudioId) : base(currentAudioId)
    {
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
    }

    [RelayCommand]
    private void Ok()
    {
        if (!IsAudioIdValid)
        {
            AudioIdError = "Please enter a valid Audio ID before continuing";
            return;
        }

        DialogResult = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }
}
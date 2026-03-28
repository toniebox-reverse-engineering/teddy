using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TeddyBench.Avalonia.Dialogs;

public partial class RestoreAsNewTonieDialogViewModel : AudioIdManagementViewModelBase
{
    private readonly Window _window;

    [ObservableProperty]
    private string _rfidUid = string.Empty;

    [ObservableProperty]
    private string _customTitle = string.Empty;

    [ObservableProperty]
    private bool _isValid;

    public bool DialogResult { get; private set; }

    public RestoreAsNewTonieDialogViewModel(Window window, string defaultTitle, uint currentAudioId)
        : base(currentAudioId)
    {
        _window = window;
        _customTitle = defaultTitle;
        ValidateInput();
    }

    partial void OnRfidUidChanged(string value)
    {
        // Auto-uppercase and validate
        if (!string.IsNullOrEmpty(value))
        {
            var upper = value.ToUpperInvariant();
            if (upper != value)
            {
                RfidUid = upper;
                return;
            }
        }

        ValidateInput();
    }

    protected override void OnAudioIdInputChangedCore()
    {
        ValidateInput();
    }

    private void ValidateInput()
    {
        // RFID must be exactly 8 hex characters
        bool rfidValid = RfidUid.Length == 8 &&
                        System.Text.RegularExpressions.Regex.IsMatch(RfidUid, "^[0-9A-F]{8}$");

        // Audio ID is optional
        bool audioIdValid = ValidateTimestampInput(AudioIdInput, allowEmpty: true, out uint _);

        IsValid = rfidValid && audioIdValid;
    }

    [RelayCommand]
    private void Restore()
    {
        DialogResult = true;
        _window.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        _window.Close();
    }
}
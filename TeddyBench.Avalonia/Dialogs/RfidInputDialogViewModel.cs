using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;

namespace TeddyBench.Avalonia.Dialogs;

public partial class RfidInputDialogViewModel : ObservableObject
{
    private readonly string _currentDirectory;

    [ObservableProperty]
    private string _rfidUid = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _canSubmit = false;

    public bool DialogResult { get; private set; }

    public RfidInputDialogViewModel(string rfidPrefix, string currentDirectory)
    {
        _currentDirectory = currentDirectory;

        // Convert to uppercase on initialization
        RfidUid = rfidPrefix.ToUpper();

        // Validate initial state
        ValidateInput();
    }

    partial void OnRfidUidChanged(string value)
    {
        ValidateInput();
    }

    private void ValidateInput()
    {
        // Clear previous error
        ErrorMessage = string.Empty;
        CanSubmit = false;

        // Check if UID is empty
        if (string.IsNullOrWhiteSpace(RfidUid))
        {
            ErrorMessage = "RFID UID cannot be empty";
            return;
        }

        // Check length (must be exactly 8 characters)
        if (RfidUid.Length < 8)
        {
            ErrorMessage = $"RFID UID must be 8 characters (currently {RfidUid.Length})";
            return;
        }

        if (RfidUid.Length > 8)
        {
            ErrorMessage = "RFID UID must be exactly 8 characters";
            return;
        }

        // Check if all characters are valid hexadecimal
        foreach (char c in RfidUid)
        {
            if (!IsHexChar(c))
            {
                ErrorMessage = "RFID UID must contain only hexadecimal characters (0-9, A-F)";
                return;
            }
        }

        // Check if directory already exists
        if (!string.IsNullOrEmpty(_currentDirectory))
        {
            string targetDir = Path.Combine(_currentDirectory, RfidUid);
            if (Directory.Exists(targetDir))
            {
                ErrorMessage = $"A custom Tonie with RFID '{RfidUid}' already exists on disk";
                return;
            }
        }

        // All validation passed
        CanSubmit = true;
    }

    private static bool IsHexChar(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
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

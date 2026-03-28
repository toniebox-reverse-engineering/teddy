using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace TeddyBench.Avalonia.Dialogs;

public partial class HexInputDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _hexValue = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _canSubmit = false;

    [ObservableProperty]
    private uint? _value = null;

    public bool DialogResult { get; private set; }

    public HexInputDialogViewModel()
    {
        // Validate initial state
        ValidateInput();
    }

    partial void OnHexValueChanged(string value)
    {
        ValidateInput();
    }

    private void ValidateInput()
    {
        // Clear previous error
        ErrorMessage = string.Empty;
        CanSubmit = false;
        Value = null;

        // Check if value is empty
        if (string.IsNullOrWhiteSpace(HexValue))
        {
            ErrorMessage = "Audio ID cannot be empty";
            return;
        }

        // Check length (must be exactly 8 characters)
        if (HexValue.Length < 8)
        {
            ErrorMessage = $"Audio ID must be 8 characters (currently {HexValue.Length})";
            return;
        }

        if (HexValue.Length > 8)
        {
            ErrorMessage = "Audio ID must be exactly 8 characters";
            return;
        }

        // Check if all characters are valid hexadecimal
        foreach (char c in HexValue)
        {
            if (!IsHexChar(c))
            {
                ErrorMessage = "Audio ID must contain only hexadecimal characters (0-9, A-F)";
                return;
            }
        }

        // Try to parse the hex value
        try
        {
            Value = Convert.ToUInt32(HexValue, 16);
            CanSubmit = true;
        }
        catch (Exception)
        {
            ErrorMessage = "Invalid hexadecimal value";
            return;
        }
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
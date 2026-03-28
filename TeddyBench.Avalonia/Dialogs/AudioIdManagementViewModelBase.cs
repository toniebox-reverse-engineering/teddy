using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace TeddyBench.Avalonia.Dialogs;

/// <summary>
/// Base class for ViewModels that manage Audio ID input with custom tonie offset calculation.
/// Provides shared logic for toggling between original Audio ID and custom timestamp-based IDs.
/// </summary>
public abstract partial class AudioIdManagementViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string _audioIdInput = string.Empty;

    [ObservableProperty]
    private bool _isCustomTonie = false;

    private readonly uint _originalAudioId;
    private uint _lastGeneratedTimestamp;

    /// <summary>
    /// Initializes the Audio ID management with the original Audio ID.
    /// </summary>
    /// <param name="currentAudioId">The current/original Audio ID of the tonie</param>
    protected AudioIdManagementViewModelBase(uint currentAudioId)
    {
        _originalAudioId = currentAudioId;

        // Pre-fill with the current Audio ID (convert to timestamp by adding the offset back)
        // If currentAudioId is already in the custom range, it needs the offset added back
        // If it's in the official range (>= 0x50000000), use it as-is
        uint displayTimestamp = currentAudioId >= 0x50000000 ? currentAudioId : currentAudioId + 0x50000000;
        AudioIdInput = displayTimestamp.ToString();
        _lastGeneratedTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    partial void OnAudioIdInputChanged(string value)
    {
        // Trigger validation in derived class
        OnAudioIdInputChangedCore();
    }

    partial void OnIsCustomTonieChanged(bool value)
    {
        // When toggle changes, update the input field
        if (value)
        {
            // Switching to Custom Tonie mode: use current timestamp
            _lastGeneratedTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            AudioIdInput = _lastGeneratedTimestamp.ToString();
        }
        else
        {
            // Switching back to original mode: restore original Audio ID
            uint displayTimestamp = _originalAudioId >= 0x50000000 ? _originalAudioId : _originalAudioId + 0x50000000;
            AudioIdInput = displayTimestamp.ToString();
        }

        // Note: AudioIdInput property change will trigger OnAudioIdInputChanged which calls OnAudioIdInputChangedCore
    }

    /// <summary>
    /// Called when the Audio ID input changes. Override to perform validation.
    /// </summary>
    protected abstract void OnAudioIdInputChangedCore();

    /// <summary>
    /// Validates that the input is a valid decimal timestamp.
    /// </summary>
    /// <param name="input">The input string to validate</param>
    /// <param name="allowEmpty">Whether empty input is allowed</param>
    /// <param name="timestamp">The parsed timestamp value</param>
    /// <returns>True if valid, false otherwise</returns>
    protected bool ValidateTimestampInput(string input, bool allowEmpty, out uint timestamp)
    {
        timestamp = 0;

        if (string.IsNullOrWhiteSpace(input))
        {
            return allowEmpty;
        }

        // Remove any spaces
        string cleanInput = input.Trim().Replace(" ", "");

        // Validate decimal format (up to 10 digits for Unix timestamp)
        if (!System.Text.RegularExpressions.Regex.IsMatch(cleanInput, "^[0-9]{1,10}$"))
        {
            return false;
        }

        // Parse the timestamp value
        return uint.TryParse(cleanInput, out timestamp);
    }

    /// <summary>
    /// Converts the timestamp input to the actual Audio ID, applying the custom tonie offset if enabled.
    /// </summary>
    /// <returns>The Audio ID, or null if input is empty</returns>
    public uint? GetAudioId()
    {
        if (string.IsNullOrEmpty(AudioIdInput))
        {
            return null;
        }

        // Parse the decimal timestamp
        if (uint.TryParse(AudioIdInput.Trim().Replace(" ", ""), out uint timestamp))
        {
            // Apply custom tonie offset if the switch is enabled
            uint audioId;
            if (IsCustomTonie)
            {
                // Custom tonie: timestamp - 0x50000000
                audioId = timestamp - 0x50000000;
            }
            else
            {
                // Use timestamp as-is (for official tonie Audio IDs)
                audioId = timestamp;
            }
            return audioId;
        }

        return null;
    }
}
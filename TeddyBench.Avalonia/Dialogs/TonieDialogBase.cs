using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;

namespace TeddyBench.Avalonia.Dialogs;

/// <summary>
/// Base class for all TeddyBench dialogs.
/// Provides common functionality:
/// - Key debouncing (treats held keys as single press)
/// - Blocks Space key (prevents accidental button activation)
/// - ESC key always closes/cancels the dialog
/// - Delayed button focus helper for mnemonics
/// </summary>
public class TonieDialogBase : Window
{
    private readonly HashSet<Key> _pressedKeys = new HashSet<Key>();

    public TonieDialogBase()
    {
        // Handle keyboard events for all dialogs
        AddHandler(KeyDownEvent, OnDialogKeyDown, handledEventsToo: true);
        AddHandler(KeyUpEvent, OnDialogKeyUp, handledEventsToo: true);
    }

    private void OnDialogKeyUp(object? sender, KeyEventArgs e)
    {
        // Key released - allow it to be processed again
        _pressedKeys.Remove(e.Key);
    }

    private void OnDialogKeyDown(object? sender, KeyEventArgs e)
    {
        // Key debouncing: ignore repeated KeyDown events from holding a key
        if (_pressedKeys.Contains(e.Key))
        {
            e.Handled = true;
            return;
        }

        // Mark this key as pressed
        _pressedKeys.Add(e.Key);

        // Block Space key - it should not activate buttons in dialogs
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }

        // ESC key closes the dialog - allow derived classes to customize via OnEscapePressed
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            OnEscapePressed();
            return;
        }

        // Allow derived classes to handle other keys
        OnKeyDownCore(sender, e);
    }

    /// <summary>
    /// Called when ESC key is pressed. Default behavior is to close the dialog.
    /// Override this to customize ESC key behavior (e.g., to return a specific value).
    /// </summary>
    protected virtual void OnEscapePressed()
    {
        Close();
    }

    /// <summary>
    /// Override this in derived classes to handle additional keyboard shortcuts.
    /// Space and ESC keys are already handled by the base class.
    /// </summary>
    protected virtual void OnKeyDownCore(object? sender, KeyEventArgs e)
    {
        // Base implementation does nothing - derived classes can override
    }

    /// <summary>
    /// Helper method to focus a button, enabling mnemonics to work.
    /// Uses a small delay to avoid keyboard events from the main window triggering the button.
    /// </summary>
    protected async Task FocusButtonDelayed(string buttonName, int delayMs = 50)
    {
        // Small delay to let keyboard events from main window finish processing
        // This is still needed for the initial open (separate from key debouncing)
        await Task.Delay(delayMs);

        var button = this.FindControl<Button>(buttonName);
        if (button != null)
        {
            button.Focus();
        }
    }

    /// <summary>
    /// Helper method to focus any control, enabling mnemonics to work.
    /// Uses a small delay to avoid keyboard events from the main window triggering actions.
    /// </summary>
    protected async Task FocusControlDelayed(string controlName, int delayMs = 50)
    {
        // Small delay to let keyboard events from main window finish processing
        // This is still needed for the initial open (separate from key debouncing)
        await Task.Delay(delayMs);

        var control = this.FindControl<Control>(controlName);
        if (control != null)
        {
            control.Focus();
        }
    }
}

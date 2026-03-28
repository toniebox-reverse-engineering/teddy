using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using TeddyBench.Avalonia.ViewModels;
using TeddyBench.Avalonia.Models;
using System.ComponentModel;

namespace TeddyBench.Avalonia.Views;

internal enum NavigationDirection
{
    Up,
    Down,
    Left,
    Right
}

public partial class MainWindow : Window
{
    private readonly HashSet<Key> _pressedKeys = new HashSet<Key>();
    private bool _shouldFocusFirstItem = false;
    private List<List<TonieFileItem>>? _cachedGrid = null;
    private bool _mnemonicModeActive = false;
    private System.Threading.CancellationTokenSource? _mnemonicModeCancellation = null;
    private bool _suppressNextAltToggle = false;

    public MainWindow()
    {
        InitializeComponent();

        // Set up the ViewModel with a reference to this window for dialogs
        var viewModel = new MainWindowViewModel(this);
        DataContext = viewModel;

        // Hook into window closing event to save configuration
        Closing += MainWindow_Closing;

        // Hook into window opened event to auto-open directory picker
        Opened += MainWindow_Opened;

        // Hook into key up event for debouncing
        KeyUp += MainWindow_KeyUp;

        // Hook into key down event for keyboard shortcuts
        // Use AddHandler with handledEventsToo to intercept keys before ListBox handles them
        AddHandler(KeyDownEvent, MainWindow_KeyDown, global::Avalonia.Interactivity.RoutingStrategies.Tunnel, true);

        // Recalculate grid when window is resized
        PropertyChanged += MainWindow_PropertyChanged;

        // Monitor IsAnyDialogOpen to clear pressed keys when dialogs close
        // This prevents stale key states if keys were released while dialog was open
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsAnyDialogOpen))
            {
                if (!viewModel.IsAnyDialogOpen && _pressedKeys.Count > 0)
                {
                    _pressedKeys.Clear();
                }
            }
            // Monitor for when scanning completes and tonies are loaded
            else if (e.PropertyName == nameof(MainWindowViewModel.IsScanning))
            {
                if (!viewModel.IsScanning && viewModel.TonieFiles.Count > 0)
                {
                    if (_shouldFocusFirstItem)
                    {
                        _shouldFocusFirstItem = false;
                        FocusFirstTonieItem();
                    }
                    // Update debug positions after scanning completes
                    UpdateGridPositions();
                }
            }
            // Set flag when directory changes to focus first item after scan
            else if (e.PropertyName == nameof(MainWindowViewModel.CurrentDirectory))
            {
                _shouldFocusFirstItem = true;
            }
        };
    }

    private async void FocusFirstTonieItem()
    {
        // Use dispatcher to ensure UI is fully rendered before focusing
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var listBox = this.FindControl<ListBox>("TonieListBox");
            if (listBox != null && listBox.ItemCount > 0)
            {
                // Select the first item
                listBox.SelectedIndex = 0;

                // Focus the ListBox to enable keyboard navigation
                listBox.Focus();

                // Also try to focus the container to ensure arrow keys work
                var container = listBox.ContainerFromIndex(0);
                if (container is Control control)
                {
                    control.Focus();
                }
            }
        }, global::Avalonia.Threading.DispatcherPriority.Background);
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        // Auto-open directory picker on startup
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.AutoOpenDirectoryOnStartup();
        }
    }

    private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // When window bounds change, recalculate the grid after a short delay
        if (e.Property.Name == nameof(Bounds))
        {
            // Use a timer to debounce resize events
            _ = Task.Run(async () =>
            {
                await Task.Delay(300); // Wait for resize to finish
                await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateGridPositions();
                });
            });
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Save configuration before closing
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveConfigurationOnExit();
        }
    }

    private void Border_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is Border border &&
            border.DataContext is TonieFileItem item &&
            DataContext is MainWindowViewModel viewModel)
        {
            // Get the ListBox
            var listBox = this.FindControl<ListBox>("TonieListBox");
            if (listBox == null) return;

            // Check how many items are currently selected
            int selectionCount = listBox.SelectedItems?.Count ?? 0;

            if (selectionCount <= 1)
            {
                // Single selection or no selection - use single-item context menu
                // The border already has the single-selection context menu attached
                // Just ensure the item is selected
                listBox.SelectedItem = item;
            }
            else
            {
                // Multiple items selected
                // Check if the right-clicked item is part of the selection
                bool isItemInSelection = listBox.SelectedItems?.Contains(item) ?? false;

                if (!isItemInSelection)
                {
                    // If right-clicked item is not in selection, select only this item
                    listBox.SelectedItem = item;
                }
                else
                {
                    // Right-clicked on a selected item - show multi-selection context menu
                    e.Handled = true;

                    // Capture the currently selected items before showing the menu
                    var selectedItemsCopy = listBox.SelectedItems?.Cast<TonieFileItem>().ToList();
                    if (selectedItemsCopy == null || selectedItemsCopy.Count == 0)
                    {
                        return;
                    }

                    var multiContextMenu = new ContextMenu
                    {
                        Background = Brushes.White
                    };

                    // Add style for black text
                    var style = new Style(x => x.OfType<MenuItem>());
                    style.Setters.Add(new Setter(MenuItem.ForegroundProperty, Brushes.Black));
                    multiContextMenu.Styles.Add(style);

                    var deleteMenuItem = new MenuItem
                    {
                        Header = $"Delete Selected ({selectionCount} items)"
                    };
                    deleteMenuItem.Click += async (s, args) =>
                    {
                        // Use the captured selection
                        viewModel.SelectedItems = new System.Collections.ArrayList(selectedItemsCopy);
                        await viewModel.DeleteMultipleTonieCommand.ExecuteAsync(null);
                    };

                    var removeLiveFlagMenuItem = new MenuItem
                    {
                        Header = $"Remove LIVE Flag ({selectionCount} items)"
                    };
                    removeLiveFlagMenuItem.Click += async (s, args) =>
                    {
                        // Use the captured selection
                        viewModel.SelectedItems = new System.Collections.ArrayList(selectedItemsCopy);
                        await viewModel.RemoveMultipleLiveFlagsCommand.ExecuteAsync(null);
                    };

                    // Check if any of the selected items are custom tonies
                    int customTonieCount = selectedItemsCopy.Count(item => item.IsCustomTonie);

                    if (customTonieCount > 0)
                    {
                        var bulkEditMenuItem = new MenuItem
                        {
                            Header = $"Bulk Edit Metadata ({customTonieCount} custom tonies)"
                        };
                        bulkEditMenuItem.Click += async (s, args) =>
                        {
                            // Use the captured selection
                            viewModel.SelectedItems = new System.Collections.ArrayList(selectedItemsCopy);
                            await viewModel.BulkEditMetadataCommand.ExecuteAsync(null);
                        };
                        multiContextMenu.Items.Add(bulkEditMenuItem);
                        multiContextMenu.Items.Add(new Separator());
                    }

                    multiContextMenu.Items.Add(removeLiveFlagMenuItem);
                    multiContextMenu.Items.Add(deleteMenuItem);

                    multiContextMenu.Open(border);
                }
            }
        }
    }

    private async void Border_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is Border border &&
            border.DataContext is TonieFileItem item &&
            DataContext is MainWindowViewModel viewModel)
        {
            // Open the player on double-click
            await viewModel.PlayTonieCommand.ExecuteAsync(item);
        }
    }

    private void TonieListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.UpdateSelectionState();
        }
    }

    private void MainWindow_KeyUp(object? sender, KeyEventArgs e)
    {
        // Ignore KeyUp when dialog is open - prevents spurious releases when focus changes
        // This ensures held keys stay "pressed" until dialog closes AND user releases them
        if (DataContext is MainWindowViewModel viewModel && viewModel.IsAnyDialogOpen)
        {
            return;
        }

        // Key released - allow it to be processed again
        _pressedKeys.Remove(e.Key);

        // If Alt key is released, check if we should enter or exit mnemonic mode
        if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
        {
            // Check if we should suppress this toggle (because KeyDown already handled it)
            if (_suppressNextAltToggle)
            {
                _suppressNextAltToggle = false;
                return;
            }

            if (_mnemonicModeActive)
            {
                // Alt released while already in mnemonic mode - exit it (toggle off)
                _ = ExitMnemonicMode();
            }
            else
            {
                // Alt released while not in mnemonic mode - enter it (toggle on)
                _mnemonicModeActive = true;

                // Cancel any existing mnemonic mode timer
                _mnemonicModeCancellation?.Cancel();
                _mnemonicModeCancellation = new System.Threading.CancellationTokenSource();

                // Exit mnemonic mode after 3 seconds
                var cancellationToken = _mnemonicModeCancellation.Token;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000, cancellationToken);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await ExitMnemonicMode();
                    }
                }, cancellationToken);
            }
        }
    }

    private async void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        // Check if Alt key is pressed or held, OR if we're in mnemonic mode
        // This allows mnemonics to work properly both when Alt is held and when Alt was just released
        bool isAltHeld = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        bool isMnemonicActive = isAltHeld || _mnemonicModeActive;

        // If Alt is pressed while in mnemonic mode, exit mnemonic mode (toggle behavior)
        if (_mnemonicModeActive && (e.Key == Key.LeftAlt || e.Key == Key.RightAlt))
        {
            await ExitMnemonicMode();
            // Set flag to suppress the KeyUp toggle that will follow
            _suppressNextAltToggle = true;
            e.Handled = true;
            return;
        }

        // If we're in mnemonic mode (Alt was just released or Alt is held),
        // we need to manually trigger mnemonic commands since Tunnel routing prevents AccessKey from working
        if (_mnemonicModeActive && e.Key != Key.LeftAlt && e.Key != Key.RightAlt)
        {
            // Check if this is a letter key that could be a mnemonic
            if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                // Manually handle mnemonics since Tunnel routing blocks Avalonia's AccessKey system
                var handled = await HandleMnemonicKey(e.Key, viewModel);
                if (handled)
                {
                    await ExitMnemonicMode();
                    e.Handled = true;
                    return;
                }
                else
                {
                    await ExitMnemonicMode();
                    // Fall through to normal processing
                }
            }
            else
            {
                await ExitMnemonicMode();
                // Fall through to normal processing for non-letter keys
            }
        }

        // Key debouncing: ignore repeated KeyDown events from holding a key
        // Do this AFTER mnemonic check so mnemonics aren't blocked by debouncing
        if (_pressedKeys.Contains(e.Key))
        {
            e.Handled = true;
            return;
        }

        // Mark this key as pressed
        _pressedKeys.Add(e.Key);

        // Disable main window shortcuts when any dialog is open
        if (viewModel.IsAnyDialogOpen)
        {
            return;
        }

        // ESC key - Clear search if active
        if (e.Key == Key.Escape && viewModel.IsSearchActive)
        {
            viewModel.ClearSearchCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Get the ListBox
        var listBox = this.FindControl<ListBox>("TonieListBox");
        if (listBox == null) return;

        // Check if exactly one item is selected
        int selectionCount = listBox.SelectedItems?.Count ?? 0;
        TonieFileItem? selectedItem = listBox.SelectedItem as TonieFileItem;

        // Handle all arrow keys for 2D grid navigation
        if ((e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right) && selectionCount == 1 && selectedItem != null)
        {
            NavigationDirection direction = e.Key switch
            {
                Key.Up => NavigationDirection.Up,
                Key.Down => NavigationDirection.Down,
                Key.Left => NavigationDirection.Left,
                Key.Right => NavigationDirection.Right,
                _ => NavigationDirection.Right
            };
            HandleGridNavigation(listBox, selectedItem, direction);
            e.Handled = true;
        }
        // F2 key - Rename (only for custom tonies)
        else if (e.Key == Key.F2 && selectionCount == 1 && selectedItem != null)
        {
            if (selectedItem.IsCustomTonie)
            {
                await viewModel.RenameSelectedTonieCommand.ExecuteAsync(selectedItem);
                e.Handled = true;
            }
        }
        // Space key - Open player
        else if (e.Key == Key.Space && selectionCount == 1 && selectedItem != null)
        {
            await viewModel.PlayTonieCommand.ExecuteAsync(selectedItem);
            e.Handled = true;
        }
        // Enter key - Modify contents
        else if (e.Key == Key.Enter && selectionCount == 1 && selectedItem != null)
        {
            await viewModel.ModifyContentsCommand.ExecuteAsync(selectedItem);
            e.Handled = true;
        }
        // Delete key - Delete tonie
        else if (e.Key == Key.Delete && selectionCount >= 1)
        {
            if (selectionCount == 1 && selectedItem != null)
            {
                // Single item deletion
                await viewModel.DeleteSelectedTonieCommand.ExecuteAsync(selectedItem);
                e.Handled = true;
            }
            else if (selectionCount > 1)
            {
                // Multiple item deletion
                await viewModel.DeleteMultipleTonieCommand.ExecuteAsync(null);
                e.Handled = true;
            }
        }
        // Handle text input for search (alphanumeric and basic punctuation)
        // Skip search input when Alt is active OR mnemonic mode is active to allow mnemonics to work
        else if (!isMnemonicActive && e.KeySymbol != null && !string.IsNullOrEmpty(e.KeySymbol) && IsSearchableCharacter(e.KeySymbol))
        {
            // Add character to search
            viewModel.HandleSearchInput(viewModel.SearchText + e.KeySymbol);
            e.Handled = true;
        }
        // Backspace - Remove last character from search
        else if (e.Key == Key.Back && viewModel.IsSearchActive)
        {
            if (viewModel.SearchText.Length > 0)
            {
                viewModel.HandleSearchInput(viewModel.SearchText[..^1]);
            }
            e.Handled = true;
        }
    }

    private bool IsSearchableCharacter(string keySymbol)
    {
        // Allow alphanumeric characters, spaces, and some basic punctuation
        if (keySymbol.Length != 1)
            return false;

        char c = keySymbol[0];
        return char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_';
    }

    private void HandleGridNavigation(ListBox listBox, TonieFileItem currentItem, NavigationDirection direction)
    {
        if (_cachedGrid == null || _cachedGrid.Count == 0)
            return;

        // Find current item's position in the cached grid
        int currentRow = -1;
        int currentCol = -1;

        for (int r = 0; r < _cachedGrid.Count; r++)
        {
            for (int c = 0; c < _cachedGrid[r].Count; c++)
            {
                if (_cachedGrid[r][c] == currentItem)
                {
                    currentRow = r;
                    currentCol = c;
                    break;
                }
            }
            if (currentRow >= 0) break;
        }

        if (currentRow < 0)
            return;

        // Calculate target position based on direction
        int targetRow = currentRow;
        int targetCol = currentCol;
        TonieFileItem? targetItem = null;

        switch (direction)
        {
            case NavigationDirection.Left:
                if (currentCol > 0)
                {
                    targetItem = _cachedGrid[currentRow][currentCol - 1];
                }
                break;

            case NavigationDirection.Right:
                if (currentCol < _cachedGrid[currentRow].Count - 1)
                {
                    targetItem = _cachedGrid[currentRow][currentCol + 1];
                }
                break;

            case NavigationDirection.Up:
                if (currentRow > 0)
                {
                    targetRow = currentRow - 1;
                    targetCol = Math.Min(currentCol, _cachedGrid[targetRow].Count - 1);
                    targetItem = _cachedGrid[targetRow][targetCol];
                }
                break;

            case NavigationDirection.Down:
                if (currentRow < _cachedGrid.Count - 1)
                {
                    targetRow = currentRow + 1;
                    targetCol = Math.Min(currentCol, _cachedGrid[targetRow].Count - 1);
                    targetItem = _cachedGrid[targetRow][targetCol];
                }
                break;
        }

        // Select the target item if found
        if (targetItem != null)
        {
            listBox.SelectedItem = targetItem;
            listBox.ScrollIntoView(targetItem);
        }
    }

    private async void UpdateGridPositions()
    {
        // Wait for UI to render
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // Give the UI a moment to fully render the items
            await Task.Delay(200);

            var listBox = this.FindControl<ListBox>("TonieListBox");
            if (listBox == null || listBox.ItemCount == 0)
                return;

            // Get all item containers with their visual positions
            var itemsWithPositions = new List<(int index, double x, double y, TonieFileItem item)>();

            for (int i = 0; i < listBox.ItemCount; i++)
            {
                var container = listBox.ContainerFromIndex(i) as Control;
                if (container != null)
                {
                    var position = container.TranslatePoint(new Point(0, 0), listBox);
                    if (position.HasValue)
                    {
                        var item = listBox.Items.Cast<object>().ElementAtOrDefault(i) as TonieFileItem;
                        if (item != null)
                        {
                            itemsWithPositions.Add((i, position.Value.X, position.Value.Y, item));
                        }
                    }
                }
            }

            if (itemsWithPositions.Count == 0)
                return;

            // Group items into rows based on Y position
            var rows = new List<List<(int index, double x, double y, TonieFileItem item)>>();
            var sortedByY = itemsWithPositions.OrderBy(x => x.y).ToList();

            double rowTolerance = 10;
            List<(int index, double x, double y, TonieFileItem item)>? currentRow = null;
            double currentRowY = 0;

            foreach (var item in sortedByY)
            {
                if (currentRow == null || Math.Abs(item.y - currentRowY) > rowTolerance)
                {
                    currentRow = new List<(int index, double x, double y, TonieFileItem item)>();
                    rows.Add(currentRow);
                    currentRowY = item.y;
                }
                currentRow.Add(item);
            }

            // Sort items within each row by X position
            foreach (var row in rows)
            {
                row.Sort((a, b) => a.x.CompareTo(b.x));
            }

            // Cache the grid structure for navigation
            _cachedGrid = new List<List<TonieFileItem>>();
            for (int r = 0; r < rows.Count; r++)
            {
                var gridRow = new List<TonieFileItem>();
                for (int c = 0; c < rows[r].Count; c++)
                {
                    gridRow.Add(rows[r][c].item);
                    rows[r][c].item.GridPosition = $"[{r}:{c}]";
                }
                _cachedGrid.Add(gridRow);
            }
        }, global::Avalonia.Threading.DispatcherPriority.Background);
    }

    private void FilePath_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Check if Ctrl key is pressed
        var pointerProps = e.GetCurrentPoint(this);
        var modifiers = e.KeyModifiers;

        if (modifiers.HasFlag(KeyModifiers.Control) && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.OpenContainingFolderCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async Task ExitMnemonicMode()
    {
        // Cancel the timeout timer
        _mnemonicModeCancellation?.Cancel();
        _mnemonicModeActive = false;

        // Note: The mnemonic underlines are controlled by Avalonia's internal AccessKey system
        // They automatically clear when the user clicks anywhere, but cannot be programmatically
        // cleared through focus changes or synthetic events. This is a known Avalonia limitation.
        // The underlines are purely cosmetic and don't affect functionality.

        // Focus the ListBox to ensure keyboard navigation works
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var listBox = this.FindControl<ListBox>("TonieListBox");
            if (listBox != null)
            {
                // If there's a selected item, try to focus its container
                if (listBox.SelectedItem != null && listBox.SelectedIndex >= 0)
                {
                    var container = listBox.ContainerFromIndex(listBox.SelectedIndex) as Control;
                    if (container != null)
                    {
                        container.Focus();
                        return;
                    }
                }

                // Otherwise focus the ListBox itself
                listBox.Focus();
            }
        });
    }

    private async Task<bool> HandleMnemonicKey(Key key, MainWindowViewModel viewModel)
    {
        // Map keys to their corresponding mnemonic commands
        // Based on the button Content properties with underscore prefixes
        switch (key)
        {
            case Key.O: // _Open Directory
                if (viewModel.OpenDirectoryCommand.CanExecute(null))
                {
                    await viewModel.OpenDirectoryCommand.ExecuteAsync(null);
                    return true;
                }
                break;

            case Key.A: // _Add Custom Tonie
                if (viewModel.HasValidDirectory && viewModel.AddCustomTonieCommand.CanExecute(null))
                {
                    await viewModel.AddCustomTonieCommand.ExecuteAsync(null);
                    return true;
                }
                break;

            case Key.D: // _Decode Selected
                if (viewModel.DecodeSelectedCommand.CanExecute(null))
                {
                    await viewModel.DecodeSelectedCommand.ExecuteAsync(null);
                    return true;
                }
                break;

            case Key.S: // _Show Info
                if (viewModel.ShowInfoCommand.CanExecute(null))
                {
                    await viewModel.ShowInfoCommand.ExecuteAsync(null);
                    return true;
                }
                break;

            case Key.R: // _Refresh
                if (viewModel.RefreshCommand.CanExecute(null))
                {
                    await viewModel.RefreshCommand.ExecuteAsync(null);
                    return true;
                }
                break;

            case Key.L: // Remove All _LIVE Flags
                if (viewModel.RemoveAllLiveFlagsCommand.CanExecute(null))
                {
                    await viewModel.RemoveAllLiveFlagsCommand.ExecuteAsync(null);
                    return true;
                }
                break;

            case Key.T: // _TRASHCAN Recovery
                if (viewModel.HasValidDirectory && viewModel.OpenTrashcanManagerCommand.CanExecute(null))
                {
                    await viewModel.OpenTrashcanManagerCommand.ExecuteAsync(null);
                    return true;
                }
                break;
        }

        return false;
    }
}
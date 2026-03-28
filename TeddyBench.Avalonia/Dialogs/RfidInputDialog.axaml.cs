using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace TeddyBench.Avalonia.Dialogs;

public partial class RfidInputDialog : Window
{
    private TextBox? _rfidInput;

    public RfidInputDialog()
    {
        InitializeComponent();
    }

    public RfidInputDialog(string rfidPrefix, string currentDirectory) : this()
    {
        DataContext = new RfidInputDialogViewModel(rfidPrefix, currentDirectory);

        // Note: Alt+O and Alt+C are handled by button mnemonics automatically
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Focus and position cursor at end when dialog opens
        this.Opened += (s, e) =>
        {
            _rfidInput = this.FindControl<TextBox>("RfidInput");
            if (_rfidInput != null)
            {
                _rfidInput.CaretIndex = _rfidInput.Text?.Length ?? 0;
                _rfidInput.Focus();

                // Handle text input to convert to uppercase
                _rfidInput.PropertyChanged += OnRfidInputPropertyChanged;
            }
        };
    }

    private void OnRfidInputPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(TextBox.Text) && _rfidInput != null)
        {
            var text = _rfidInput.Text;
            if (!string.IsNullOrEmpty(text))
            {
                // Filter to only hex characters and convert to uppercase
                var filteredText = FilterToHexAndUppercase(text);

                if (text != filteredText)
                {
                    var caretIndex = _rfidInput.CaretIndex;
                    _rfidInput.Text = filteredText;
                    _rfidInput.CaretIndex = caretIndex;

                    // Update the ViewModel
                    if (DataContext is RfidInputDialogViewModel vm)
                    {
                        vm.RfidUid = filteredText;
                    }
                }
            }
        }
    }

    private static string FilterToHexAndUppercase(string input)
    {
        var result = new System.Text.StringBuilder(input.Length);
        foreach (char c in input)
        {
            if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))
            {
                result.Append(c);
            }
            else if (c >= 'a' && c <= 'f')
            {
                result.Append(char.ToUpper(c));
            }
            // Skip any non-hex characters
        }
        return result.ToString();
    }

    private void OnOkClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    public string? GetRfidUid()
    {
        return (DataContext as RfidInputDialogViewModel)?.RfidUid;
    }
}

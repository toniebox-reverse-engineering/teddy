using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace TeddyBench.Avalonia.Dialogs;

public partial class HexInputDialog : Window
{
    private TextBox? _hexInput;

    public HexInputDialog()
    {
        InitializeComponent();
        DataContext = new HexInputDialogViewModel();

        // Note: Alt+O and Alt+C are handled by button mnemonics automatically
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Focus and position cursor at end when dialog opens
        this.Opened += (s, e) =>
        {
            _hexInput = this.FindControl<TextBox>("HexInput");
            if (_hexInput != null)
            {
                _hexInput.CaretIndex = _hexInput.Text?.Length ?? 0;
                _hexInput.Focus();

                // Handle text input to convert to uppercase
                _hexInput.PropertyChanged += OnHexInputPropertyChanged;
            }
        };
    }

    private void OnHexInputPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(TextBox.Text) && _hexInput != null)
        {
            var text = _hexInput.Text;
            if (!string.IsNullOrEmpty(text))
            {
                // Filter to only hex characters and convert to uppercase
                var filteredText = FilterToHexAndUppercase(text);

                if (text != filteredText)
                {
                    var caretIndex = _hexInput.CaretIndex;
                    _hexInput.Text = filteredText;
                    _hexInput.CaretIndex = caretIndex;

                    // Update the ViewModel
                    if (DataContext is HexInputDialogViewModel vm)
                    {
                        vm.HexValue = filteredText;
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

    public uint? GetValue()
    {
        return (DataContext as HexInputDialogViewModel)?.Value;
    }
}
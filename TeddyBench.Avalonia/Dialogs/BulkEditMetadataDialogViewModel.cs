using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.RegularExpressions;

namespace TeddyBench.Avalonia.Dialogs;

public partial class BulkEditMetadataDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private string _series = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _language = string.Empty;

    [ObservableProperty]
    private string _languageError = string.Empty;

    [ObservableProperty]
    private bool _isValid = true;

    public bool DialogResult { get; private set; }

    public bool HasLanguageError => !string.IsNullOrEmpty(LanguageError);

    public BulkEditMetadataDialogViewModel(int selectedCount)
    {
        SelectedCount = selectedCount;

        // Validate on property changes
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Language))
            {
                Validate();
            }
        };
    }

    private void Validate()
    {
        bool isValid = true;

        // Validate Language (optional, but must be valid format if provided)
        LanguageError = string.Empty;
        if (!string.IsNullOrWhiteSpace(Language))
        {
            // Language should be in format like "en-us", "de-de", etc.
            var languageRegex = new Regex(@"^[a-z]{2}-[a-z]{2}$", RegexOptions.IgnoreCase);
            if (!languageRegex.IsMatch(Language))
            {
                LanguageError = "Language must be in format 'xx-xx' (e.g., 'en-us', 'de-de')";
                isValid = false;
            }
        }

        IsValid = isValid;
        OnPropertyChanged(nameof(HasLanguageError));
    }

    [RelayCommand]
    private void Apply()
    {
        DialogResult = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }
}

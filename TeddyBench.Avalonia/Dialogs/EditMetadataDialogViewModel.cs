using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TeddyBench.Avalonia.Models;

namespace TeddyBench.Avalonia.Dialogs;

public partial class EditMetadataDialogViewModel : ObservableObject
{
    private readonly TonieMetadata _originalMetadata;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _series = string.Empty;

    [ObservableProperty]
    private string _episodes = string.Empty;

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private string _audioIdText = string.Empty;

    [ObservableProperty]
    private string _tracksText = string.Empty;

    [ObservableProperty]
    private string _language = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _release = string.Empty;

    [ObservableProperty]
    private string _pic = string.Empty;

    [ObservableProperty]
    private string _audioIdError = string.Empty;

    [ObservableProperty]
    private string _languageError = string.Empty;

    [ObservableProperty]
    private bool _isValid = true;

    public bool DialogResult { get; private set; }

    public bool HasAudioIdError => !string.IsNullOrEmpty(AudioIdError);
    public bool HasLanguageError => !string.IsNullOrEmpty(LanguageError);

    public EditMetadataDialogViewModel(TonieMetadata metadata)
    {
        _originalMetadata = metadata;

        // Populate fields from existing metadata
        Title = metadata.Title ?? string.Empty;
        Series = metadata.Series ?? string.Empty;
        Episodes = metadata.Episodes ?? string.Empty;
        Model = metadata.Model ?? string.Empty;
        AudioIdText = string.Join(", ", metadata.AudioId ?? new List<string>());
        TracksText = string.Join(Environment.NewLine, metadata.Tracks ?? new List<string>());
        Language = metadata.Language ?? string.Empty;
        Category = metadata.Category ?? string.Empty;
        Release = metadata.Release ?? string.Empty;
        Pic = metadata.Pic ?? string.Empty;

        // Validate on property changes
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Title) ||
                e.PropertyName == nameof(AudioIdText) ||
                e.PropertyName == nameof(Language))
            {
                Validate();
            }
        };
    }

    private void Validate()
    {
        bool isValid = true;

        // Validate Title (required)
        if (string.IsNullOrWhiteSpace(Title))
        {
            isValid = false;
        }

        // Validate Audio IDs (optional, but must be valid if provided)
        AudioIdError = string.Empty;
        if (!string.IsNullOrWhiteSpace(AudioIdText))
        {
            var audioIds = AudioIdText.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            foreach (var id in audioIds)
            {
                // Audio IDs should be numeric
                if (!ulong.TryParse(id, out _))
                {
                    AudioIdError = $"Invalid audio ID: '{id}'. Must be numeric.";
                    isValid = false;
                    break;
                }
            }
        }

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
        OnPropertyChanged(nameof(HasAudioIdError));
        OnPropertyChanged(nameof(HasLanguageError));
    }

    [RelayCommand]
    private void Save()
    {
        DialogResult = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }

    public TonieMetadata GetUpdatedMetadata()
    {
        // Parse Audio IDs
        var audioIds = new List<string>();
        if (!string.IsNullOrWhiteSpace(AudioIdText))
        {
            audioIds = AudioIdText.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        // Parse Tracks
        var tracks = new List<string>();
        if (!string.IsNullOrWhiteSpace(TracksText))
        {
            tracks = TracksText.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        // Create updated metadata, preserving No and Hash from original
        return new TonieMetadata
        {
            No = _originalMetadata.No,
            Hash = _originalMetadata.Hash,
            Title = Title.Trim(),
            Series = Series.Trim(),
            Episodes = Episodes.Trim(),
            Model = Model.Trim(),
            AudioId = audioIds,
            Tracks = tracks,
            Language = Language.Trim(),
            Category = Category.Trim(),
            Release = Release.Trim(),
            Pic = Pic.Trim()
        };
    }
}

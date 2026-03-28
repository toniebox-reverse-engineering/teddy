using CommunityToolkit.Mvvm.ComponentModel;

namespace TeddyBench.Avalonia.Models;

public partial class TonieFileItem : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _directoryName = string.Empty;

    [ObservableProperty]
    private string _infoText = string.Empty;

    [ObservableProperty]
    private string? _imagePath;

    [ObservableProperty]
    private bool _isLive;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isCustomTonie;

    [ObservableProperty]
    private string _gridPosition = string.Empty;

    [ObservableProperty]
    private string _hash = string.Empty;
}

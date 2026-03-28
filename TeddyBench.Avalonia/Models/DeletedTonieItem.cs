using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace TeddyBench.Avalonia.Models;

public partial class DeletedTonieItem : ObservableObject
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _uid = string.Empty;

    [ObservableProperty]
    private DateTime _deletionDate;

    [ObservableProperty]
    private string _hash = string.Empty;

    [ObservableProperty]
    private string? _imagePath;

    [ObservableProperty]
    private string _audioId = string.Empty;

    [ObservableProperty]
    private string _duration = string.Empty;

    [ObservableProperty]
    private int _trackCount;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isCustomTonie;

    /// <summary>
    /// The directory where this file is currently located (e.g., "E40")
    /// </summary>
    [ObservableProperty]
    private string _trashcanDirectory = string.Empty;
}
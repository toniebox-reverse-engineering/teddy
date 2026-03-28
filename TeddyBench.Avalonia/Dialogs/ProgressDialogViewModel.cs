using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading;

namespace TeddyBench.Avalonia.Dialogs;

public partial class ProgressDialogViewModel : ObservableObject
{
    private readonly Window _dialog;

    [ObservableProperty]
    private string _title = "Encoding...";

    [ObservableProperty]
    private string _currentFileStatus = "";

    [ObservableProperty]
    private double _overallProgress = 0.0;

    [ObservableProperty]
    private double _currentFileProgress = 0.0;

    [ObservableProperty]
    private bool _showOverallProgress = true;

    [ObservableProperty]
    private bool _showCurrentFileProgress = true;

    [ObservableProperty]
    private string _overallProgressText = "Overall: 0%";

    [ObservableProperty]
    private string _currentFileProgressText = "Current file: 0%";

    private int _totalFiles = 0;
    private int _currentFileNumber = 0;

    public ProgressDialogViewModel(Window dialog, int totalFiles)
    {
        _dialog = dialog;
        _totalFiles = totalFiles;

        // Show overall progress only when encoding multiple files
        ShowOverallProgress = totalFiles > 1;
    }

    public void UpdateFileStart(int fileNumber, string fileName)
    {
        _currentFileNumber = fileNumber;

        Dispatcher.UIThread.Post(() =>
        {
            CurrentFileStatus = $"Encoding file {fileNumber}/{_totalFiles}: {fileName}";
            CurrentFileProgress = 0.0;
            CurrentFileProgressText = "Current file: 0%";

            if (ShowOverallProgress)
            {
                OverallProgress = ((double)(fileNumber - 1) / _totalFiles) * 100.0;
                OverallProgressText = $"Overall: {OverallProgress:F0}%";
            }
        });
    }

    public void UpdateFileProgress(decimal progress)
    {
        double progressPercent = (double)progress * 100.0;

        // Use Invoke instead of Post to ensure immediate UI update
        // This prevents updates from being batched together
        Dispatcher.UIThread.Invoke(() =>
        {
            CurrentFileProgress = progressPercent;
            CurrentFileProgressText = $"Current file: {progressPercent:F0}%";
        });
    }

    public void UpdateFileDone()
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentFileProgress = 100.0;
            CurrentFileProgressText = "Current file: 100%";

            if (ShowOverallProgress)
            {
                OverallProgress = ((double)_currentFileNumber / _totalFiles) * 100.0;
                OverallProgressText = $"Overall: {OverallProgress:F0}%";
            }
        });
    }

    public void UpdatePostProcessing(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentFileStatus = message;
        });
    }

    public void Complete()
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentFileProgress = 100.0;
            OverallProgress = 100.0;
            CurrentFileProgressText = "Current file: 100%";
            OverallProgressText = "Overall: 100%";
            CurrentFileStatus = "Encoding complete!";

            // Close dialog after a brief delay
            Thread.Sleep(500);
            _dialog.Close();
        });
    }
}

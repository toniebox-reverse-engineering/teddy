using System;
using System.IO;
using TonieFile;

namespace TeddyBench.Avalonia.Services;

/// <summary>
/// Custom EncodeCallback that reports progress to the ProgressDialogViewModel.
/// This allows the Avalonia UI to show real-time progress during encoding.
/// </summary>
public class AvaloniaEncodeCallback : TonieAudio.EncodeCallback
{
    private readonly Dialogs.ProgressDialogViewModel _progressViewModel;
    private int _currentTrack = 0;

    public AvaloniaEncodeCallback(Dialogs.ProgressDialogViewModel progressViewModel)
    {
        _progressViewModel = progressViewModel;
    }

    public override void FileStart(int track, string sourceFile)
    {
        _currentTrack = track;
        ParseName(track, sourceFile);

        // Report to progress dialog
        _progressViewModel.UpdateFileStart(track, ShortName);
    }

    public override void Progress(decimal pct)
    {
        // Report progress to the dialog
        _progressViewModel.UpdateFileProgress(pct);
    }

    public override void FileDone()
    {
        // Report file completion
        _progressViewModel.UpdateFileDone();
    }

    public override void FileFailed(string message)
    {
        // Could add error handling here if needed
        _progressViewModel.UpdateFileDone();
    }

    public override void Failed(string message)
    {
        // Could add error handling here if needed
    }

    public override void Warning(string message)
    {
        // Could add warning display here if needed
    }

    public override void PostProcessing(string message)
    {
        // Report post-processing status to the dialog
        _progressViewModel.UpdatePostProcessing(message);
    }
}

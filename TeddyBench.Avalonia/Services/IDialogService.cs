using System;
using System.Threading;
using System.Threading.Tasks;
using TeddyBench.Core;

namespace TeddyBench.Avalonia.Services
{
    public class ReportResult
    {
        public string Username;
        public string Message;
    }

    /// <summary>A modeless "operation in progress" window (e.g. while reading a tag).</summary>
    public interface IOperationHandle
    {
        /// <summary>Signalled when the user pressed Cancel.</summary>
        CancellationToken Cancelled { get; }
        void Close();
    }

    /// <summary>
    /// All dialogs the main window needs. Implemented with Avalonia windows by <see cref="DialogService"/>,
    /// and by a scripted fake in the tests. All methods must be called on the UI thread.
    /// </summary>
    public interface IDialogService
    {
        Task ShowMessage(string text, string title = "TeddyBench", bool error = false);
        Task<bool> AskYesNo(string text, string title);
        /// <summary>true = yes, false = no, null = cancel.</summary>
        Task<bool?> AskYesNoCancel(string text, string title);

        /// <summary>UID of the tag to assign a file to (pre-filled from the NFC reader if one is connected); null if cancelled.</summary>
        Task<string> AskUid(RfidReaderBase reader, string initialUid = null);
        /// <summary>A hex audio ID; null if cancelled.</summary>
        Task<uint?> AskAudioId(string initialHex);
        /// <summary>Free text (e.g. a new name); null if cancelled.</summary>
        Task<string> AskText(string title, string prompt, string initial);

        /// <param name="filterName">e.g. "Firmware ELF files"; null for all files.</param>
        /// <param name="patterns">e.g. "*.elf".</param>
        Task<string[]> PickFiles(string title, bool allowMultiple, string filterName = null, params string[] patterns);
        Task<string> PickFolder(string title);

        /// <summary>Shows a diagnostic report for review; null if the user does not want to send it.</summary>
        Task<ReportResult> ShowReport(string content, string defaultUsername);
        /// <summary>Lets the user order MP3/Ogg files; null if cancelled.</summary>
        Task<string[]> SortTracks(string[] files);
        /// <summary>Shows (<paramref name="display"/>) or asks for a tag dump (hex); null if cancelled.</summary>
        Task<string> ShowTagDump(bool display, string initial);
        Task<bool> AskUpdate(string version, string releaseName, bool canInstall);

        Task ShowAbout(string version);
        Task ShowAntennaPlot(Proxmark3.MeasurementResult result);
        IOperationHandle ShowOperation(string title, bool cancellable);

        void SetLogWindowVisible(bool visible);
    }

    /// <summary>Access to the UI thread. Core events arrive on worker threads.</summary>
    public interface IUiDispatcher
    {
        void Post(Action action);
        /// <summary>Runs an async UI action and returns its result; may be awaited or (from worker threads only) blocked on.</summary>
        Task<T> InvokeAsync<T>(Func<Task<T>> action);
    }
}

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TeddyBench.Avalonia.Views.Dialogs;
using TeddyBench.Core;

namespace TeddyBench.Avalonia.Services
{
    /// <summary><see cref="IDialogService"/> with Avalonia windows, owned by the main window.</summary>
    public class DialogService : IDialogService
    {
        private readonly Window Owner;
        private LogWindow Log;

        public DialogService(Window owner)
        {
            Owner = owner;
            Owner.Closed += (s, e) =>
            {
                if (Log != null)
                {
                    Log.AllowClose = true;
                    Log.Close();
                }
            };
        }

        private async Task<T> Show<T>(T dialog) where T : Window
        {
            if (Owner.IsVisible)
            {
                await dialog.ShowDialog(Owner);
            }
            else
            {
                /* e.g. a crash report before the main window is shown */
                var closed = new TaskCompletionSource<bool>();
                dialog.Closed += (s, e) => closed.TrySetResult(true);
                dialog.Show();
                await closed.Task;
            }
            return dialog;
        }

        public async Task ShowMessage(string text, string title = "TeddyBench", bool error = false)
        {
            await Show(new MessageDialog(text, title, MessageButtons.Ok, error));
        }

        public async Task<bool> AskYesNo(string text, string title)
        {
            return (await Show(new MessageDialog(text, title, MessageButtons.YesNo))).Result == "yes";
        }

        public async Task<bool?> AskYesNoCancel(string text, string title)
        {
            string result = (await Show(new MessageDialog(text, title, MessageButtons.YesNoCancel))).Result;
            return result == null ? (bool?)null : result == "yes";
        }

        public async Task<string> AskUid(RfidReaderBase reader, string initialUid = null)
        {
            return (await Show(new UidDialog(reader, initialUid))).Result?.ToUpperInvariant();
        }

        public async Task<uint?> AskAudioId(string initialHex)
        {
            var dialog = new InputDialog("Enter value", "Please enter the audio ID you want (8 hex digits)", initialHex,
                s => s.Length == 8 && s.All(Uri.IsHexDigit) ? null : "The ID must have 8 hex digits (" + s.Length + "/8)");
            string text = (await Show(dialog)).Result;
            return text == null ? (uint?)null : uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        public async Task<string> AskText(string title, string prompt, string initial)
        {
            return (await Show(new InputDialog(title, prompt, initial, s => string.IsNullOrWhiteSpace(s) ? "Please enter a name" : null))).Result;
        }

        public async Task<string[]> PickFiles(string title, bool allowMultiple, string filterName = null, params string[] patterns)
        {
            var types = new List<FilePickerFileType>();
            if (filterName != null && patterns != null && patterns.Length > 0)
            {
                types.Add(new FilePickerFileType(filterName) { Patterns = patterns });
            }
            types.Add(FilePickerFileTypes.All);

            var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = allowMultiple,
                FileTypeFilter = types
            });
            return files.Select(f => f.TryGetLocalPath()).Where(p => p != null).ToArray();
        }

        public async Task<string> PickFolder(string title)
        {
            var folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            return folders.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => p != null);
        }

        public async Task<ReportResult> ShowReport(string content, string defaultUsername)
        {
            return (await Show(new ReportDialog(content, defaultUsername))).Result;
        }

        public async Task<string[]> SortTracks(string[] files)
        {
            return (await Show(new TrackSortDialog(files))).Result;
        }

        public async Task<string> ShowTagDump(bool display, string initial)
        {
            return (await Show(new TagDumpDialog(display, initial))).Result;
        }

        public async Task<bool> AskUpdate(string version, string releaseName, bool canInstall)
        {
            return (await Show(new UpdateDialog(version, releaseName, canInstall))).Result == "yes";
        }

        public async Task ShowAbout(string version)
        {
            await Show(new AboutDialog(version));
        }

        public async Task ShowAntennaPlot(Proxmark3.MeasurementResult result)
        {
            await Show(new AntennaPlotWindow(result));
        }

        public IOperationHandle ShowOperation(string title, bool cancellable)
        {
            var dialog = new OperationDialog(title, cancellable);
            dialog.Show(Owner);
            return dialog;
        }

        public void SetLogWindowVisible(bool visible)
        {
            if (visible)
            {
                Log = Log ?? new LogWindow();
                Log.Show();
            }
            else
            {
                Log?.Hide();
            }
        }
    }
}

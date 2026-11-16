using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TeddyBench.Core;
using TonieFile;

namespace TeddyBench.Avalonia.ViewModels
{
    public partial class MainWindowViewModel
    {
        private bool HasSelection => SelectedItems.Count > 0;
        private bool HasSingleSelection => SelectedItems.Count == 1;
        private bool CanModifySelection => HasSelection && IsIdle;
        private bool CanModifySingle => HasSingleSelection && IsIdle;
        private bool CanAddFiles => IsCardOpen && IsIdle;
        private bool CanPlayStop => CanPlay && (Services.Player.IsPlaying || HasSingleSelection);

        partial void OnIsBusyChanged(bool value) => NotifySelectionCommands();
        partial void OnCurrentDirectoryChanged(string value) => NotifySelectionCommands();

        private void NotifySelectionCommands()
        {
            AddFilesCommand.NotifyCanExecuteChanged();
            DeleteSelectedCommand.NotifyCanExecuteChanged();
            SaveSelectedCommand.NotifyCanExecuteChanged();
            ExportSelectedCommand.NotifyCanExecuteChanged();
            ReassignSelectedCommand.NotifyCanExecuteChanged();
            ToggleLiveCommand.NotifyCanExecuteChanged();
            RenameCommand.NotifyCanExecuteChanged();
            RevealCommand.NotifyCanExecuteChanged();
            ReportSelectedCommand.NotifyCanExecuteChanged();
            PlayStopCommand.NotifyCanExecuteChanged();
        }

        private static bool IsAudioFile(string file)
        {
            string ext = Path.GetExtension(file);
            return ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase) || ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase);
        }

        [RelayCommand]
        private async Task OpenDirectory()
        {
            string dir = await Dialogs.PickFolder("Open directory");
            if (dir != null)
            {
                OpenPath(dir);
            }
        }

        [RelayCommand]
        private void Exit()
        {
            RequestClose();
        }

        [RelayCommand(CanExecute = nameof(CanAddFiles))]
        private async Task AddFiles()
        {
            string[] files = await Dialogs.PickFiles("Add files", true);
            if (files != null && files.Length > 0)
            {
                await AddFilesAsync(files, await AskAudioId());
            }
        }

        /// <summary>Files dropped onto the window.</summary>
        public async Task DropFiles(string[] files)
        {
            if (files == null || files.Length == 0 || !CanAddFiles)
            {
                if (!IsCardOpen)
                {
                    await Dialogs.ShowMessage("Please open the SD card (or a directory) first.", "Add file...");
                }
                return;
            }
            await AddFilesAsync(files, await AskAudioId());
        }

        private async Task<uint> AskAudioId()
        {
            if (!await Dialogs.AskYesNo("Do you want to set a specific Audio-ID? If you don't know, just say 'No'.", "Set specific Audio ID"))
            {
                return uint.MaxValue;
            }

            uint? id = await Dialogs.AskAudioId(CardContent.NewCustomAudioId().ToString("X8"));
            return id ?? uint.MaxValue;
        }

        /// <summary>Asks for the tag UID, then encodes MP3/Ogg files or copies an existing tonie file.</summary>
        internal async Task AddFilesAsync(string[] fileNames, uint id = uint.MaxValue)
        {
            string uid = await Dialogs.AskUid(RfidReader);
            if (uid == null)
            {
                return;
            }

            if (fileNames.Length == 1)
            {
                string fileName = fileNames[0];

                if (IsAudioFile(fileName))
                {
                    if (await Dialogs.AskYesNo("You are about to encode a single MP3/Ogg, is this right?", "Encode a file"))
                    {
                        await EncodeFiles(uid, new[] { fileName }, id);
                    }
                    return;
                }

                bool valid;
                try
                {
                    valid = TonieAudio.FromFile(fileName).FileContent.Length > 0;
                }
                catch (Exception ex)
                {
                    valid = false;
                }

                if (!valid)
                {
                    await Dialogs.ShowMessage("The file you have chosen is not supported.", "Add file...");
                    return;
                }
                await CopyFile(uid, fileName);
                return;
            }

            if (fileNames.Any(f => !IsAudioFile(f)))
            {
                await Dialogs.ShowMessage("Please select MP3/Ogg files only.", "Add file...");
                return;
            }

            string[] sorted = await Dialogs.SortTracks(fileNames);
            if (sorted == null)
            {
                return;
            }
            await EncodeFiles(uid, sorted, id);
        }

        private Task CopyFile(string uid, string fileName)
        {
            return RunCardOperation(card => card.CopyTonieFile(uid, fileName), showLog: false);
        }

        private Task EncodeFiles(string uid, string[] files, uint id)
        {
            return RunCardOperation(card =>
            {
                TonieAudio audio = CardContent.Encode(files, id, new LogEncodeCallback(this));
                card.WriteTonieFile(uid, audio.FileContent);
            }, showLog: true);
        }

        /// <summary>Runs a write operation on a worker thread, reports errors, then rescans the card.</summary>
        private async Task RunCardOperation(Action<CardContent> operation, bool showLog)
        {
            var card = new CardContent(CurrentDirectory);
            EncodeLog = "";
            IsBusy = true;
            ShowEncodeLog = showLog;

            try
            {
                await Task.Run(() => operation(card));
            }
            catch (CardOperationException ex)
            {
                await Dialogs.ShowMessage(ex.Message, ex.Title ?? "TeddyBench", true);
            }
            finally
            {
                IsBusy = false;
                ShowEncodeLog = false;
                RefreshCardContent();
            }
        }

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private bool showEncodeLog;

        internal void AppendEncodeLog(string text)
        {
            Ui.Post(() => EncodeLog += text);
        }

        /// <summary>Encoder progress as text: " File: name [....25%....50%...]".</summary>
        private class LogEncodeCallback : TonieAudio.EncodeCallback
        {
            private readonly MainWindowViewModel Owner;
            private int LastPct = 0;

            public LogEncodeCallback(MainWindowViewModel owner)
            {
                Owner = owner;
            }

            public override void FileStart(int track, string sourceFile)
            {
                ParseName(track, sourceFile);
                Owner.AppendEncodeLog(" Track " + track.ToString().PadLeft(3) + " - " + ShortName + "  [");
            }

            public override void FileDone() => Owner.AppendEncodeLog("]" + Environment.NewLine);
            public override void FileFailed(string message) => Owner.AppendEncodeLog("] FAILED: " + message + Environment.NewLine);
            public override void Failed(string message) => Owner.AppendEncodeLog("] FAILED: " + message + Environment.NewLine);
            public override void Warning(string message) => Owner.AppendEncodeLog(Environment.NewLine + "Warning: " + message + Environment.NewLine);

            public override void Progress(decimal pct)
            {
                int now = (int)(pct * 20);
                if (now == LastPct)
                {
                    return;
                }
                LastPct = now;
                if (now % 5 == 0)
                {
                    if (now != 20)
                    {
                        Owner.AppendEncodeLog((now * 5) + "%");
                    }
                }
                else
                {
                    Owner.AppendEncodeLog(".");
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task DeleteSelected()
        {
            var items = SelectedItems.ToList();
            bool deleteAll = false;

            if (items.Count > 1)
            {
                bool? answer = await Dialogs.AskYesNoCancel("Delete all selected files?", "Delete files?");
                if (answer == null)
                {
                    return;
                }
                deleteAll = answer.Value;
            }

            StopAnalyzeThread();
            try
            {
                foreach (var item in items)
                {
                    if (!deleteAll && !await Dialogs.AskYesNo("Delete '" + item.Text + "'?", "Delete files?"))
                    {
                        continue;
                    }

                    try
                    {
                        CardContent.Delete(item.Entry);
                    }
                    catch (CardOperationException ex)
                    {
                        await Dialogs.ShowMessage(ex.Message, "Delete files", true);
                        return;
                    }
                }
            }
            finally
            {
                RefreshCardContent();
            }
        }

        [RelayCommand(CanExecute = nameof(CanModifySelection))]
        private async Task SaveSelected()
        {
            var items = SelectedItems.ToList();

            bool? createDirs = await Dialogs.AskYesNoCancel("Create directories for selected file(s)?", "Save audio content");
            if (createDirs == null)
            {
                return;
            }

            string outputLocation = await Dialogs.PickFolder("Save audio content to");
            if (outputLocation == null)
            {
                return;
            }

            await Dialogs.ShowMessage("You are about to export audio content of the tonie files on your SD card. " +
                "Please *do not* share these files as they could contain information which can be used to identify your tonie ID. " +
                "Also be aware that sharing these files is most likely illegal in your country.", "Legal information");

            IsBusy = true;
            try
            {
                foreach (var item in items)
                {
                    await Task.Run(() => CardContent.SaveAudio(item.Entry, outputLocation, createDirs.Value, Services.Database, Services.Version));
                }
            }
            catch (CardOperationException ex)
            {
                await Dialogs.ShowMessage(ex.Message, "Save audio content", true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private async Task ExportSelected()
        {
            var items = SelectedItems.ToList();
            string outputLocation = await Dialogs.PickFolder("Export tonie files to");
            if (outputLocation == null)
            {
                return;
            }

            foreach (var item in items)
            {
                try
                {
                    CardContent.Export(item.Entry, outputLocation);
                }
                catch (CardOperationException ex)
                {
                    await Dialogs.ShowMessage(ex.Message, "Export", true);
                    return;
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanModifySingle))]
        private async Task ReassignSelected()
        {
            var item = SelectedItems[0];
            var fi = new FileInfo(item.Entry.FileName);
            string oldUid = TonieTools.ReverseUid(fi.Directory.Name + fi.Name);

            string uid = await Dialogs.AskUid(RfidReader, oldUid);
            if (uid == null)
            {
                return;
            }

            try
            {
                switch (new CardContent(CurrentDirectory).Reassign(item.Entry, uid))
                {
                    case ReassignResult.Unchanged:
                        return;
                    case ReassignResult.MovedOldDirectoryKept:
                        await Dialogs.ShowMessage("Success. Will not delete directory '" + fi.Directory.FullName + "' as it is not empty.", "Successfully re-assigned UID");
                        break;
                }
            }
            catch (CardOperationException ex)
            {
                await Dialogs.ShowMessage(ex.Message, ex.Title ?? "Re-assigning UID failed", true);
                return;
            }

            RefreshCardContent();
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private async Task ToggleLive()
        {
            bool unhideAll = SelectedItems.Count > 1;

            try
            {
                foreach (var item in SelectedItems.ToList())
                {
                    bool live = PlatformServices.LiveFlag.IsLive(item.Entry.FileName);
                    PlatformServices.LiveFlag.SetLive(item.Entry.FileName, !(unhideAll || live));
                }
            }
            catch (Exception ex)
            {
                await Dialogs.ShowMessage("Failed to change the live flag: " + ex.Message, "Toggle live flag", true);
            }

            /* the flag is part of the displayed name: identify again */
            foreach (var item in SelectedItems.ToList())
            {
                ResolveItem(item);
            }
        }

        [RelayCommand(CanExecute = nameof(HasSingleSelection))]
        private async Task Rename()
        {
            var item = SelectedItems[0];
            if (item.Entry.Hash == null)
            {
                await Dialogs.ShowMessage("This file was not analyzed yet, please try again in a moment.", "Rename");
                return;
            }

            string current = Services.Database.GetCustomName(item.Entry.Hash) ?? item.Entry.Info?.Title ?? item.Text;
            string name = await Dialogs.AskText("Rename", "Name for this tonie (stored in customTonies.json):", current);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            Services.Database.SetCustomName(item.Entry.Hash, name.Trim());
            ResolveItem(item);
        }

        [RelayCommand(CanExecute = nameof(HasSingleSelection))]
        private async Task Reveal()
        {
            try
            {
                PlatformServices.Shell.RevealFile(SelectedItems[0].Entry.FileName);
            }
            catch (Exception ex)
            {
                await Dialogs.ShowMessage("Could not open the file manager: " + ex.Message, "Show in file manager", true);
            }
        }

        [RelayCommand(CanExecute = nameof(CanPlayStop))]
        private void PlayStop()
        {
            PlayStopCore();
            PlayStopCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void DownloadJsonNow()
        {
            new SafeThread(() => LoadJson(true), "JSON Downloader").Start();
        }

        [RelayCommand]
        private Task About()
        {
            return Dialogs.ShowAbout(Services.Version);
        }
    }
}

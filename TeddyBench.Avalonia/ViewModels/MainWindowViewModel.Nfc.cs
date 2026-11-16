using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeddyBench.Avalonia.Services;
using TeddyBench.Core;

namespace TeddyBench.Avalonia.ViewModels
{
    public partial class MainWindowViewModel
    {
        internal RfidReaderBase RfidReader { get; private set; }

        private string LastFoundUid;
        private bool AutoSelected;
        private bool SelectingFromReader;
        private bool TagOperationRunning;

        [ObservableProperty]
        private bool nfcAutodetect;

        /// <summary>A reader is connected (enables the diagnostics reports).</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ReportAntennaCommand), nameof(ReportNfcTagCommand))]
        private bool readerConnected;

        /// <summary>Reader with the SLIX-L capable firmware: enables the advanced tag actions.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ReadTagContentCommand), nameof(MeasureAntennaCommand))]
        private bool advancedActionsEnabled;

        /// <summary>Proxmark3 with bootloader: firmware/bootloader can be flashed.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(FlashFirmwareCommand), nameof(FlashBootloaderCommand))]
        private bool flashEnabled;

        [ObservableProperty]
        private bool consoleMode;

        partial void OnNfcAutodetectChanged(bool value)
        {
            Settings.NfcEnabled = value;
            SaveSettings();
            UpdateNfcReader();
        }

        internal void UpdateNfcReader()
        {
            if (!Settings.NfcEnabled)
            {
                StopNfcReader();
                return;
            }

            if (RfidReader != null)
            {
                return;
            }

            RfidReader = Services.CreateReader(Settings.NfcType);
            if (RfidReader == null)
            {
                return;
            }
            RfidReader.UidFound += Reader_UidFound;
            RfidReader.DeviceFound += Reader_DeviceFound;
            RfidReader.FlashRequest += Reader_FlashRequest;
            RfidReader.FlashResult += Reader_FlashResult;

            if (Services.StartBackgroundWork)
            {
                RfidReader.Start();
            }
        }

        private void StopNfcReader()
        {
            var reader = RfidReader;
            if (reader == null)
            {
                return;
            }
            RfidReader = null;
            reader.UidFound -= Reader_UidFound;
            reader.DeviceFound -= Reader_DeviceFound;
            reader.FlashRequest -= Reader_FlashRequest;
            reader.FlashResult -= Reader_FlashResult;
            reader.Stop();
            ApplyReaderState(null, null);
        }

        private void Reader_DeviceFound(object sender, string port)
        {
            var reader = sender as RfidReaderBase;
            Ui.Post(() => ApplyReaderState(reader, port));
        }

        internal void ApplyReaderState(RfidReaderBase reader, string port)
        {
            bool connected = reader != null && port != null;
            ReaderConnected = connected;
            AdvancedActionsEnabled = connected && reader.UnlockSupported;
            FlashEnabled = connected && (reader.DeviceInfo & RfidReaderBase.eDeviceInfo.BootromPresent) != 0;
            UpdateStatusBar();
        }

        private void Reader_UidFound(object sender, string uid)
        {
            Ui.Post(() => SelectTagUid(uid));
        }

        /// <summary>Selects the tonie belonging to a tag on the reader; deselects again when the tag is removed.</summary>
        internal void SelectTagUid(string uid)
        {
            bool found = false;

            if (uid != null)
            {
                /* already handled this UID? */
                if (uid == LastFoundUid)
                {
                    return;
                }

                var item = Items.FirstOrDefault(i => i.Entry.Uid == uid);
                if (item != null)
                {
                    if (SelectedItems.Count == 1 && SelectedItems[0] == item)
                    {
                        LastFoundUid = uid;
                        return;
                    }

                    SelectingFromReader = true;
                    SelectedItems.Clear();
                    SelectedItems.Add(item);
                    SelectingFromReader = false;
                    RequestScrollIntoView(item);
                    found = true;
                    AutoSelected = true;
                }
            }

            LastFoundUid = uid;

            if (!found && AutoSelected)
            {
                SelectingFromReader = true;
                SelectedItems.Clear();
                SelectingFromReader = false;
                AutoSelected = false;
            }
        }

        /* raised on the reader thread, which waits for the answer */
        private void Reader_FlashRequest(object sender, Proxmark3.FlashRequestContext e)
        {
            string nl = Environment.NewLine;
            string text = e.Bootloader
                ? "=== FLASHING BOOTLOADER ===" + nl + nl +
                  "Please be aware that this procedure could fail and your device" + nl +
                  "gets bricked. To recover you need a special programmer." + nl + nl +
                  "Do you still want to flash the file " + e.FlashFile + "?"
                : "Please be aware that this procedure could fail and your device" + nl +
                  "main firmware is lost. (Device starts with LEDs A and C lit and" + nl +
                  "it doesn't enumerate on USB anymore)." + nl +
                  "No panic, the bootloader wasn't touched and you can reflash it." + nl + nl +
                  "If so, plug the device with the button pressed and *keep* it" + nl +
                  "pressed until it's flashed again." + nl + nl +
                  "If this still fails, please" + nl +
                  " a) check the firmware file or" + nl +
                  " b) use the official flasher tool" + nl + nl +
                  "Do you want to flash the file " + e.FlashFile + "?";

            e.Proceed = Ui.InvokeAsync(() => Dialogs.AskYesNo(text, "Flash Proxmark3?")).GetAwaiter().GetResult();
        }

        private void Reader_FlashResult(object sender, bool success)
        {
            string nl = Environment.NewLine;
            Ui.Post(() =>
            {
                if (success)
                {
                    _ = Dialogs.ShowMessage("Flashing the device succeeded, will reconnect now", "Flashing Proxmark3 done");
                }
                else
                {
                    _ = Dialogs.ShowMessage(
                        "Flashing the device FAILED, it will reconnect now." + nl + nl +
                        "If Device starts with LEDs A and C lit and it doesn't show" + nl +
                        "on USB, replug the device with the button pressed and *keep*" + nl +
                        "it pressed until it's flashed again." + nl + nl +
                        "If this still fails, please" + nl +
                        " a) check the firmware file or" + nl +
                        " b) use the official flasher tool", "Flashing Proxmark3 failed", true);
                }
            });
        }

        /// <summary>
        /// Runs a reader operation on a worker thread while showing a (cancellable) progress window.
        /// Returns default if cancelled. The reader call itself cannot be interrupted; its late result is dropped.
        /// </summary>
        private async Task<(bool completed, T result)> RunTagOperation<T>(Func<RfidReaderBase, T> operation, bool cancellable)
        {
            var reader = RfidReader;
            if (reader == null || TagOperationRunning)
            {
                return (false, default(T));
            }

            TagOperationRunning = true;
            IOperationHandle op = Dialogs.ShowOperation("Tag Operation...", cancellable);
            try
            {
                Task<T> work = Task.Run(() => operation(reader));
                var cancelled = new TaskCompletionSource<bool>();
                using (op.Cancelled.Register(() => cancelled.TrySetResult(true)))
                {
                    if (await Task.WhenAny(work, cancelled.Task) != work)
                    {
                        return (false, default(T));
                    }
                }
                return (true, await work);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.eLogLevel.Error, "Tag operation failed: " + ex.Message);
                return (false, default(T));
            }
            finally
            {
                op.Close();
                TagOperationRunning = false;
            }
        }

        [RelayCommand(CanExecute = nameof(AdvancedActionsEnabled))]
        private async Task ReadTagContent()
        {
            var (completed, data) = await RunTagOperation(r => r.ReadMemory(), true);
            if (!completed)
            {
                return;
            }
            if (data == null)
            {
                await Dialogs.ShowMessage("No tag found. Please make sure you position it correctly.", "Failed");
                return;
            }
            await Dialogs.ShowTagDump(true, BitConverter.ToString(data).Replace("-", ""));
        }

        /* disabled like in the WinForms version: the firmware's emulation is not fully SLIX-L compatible */
        private bool CanEmulateTag => false;

        [RelayCommand(CanExecute = nameof(CanEmulateTag))]
        private async Task EmulateTag()
        {
            string previous = "";
            while (true)
            {
                string hex = await Dialogs.ShowTagDump(false, previous);
                if (hex == null)
                {
                    return;
                }
                previous = hex;

                byte[] data;
                try
                {
                    data = Helpers.ConvertHexStringToByteArray(hex);
                }
                catch (Exception ex)
                {
                    await Dialogs.ShowMessage("Failed to parse '" + hex + "'." + Environment.NewLine + "The hex code must represent 40 byte in hexadecimal numbers:" + Environment.NewLine + "8 byte UUID and 32 byte data.", "Emulate tag", true);
                    continue;
                }

                await RunTagOperation(r => { r.EmulateTag(data); return true; }, true);
                return;
            }
        }

        [RelayCommand(CanExecute = nameof(AdvancedActionsEnabled))]
        private async Task MeasureAntenna()
        {
            var (completed, result) = await RunTagOperation(r => r.MeasureAntenna(), false);
            if (!completed || result == null)
            {
                await Dialogs.ShowMessage("Measurement failed.", "Failed");
                return;
            }
            await Dialogs.ShowAntennaPlot(result);
        }

        [RelayCommand(CanExecute = nameof(FlashEnabled))]
        private Task FlashFirmware()
        {
            return Flash("Please select the fullimage.elf you want to flash", "Firmware ELF files");
        }

        [RelayCommand(CanExecute = nameof(FlashEnabled))]
        private Task FlashBootloader()
        {
            return Flash("Please select the bootrom.elf you want to flash", "Bootloader ELF files");
        }

        private async Task Flash(string title, string filterName)
        {
            var reader = RfidReader;
            string[] files = await Dialogs.PickFiles(title, false, filterName, "*.elf");
            if (reader == null || files == null || files.Length == 0)
            {
                return;
            }

            try
            {
                /* the reader asks for confirmation (FlashRequest) and reports the result (FlashResult) */
                await Task.Run(() => reader.EnterBootloader(files[0]));
            }
            catch (Exception ex)
            {
                await Dialogs.ShowMessage("Flashing failed: " + ex.Message, "Flash Proxmark3", true);
            }
        }

        /* disabled like in the WinForms version (experts only, never enabled there) */
        private bool CanToggleConsole => false;

        [RelayCommand(CanExecute = nameof(CanToggleConsole))]
        private void ToggleConsole()
        {
            ConsoleMode = !ConsoleMode;
            if (ConsoleMode)
            {
                RfidReader?.EnterConsole();
            }
            else
            {
                RfidReader?.ExitConsole();
            }
        }
    }
}

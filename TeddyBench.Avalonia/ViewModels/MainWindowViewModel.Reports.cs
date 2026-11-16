using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TeddyBench.Avalonia.Services;
using TeddyBench.Core;

namespace TeddyBench.Avalonia.ViewModels
{
    public partial class MainWindowViewModel
    {
        private int ReportingException;

        /// <summary>Shows the report for review and sends it on confirmation. Returns true if it was sent successfully.</summary>
        internal async Task<bool> SendReport(string content, string filename)
        {
            ReportResult form = await Dialogs.ShowReport(content, Settings.Username);
            if (form == null)
            {
                return false;
            }

            Settings.Username = form.Username;
            SaveSettings();

            DiagStatus ret = await Services.SendDiagnostics(content, form.Username, form.Message, filename);
            string nl = Environment.NewLine;
            if (ret.Success)
            {
                await Dialogs.ShowMessage("Report Successfully sent." + nl + "Server response:" + nl + ret.Message, "Report sent");
                return true;
            }

            await Dialogs.ShowMessage("Error sending the report." + nl + "Server response:" + nl + ret.Message, "Report sent", true);
            return false;
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private Task ReportSelected()
        {
            var entries = SelectedItems.Select(i => i.Entry).ToList();
            return SendReport(DiagnosticReports.AudioFiles(entries, Services.Database), "Audio Report.txt");
        }

        [RelayCommand(CanExecute = nameof(ReaderConnected))]
        private async Task ReportNfcTag()
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
            await SendReport(DiagnosticReports.NfcTag(data), "Tag Dump.txt");
        }

        [RelayCommand(CanExecute = nameof(ReaderConnected))]
        private async Task ReportAntenna()
        {
            var (completed, result) = await RunTagOperation(r => r.MeasureAntenna(), false);
            if (!completed || result == null)
            {
                await Dialogs.ShowMessage("Measurement failed.", "Failed");
                return;
            }
            await SendReport(DiagnosticReports.Antenna(result), "Antenna Report.txt");
        }

        /// <summary>Offers to report an unhandled exception (from any thread). Only one report dialog at a time.</summary>
        public void ReportException(string name, Exception e)
        {
            Logger.Log(Logger.eLogLevel.Error, "Exception in '" + name + "': " + e);

            Ui.Post(async () =>
            {
                if (Interlocked.Exchange(ref ReportingException, 1) != 0)
                {
                    return;
                }
                try
                {
                    if (await Dialogs.AskYesNo("Sorry, there was an exception in '" + name + "'. Do you want to report that crash?", "Some Exception happened"))
                    {
                        await SendReport(DiagnosticReports.Exception(name, e), "Exception.txt");
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref ReportingException, 0);
                }
            });
        }

        /// <summary>
        /// Checks GitHub for a newer release. On Windows the release zip is downloaded next to the
        /// executable (as before); elsewhere the release page is opened.
        /// </summary>
        internal async Task UpdateCheck(int delayMs = 2000)
        {
            try
            {
                string thisVersion = Services.BaseTag;
                if (string.IsNullOrEmpty(thisVersion))
                {
                    return;
                }
                await Task.Delay(delayMs).ConfigureAwait(false);

                bool windows = OperatingSystem.IsWindows();
                UpdateInfo update = await Services.UpdateChecker.CheckAsync(thisVersion, windows ? "TeddyBench.zip" : null).ConfigureAwait(false);
                if (update == null)
                {
                    return;
                }

                bool install = windows && update.AssetUrl != null;
                bool accepted = await Ui.InvokeAsync(() => Dialogs.AskUpdate(update.LatestVersion + " (yours: " + thisVersion + ")", update.ReleaseName, install)).ConfigureAwait(false);
                if (!accepted)
                {
                    return;
                }

                if (install)
                {
                    string zipName = Path.Combine(AppContext.BaseDirectory, update.AssetName);
                    await UpdateChecker.DownloadAsync(update.AssetUrl, zipName).ConfigureAwait(false);
                    PlatformServices.Shell.OpenFolder(zipName);
                }
                else if (update.ReleaseUrl != null)
                {
                    PlatformServices.Shell.OpenUrl(update.ReleaseUrl);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.eLogLevel.Debug, "Update check failed: " + ex.Message);
            }
        }
    }
}

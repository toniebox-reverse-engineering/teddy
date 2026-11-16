using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeddyBench.Core;
using TonieFile;
using Xunit;

namespace TeddyBench.Avalonia.Tests
{
    /* both classes change the static AppPaths: never run them in parallel */
    [Collection("AppPaths")]
    public class MainWindowViewModelTests
    {
        private const string Uid1 = "E00403500102AB12";
        private const string Uid2 = "E00403500102CD34";
        private const uint Custom = 0x01234567;
        private const uint Official = 0x5E6F7A8B;

        [Fact]
        public void OpenCard_ListsAndIdentifiesTonies()
        {
            using (var h = new Harness())
            {
                h.AddTonie(Uid1, Custom);
                h.AddTonie(Uid2, Official);

                Assert.False(h.Vm.IsCardOpen);
                h.OpenCard();

                Assert.True(h.Vm.IsCardOpen);
                Assert.Equal(2, h.Vm.Items.Count);
                Assert.EndsWith(h.Card, h.Vm.Title);
                var custom = h.Vm.Items.Single(i => i.Entry.Uid == Uid1);
                var unknown = h.Vm.Items.Single(i => i.Entry.Uid == Uid2);
                Assert.Equal(TonieResolver.ImageCustom, custom.ImageKey);
                Assert.StartsWith("Unnamed Teddy", custom.Text);
                Assert.True(unknown.IsUnknown);
                Assert.Contains("0x5E6F7A8B", unknown.ToolTip);
            }
        }

        [Fact]
        public void KnownTonie_UsesDatabaseNameAndSortsByType()
        {
            byte[] official = Fixtures.TonieFile(Official);
            string hash = BitConverter.ToString(TonieAudioHash(official)).Replace("-", "");
            string json = "[{\"model\":\"10000123\",\"audio_id\":[],\"hash\":[\"" + hash + "\"],\"title\":\"Sine\",\"no\":\"5\",\"language\":\"de-de\"}]";

            using (var h = new Harness(toniesJson: json))
            {
                h.AddTonie(Uid1, Official);
                h.AddTonie(Uid2, Custom);
                File.WriteAllBytes(h.Dir.Combine("CONTENT", "unknown.bin"), new byte[] { 1 });
                h.OpenCard();

                var known = h.Vm.Items.Single(i => i.Entry.Uid == Uid1);
                Assert.Equal("10000123 - Sine", known.Text);
                Assert.Equal("Retail", known.GroupName);

                h.Vm.SortMode = 3;
                Assert.True(h.Vm.IsGroupedByType);
                Assert.Equal(new[] { "Custom", "Retail" }, h.Vm.Items.Select(i => i.GroupName).ToArray());

                h.Vm.SortMode = 0;
                Assert.Equal(new[] { Uid1, Uid2 }, h.Vm.Items.Select(i => i.Entry.Uid).ToArray());
            }
        }

        private static byte[] TonieAudioHash(byte[] tonieFile)
        {
            string tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tmp, tonieFile);
                return TonieAudio.FromFile(tmp, false).Header.Hash;
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Fact]
        public async Task AddSingleMp3_AsksUidAndConfirmation_EncodesOntoCard()
        {
            using (var h = new Harness())
            {
                h.OpenCard();
                h.Dialogs.Uids.Enqueue(Uid1);
                h.Dialogs.YesNo.Enqueue(true);

                await h.Vm.AddFilesAsync(new[] { Fixtures.Mp3 });

                Assert.Equal(new[] { "uid:", "yesno:Encode a file" }, h.Dialogs.Calls);
                string written = new CardContent(h.Card).GetTonieFilePath(Uid1);
                Assert.True(File.Exists(written));
                Assert.True(TonieAudio.FromFile(written).HashCorrect);
                Assert.Single(h.Vm.Items);
                Assert.Contains("Track   1", h.Vm.EncodeLog);
                Assert.False(h.Vm.IsBusy);
                Assert.False(h.Vm.ShowEncodeLog);
            }
        }

        [Fact]
        public async Task AddSeveralFiles_SortsThenEncodesInChosenOrder()
        {
            using (var h = new Harness())
            {
                h.OpenCard();
                h.Dialogs.Uids.Enqueue(Uid1);
                h.Dialogs.Sort = files => files.Reverse().ToArray();

                await h.Vm.AddFilesAsync(new[] { Fixtures.Mp3, Fixtures.Ogg });

                Assert.Equal(new[] { "uid:", "sort" }, h.Dialogs.Calls);
                var audio = TonieAudio.FromFile(new CardContent(h.Card).GetTonieFilePath(Uid1));
                Assert.Equal(2, audio.Header.AudioChapters.Length);
            }
        }

        [Fact]
        public async Task AddFiles_RejectsMixedAndUnsupportedFiles()
        {
            using (var h = new Harness())
            {
                h.OpenCard();
                string notes = Path.Combine(Fixtures.Album, "notes.txt");

                h.Dialogs.Uids.Enqueue(Uid1);
                await h.Vm.AddFilesAsync(new[] { Fixtures.Mp3, notes });
                Assert.Equal("Please select MP3/Ogg files only.", h.Dialogs.Messages.Last());

                h.Dialogs.Uids.Enqueue(Uid1);
                await h.Vm.AddFilesAsync(new[] { notes });
                Assert.Equal("The file you have chosen is not supported.", h.Dialogs.Messages.Last());

                h.Dialogs.Uids.Enqueue(null);
                await h.Vm.AddFilesAsync(new[] { Fixtures.Mp3 });
                Assert.Empty(Directory.GetDirectories(h.Card));
            }
        }

        [Fact]
        public async Task AddExistingTonieFile_CopiesIt()
        {
            using (var h = new Harness())
            {
                h.OpenCard();
                string source = h.Dir.Combine("10000123 - 01234567 - Exported");
                File.WriteAllBytes(source, Fixtures.TonieFile(Custom));
                h.Dialogs.Uids.Enqueue(Uid2);

                await h.Vm.AddFilesAsync(new[] { source });

                Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(new CardContent(h.Card).GetTonieFilePath(Uid2)));
                Assert.Single(h.Vm.Items);
            }
        }

        [Fact]
        public async Task DropFiles_WithoutCard_AsksToOpenOne()
        {
            using (var h = new Harness())
            {
                await h.Vm.DropFiles(new[] { Fixtures.Mp3 });
                Assert.Equal(new[] { "message:Add file..." }, h.Dialogs.Calls);
                Assert.False(h.Vm.AddFilesCommand.CanExecute(null));
            }
        }

        [Fact]
        public async Task DeleteSelected_AllAfterOneConfirmation()
        {
            using (var h = new Harness())
            {
                h.AddTonie(Uid1, Custom);
                h.AddTonie(Uid2, Custom);
                h.OpenCard();
                Assert.False(h.Vm.DeleteSelectedCommand.CanExecute(null));

                foreach (var item in h.Vm.Items.ToList())
                {
                    h.Vm.SelectedItems.Add(item);
                }
                Assert.True(h.Vm.DeleteSelectedCommand.CanExecute(null));
                h.Dialogs.YesNoCancel.Enqueue(true);

                await h.Vm.DeleteSelectedCommand.ExecuteAsync(null);

                Assert.Equal(new[] { "yesnocancel:Delete files?" }, h.Dialogs.Calls);
                Assert.Empty(Directory.GetFileSystemEntries(h.Card));
                Assert.Empty(h.Vm.Items);
            }
        }

        [Fact]
        public async Task Reassign_MovesToNewUid_AndKeepsSelection()
        {
            using (var h = new Harness())
            {
                h.AddTonie(Uid1, Custom);
                h.OpenCard();
                h.Vm.SelectedItems.Add(h.Vm.Items[0]);
                h.Dialogs.Uids.Enqueue(Uid2);

                await h.Vm.ReassignSelectedCommand.ExecuteAsync(null);

                Assert.Equal("uid:" + Uid1, h.Dialogs.Calls.Single());
                Assert.True(File.Exists(new CardContent(h.Card).GetTonieFilePath(Uid2)));
                Assert.Equal(Uid2, h.Vm.Items.Single().Entry.Uid);
            }
        }

        [Fact]
        public async Task Rename_StoresCustomNameAndUpdatesText()
        {
            using (var h = new Harness())
            {
                h.AddTonie(Uid1, Custom);
                h.OpenCard();
                var item = h.Vm.Items[0];
                h.Vm.SelectedItems.Add(item);
                h.Dialogs.Texts.Enqueue("  Bedtime songs ");

                await h.Vm.RenameCommand.ExecuteAsync(null);

                Assert.Equal("text:Unnamed Teddy", h.Dialogs.Calls.Single());
                Assert.Equal("Bedtime songs", item.Text);
                Assert.Equal("Bedtime songs", h.Services.Database.GetCustomName(item.Entry.Hash));
                Assert.Contains("Bedtime songs", File.ReadAllText(h.Dir.Combine("data", "customTonies.json")));
            }
        }

        [Fact]
        public async Task ExportAndSaveAudio_WriteIntoChosenFolder()
        {
            using (var h = new Harness())
            {
                h.AddTonie(Uid1, Custom);
                h.OpenCard();
                h.Vm.SelectedItems.Add(h.Vm.Items[0]);
                Directory.CreateDirectory(h.Dir.Combine("export"));
                Directory.CreateDirectory(h.Dir.Combine("ogg"));

                h.Dialogs.Folders.Enqueue(h.Dir.Combine("export"));
                await h.Vm.ExportSelectedCommand.ExecuteAsync(null);
                Assert.Equal(new[] { " - 01234567 - Unnamed Teddy" }, Directory.GetFiles(h.Dir.Combine("export")).Select(Path.GetFileName).ToArray());

                h.Dialogs.YesNoCancel.Enqueue(false);
                h.Dialogs.Folders.Enqueue(h.Dir.Combine("ogg"));
                await h.Vm.SaveSelectedCommand.ExecuteAsync(null);
                Assert.Contains("message:Legal information", h.Dialogs.Calls);
                Assert.Single(Directory.GetFiles(h.Dir.Combine("ogg"), "*.ogg"));
            }
        }

        [Fact]
        public async Task ReportSelected_SendsReportAndRemembersName()
        {
            using (var h = new Harness())
            {
                h.AddTonie(Uid1, Custom);
                h.OpenCard();
                h.Vm.SelectedItems.Add(h.Vm.Items[0]);
                h.Dialogs.Report = new Services.ReportResult { Username = "tester", Message = "hello" };

                await h.Vm.ReportSelectedCommand.ExecuteAsync(null);

                var sent = h.SentReports.Single();
                Assert.Contains("Reporting 1 files", sent[0]);
                Assert.Equal(new[] { "tester", "hello", "Audio Report.txt" }, sent.Skip(1).ToArray());
                Assert.Equal("Report Successfully sent.", h.Dialogs.Messages.Last().Split(Environment.NewLine)[0]);
                Assert.Equal("tester", Settings.FromFile(h.Services.SettingsFile).Username);
            }
        }

        [Fact]
        public void ReportException_AsksBeforeSending()
        {
            using (var h = new Harness())
            {
                h.Dialogs.YesNo.Enqueue(false);
                h.Vm.ReportException("Worker", new InvalidOperationException("boom"));
                Assert.Empty(h.SentReports);

                h.Dialogs.YesNo.Enqueue(true);
                h.Dialogs.Report = new Services.ReportResult { Username = "", Message = "" };
                h.Vm.ReportException("Worker", new InvalidOperationException("boom"));
                Assert.Equal("Exception.txt", h.SentReports.Single()[3]);
                Assert.Contains("boom", h.SentReports.Single()[0]);
            }
        }

        [Fact]
        public async Task UpdateCheck_OffersReleasePageOutsideWindows()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }
            string release = "{\"tag_name\":\"v9\",\"name\":\"Big release\",\"html_url\":\"https://example.invalid/v9\",\"assets\":[{\"name\":\"TeddyBench.zip\",\"browser_download_url\":\"https://example.invalid/z\"}]}";
            var shell = new FakeShell();
            var previous = PlatformServices.Shell;
            PlatformServices.Shell = shell;
            try
            {
                using (var h = new Harness(github: url => Task.FromResult(release), baseTag: "v1"))
                {
                    h.Dialogs.YesNo.Enqueue(true);
                    await h.Vm.UpdateCheck(0);

                    Assert.Equal("update:v9 (yours: v1):False", h.Dialogs.Calls.Single());
                    Assert.Equal(new[] { "url:https://example.invalid/v9" }, shell.Opened);
                }
            }
            finally
            {
                PlatformServices.Shell = previous;
            }
        }

        [Fact]
        public void AppVersion_DefaultsToUpstreamUpdateRepository()
        {
            /* CI builds set -p:TeddyUpdateRepository; the test build uses the default */
            Assert.Equal(UpdateChecker.DefaultRepository, AppVersion.UpdateRepository);

            /* CreateDefault uses the real data folders: keep them out of the user's home */
            using (var dir = new TempDir())
            {
                AppPaths.Configure(dir.Combine("config"), dir.Combine("cache"));
                try
                {
                    Assert.Equal("someone/fork", ViewModels.MainWindowServices.CreateDefault("1", "", "someone/fork").UpdateChecker.Repository);
                }
                finally
                {
                    AppPaths.Configure(null, null);
                }
            }
        }

        [Fact]
        public void Options_ArePersisted()
        {
            using (var h = new Harness())
            {
                var previous = Logger.LogLevel;
                try
                {
                    h.Vm.DownloadJsonOnStartup = false;
                    h.Vm.DebugWindow = true;

                    var saved = Settings.FromFile(h.Services.SettingsFile);
                    Assert.False(saved.DownloadJson);
                    Assert.True(saved.DebugWindow);
                    Assert.True(h.Dialogs.LogVisible);
                    Assert.Equal(Logger.eLogLevel.DebugVerbose, Logger.LogLevel);
                }
                finally
                {
                    Logger.LogLevel = previous;
                }
            }
        }

        [Fact]
        public void StatusBar_ShowsDatabaseState()
        {
            using (var h = new Harness())
            {
                h.Vm.UpdateStatusBar();
                Assert.Contains("No tonies.json loaded", h.Vm.StatusText);
            }
        }

        [Fact]
        public void StatusBar_ExplainsMissingSerialPermission()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }
            var list = SerialPorts.ListPorts;
            var probe = SerialPorts.Probe;
            try
            {
                SerialPorts.ListPorts = () => new[] { "/dev/ttyACM0" };
                SerialPorts.Probe = port => throw new UnauthorizedAccessException();
                SerialPorts.GetAvailablePorts();

                var reader = new FakeReader();
                using (var h = new Harness(reader: type => reader))
                {
                    h.Vm.UpdateStatusBar();
                    Assert.DoesNotContain("dialout", h.Vm.StatusText);   /* reader detection is off */

                    h.Vm.NfcAutodetect = true;
                    h.Vm.UpdateStatusBar();
                    Assert.Contains("No permission to open /dev/ttyACM0 - add your user to the 'dialout' group", h.Vm.StatusText);
                }
            }
            finally
            {
                /* reset the remembered state without touching real serial devices */
                SerialPorts.ListPorts = () => new string[0];
                SerialPorts.GetAvailablePorts();
                SerialPorts.ListPorts = list;
                SerialPorts.Probe = probe;
            }
        }

        [Fact]
        public void Reader_SelectsTagsTonieAndDeselectsWhenRemoved()
        {
            var reader = new FakeReader();
            using (var h = new Harness(reader: type => reader))
            {
                h.AddTonie(Uid1, Custom);
                h.AddTonie(Uid2, Custom);
                h.OpenCard();
                h.Vm.NfcAutodetect = true;
                Assert.Same(reader, h.Vm.RfidReader);

                reader.RaiseDevice("/dev/ttyACM0");
                Assert.True(h.Vm.ReaderConnected);
                Assert.True(h.Vm.AdvancedActionsEnabled);
                Assert.True(h.Vm.FlashEnabled);
                Assert.Contains("FakeReader (FW: SLIX-L enabled) found at /dev/ttyACM0", h.Vm.StatusText);

                reader.RaiseUid(Uid2);
                Assert.Equal(Uid2, h.Vm.SelectedItems.Single().Entry.Uid);

                reader.RaiseUid(null);
                Assert.Empty(h.Vm.SelectedItems);

                /* a manual selection is not undone when the tag goes away */
                reader.RaiseUid(Uid1);
                h.Vm.SelectedItems.Add(h.Vm.Items.Single(i => i.Entry.Uid == Uid2));
                reader.RaiseUid(null);
                Assert.Equal(2, h.Vm.SelectedItems.Count);

                h.Vm.NfcAutodetect = false;
                Assert.Null(h.Vm.RfidReader);
                Assert.False(h.Vm.ReaderConnected);
                Assert.False(Settings.FromFile(h.Services.SettingsFile).NfcEnabled);
            }
        }

        [Fact]
        public void Reader_FlashRequestFromReaderThreadWaitsForAnswer()
        {
            var reader = new FakeReader();
            using (var h = new Harness(reader: type => reader))
            {
                h.Vm.NfcAutodetect = true;
                h.Dialogs.YesNo.Enqueue(true);
                var ctx = new Proxmark3.FlashRequestContext { Bootloader = true, FlashFile = "bootrom.elf" };

                var thread = new Thread(() => reader.RaiseFlashRequest(ctx));
                thread.Start();
                Assert.True(thread.Join(5000));

                Assert.True(ctx.Proceed);
                Assert.Equal("yesno:Flash Proxmark3?", h.Dialogs.Calls.Single());
            }
        }

        [Fact]
        public async Task ReadTagContent_ShowsDumpAndClosesProgress()
        {
            var reader = new FakeReader { Memory = new byte[] { 0xE0, 0x04, 0x03, 0x50, 0xAA } };
            using (var h = new Harness(reader: type => reader))
            {
                h.Vm.NfcAutodetect = true;
                reader.RaiseDevice("/dev/ttyACM0");

                await h.Vm.ReadTagContentCommand.ExecuteAsync(null);

                Assert.Equal(new[] { "operation:True", "tagdump:E0040350AA" }, h.Dialogs.Calls);
                Assert.True(h.Dialogs.LastOperation.Closed);
            }
        }

        [Fact]
        public async Task ReadTagContent_CancelReturnsWithoutWaitingForReader()
        {
            var gate = new ManualResetEventSlim();
            var reader = new FakeReader { Memory = new byte[] { 1 }, ReadGate = gate };
            using (var h = new Harness(reader: type => reader))
            {
                h.Vm.NfcAutodetect = true;
                reader.RaiseDevice("/dev/ttyACM0");

                Task run = h.Vm.ReadTagContentCommand.ExecuteAsync(null);
                h.Dialogs.LastOperation.Cts.Cancel();
                await run.WaitAsync(TimeSpan.FromSeconds(3));
                gate.Set();

                Assert.Equal(new[] { "operation:True" }, h.Dialogs.Calls);
                Assert.True(h.Dialogs.LastOperation.Closed);
            }
        }

        [Fact]
        public void CardLost_ClosesDirectory()
        {
            using (var h = new Harness())
            {
                h.AddTonie(Uid1, Custom);
                h.OpenCard();
                Directory.Delete(h.Card, true);

                h.Services.CardMonitor.Poll();

                Assert.False(h.Vm.IsCardOpen);
                Assert.Empty(h.Vm.Items);
            }
        }
    }
}

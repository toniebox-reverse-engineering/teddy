using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.IO;
using System.Linq;
using TeddyBench.Avalonia.Services;
using TeddyBench.Avalonia.Views;
using TeddyBench.Avalonia.Views.Dialogs;
using TeddyBench.Core;
using Xunit;

namespace TeddyBench.Avalonia.Tests
{
    /// <summary>The real windows, rendered headless with Skia.</summary>
    /* both classes change the static AppPaths: never run them in parallel */
    [Collection("AppPaths")]
    public class UiTests
    {
        private const string Uid1 = "E00403500102AB12";
        private const string Uid2 = "E00403500102CD34";

        /// <summary>Set TEDDYBENCH_SCREENSHOTS to a directory to keep PNGs of the rendered windows.</summary>
        private static void Screenshot(Window window, string name)
        {
            /* a freshly shown window may need a few render ticks before its first frame exists */
            global::Avalonia.Media.Imaging.WriteableBitmap frame = null;
            for (int attempt = 0; attempt < 5 && frame == null; attempt++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                frame = window.CaptureRenderedFrame();
            }
            Assert.NotNull(frame);

            string dir = Environment.GetEnvironmentVariable("TEDDYBENCH_SCREENSHOTS");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                frame.Save(Path.Combine(dir, name + ".png"));
            }
        }

        private static T Find<T>(Window window, string name) where T : Control
        {
            return window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
        }

        private static Button FindButton(Control root, string text)
        {
            return root.GetVisualDescendants().OfType<Button>().First(b => (b.Content as string) == text);
        }

        private static void Click(Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        [AvaloniaFact]
        public void MainWindow_WithoutCard_AsksForTheSdCard()
        {
            using (var h = new Harness(covers: new CoverImages()))
            {
                var window = new MainWindow { DataContext = h.Vm, Width = 900, Height = 560 };
                window.Show();
                h.Vm.Start();
                Dispatcher.UIThread.RunJobs();

                Assert.True(Find<StackPanel>(window, "NoCardPanel").IsVisible);
                Assert.False(Find<DockPanel>(window, "CardPanel").IsVisible);
                Assert.Contains("No tonies.json loaded", Find<TextBlock>(window, "StatusBar").Text);
                Screenshot(window, "main-no-card");
                window.Close();
            }
        }

        [AvaloniaFact]
        public void MainWindow_WithCard_ShowsTilesAndSyncsSelection()
        {
            using (var h = new Harness(covers: new CoverImages()))
            {
                h.AddTonie(Uid1, 0x01234567);
                h.AddTonie(Uid2, 0x5E6F7A8B);
                var window = new MainWindow { DataContext = h.Vm, Width = 900, Height = 560 };
                window.Show();
                h.OpenCard();
                Dispatcher.UIThread.RunJobs();

                Assert.False(Find<StackPanel>(window, "NoCardPanel").IsVisible);
                Assert.True(Find<DockPanel>(window, "CardPanel").IsVisible);

                var list = Find<ListBox>(window, "TonieList");
                Assert.Equal(2, list.ItemCount);
                var texts = list.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains(texts, t => t != null && t.StartsWith("Unnamed Teddy"));
                Assert.Contains(texts, t => t != null && t.Contains("(unknown: 5E6F7A8B)"));

                /* view -> view model */
                list.SelectedItems.Add(h.Vm.Items[0]);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(h.Vm.Items[0], h.Vm.SelectedItems.Single());
                Assert.True(h.Vm.RenameCommand.CanExecute(null));

                /* view model -> view (e.g. tag found on the reader) */
                h.Vm.SelectedItems.Clear();
                h.Vm.SelectedItems.Add(h.Vm.Items[1]);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(h.Vm.Items[1], list.SelectedItem);

                Screenshot(window, "main-card");

                h.Vm.SortMode = 3;
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("Custom", list.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text));
                Screenshot(window, "main-card-by-type");
                window.Close();
            }
        }

        [AvaloniaFact]
        public void MainWindow_EncodeLogReplacesCardView()
        {
            using (var h = new Harness())
            {
                var window = new MainWindow { DataContext = h.Vm, Width = 900, Height = 560 };
                window.Show();
                h.OpenCard();
                h.Vm.ShowEncodeLog = true;
                h.Vm.EncodeLog = " Track   1 - Sine 440         [....25%....50%";
                Dispatcher.UIThread.RunJobs();

                Assert.False(Find<DockPanel>(window, "CardPanel").IsVisible);
                Assert.True(Find<TextBox>(window, "EncodeLogBox").IsEffectivelyVisible);
                Screenshot(window, "main-encoding");
                window.Close();
            }
        }

        [AvaloniaFact]
        public void RealDialogService_ShowsOwnedDialogsAndReturnsAnswers()
        {
            using (var dir = new TempDir())
            {
                AppPaths.Configure(dir.Combine("config"), dir.Combine("cache"));
                try
                {
                    var window = new MainWindow();
                    var dialogs = new DialogService(window);
                    var vm = new ViewModels.MainWindowViewModel(dialogs, new AvaloniaUiDispatcher(), new CoverImages(), ViewModels.MainWindowServices.CreateDefault("1.2.3-test", ""));
                    window.DataContext = vm;
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal("TeddyBench - 1.2.3-test", window.Title);
                    Assert.Equal(dir.Combine("config", "teddyBench.cfg"), AppPaths.SettingsFile);

                    var answer = dialogs.AskYesNoCancel("Create directories for selected file(s)?", "Save audio content");
                    Dispatcher.UIThread.RunJobs();
                    var shown = window.OwnedWindows.OfType<MessageDialog>().Single();
                    Assert.Equal("Save audio content", shown.Title);
                    Click(FindButton(shown, "Cancel"));
                    Assert.True(answer.IsCompleted);
                    Assert.Null(answer.Result);

                    var uid = dialogs.AskUid(null, "e00403500102ab12");
                    Dispatcher.UIThread.RunJobs();
                    Click(FindButton(window.OwnedWindows.OfType<UidDialog>().Single(), "OK"));
                    Assert.Equal("E00403500102AB12", uid.Result);

                    var op = dialogs.ShowOperation("Tag Operation...", true);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Single(window.OwnedWindows.OfType<OperationDialog>());
                    op.Close();
                    Dispatcher.UIThread.RunJobs();
                    Assert.Empty(window.OwnedWindows);

                    vm.Shutdown();
                    Assert.True(File.Exists(AppPaths.SettingsFile));
                    window.Close();
                }
                finally
                {
                    AppPaths.Configure(null, null);
                }
            }
        }

        [AvaloniaFact]
        public void UidDialog_ValidatesAndTakesUidFromReader()
        {
            var reader = new FakeReader();
            var dialog = new UidDialog(reader, null);
            dialog.Show();
            Dispatcher.UIThread.RunJobs();

            var input = dialog.GetVisualDescendants().OfType<TextBox>().Single();
            var ok = FindButton(dialog, "OK");
            Assert.Equal("E00403", input.Text);
            Assert.False(ok.IsEnabled);

            input.Text = "E00403500102AB1";
            Dispatcher.UIThread.RunJobs();
            Assert.False(ok.IsEnabled);
            input.Text = "X00403500102AB12";
            Dispatcher.UIThread.RunJobs();
            Assert.False(ok.IsEnabled);

            reader.RaiseUid(Uid1);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Uid1, input.Text);
            Assert.True(ok.IsEnabled);
            Screenshot(dialog, "dialog-uid");

            Click(ok);
            Assert.Equal(Uid1, dialog.Result);
        }

        [AvaloniaFact]
        public void TrackSortDialog_MovesTracks()
        {
            string[] files = { Fixtures.Mp3, Fixtures.Ogg, Path.Combine(Fixtures.Album, "02 MONO22K.MP3") };
            var dialog = new TrackSortDialog(files);
            dialog.Show();
            Dispatcher.UIThread.RunJobs();

            var list = dialog.GetVisualDescendants().OfType<ListBox>().Single();
            /* ID3 track numbers: stereo44k=1, MONO22K=2, the Ogg has none and goes last */
            var initial = list.Items.Cast<TrackSortDialog.Track>().Select(t => Path.GetFileName(t.File)).ToArray();
            Assert.Equal(new[] { "01 stereo44k.mp3", "02 MONO22K.MP3", "03 opus48k.ogg" }, initial);
            Screenshot(dialog, "dialog-tracksort");

            list.SelectedItems.Add(list.Items.Cast<TrackSortDialog.Track>().Last());
            Click(FindButton(dialog, "Up"));
            Click(FindButton(dialog, "OK"));

            Assert.Equal(new[] { "01 stereo44k.mp3", "03 opus48k.ogg", "02 MONO22K.MP3" }, dialog.Result.Select(Path.GetFileName).ToArray());
        }

        [AvaloniaFact]
        public void MessageAndInputDialogs_ReturnTheChoice()
        {
            var yesNo = new MessageDialog("Delete all selected files?", "Delete files?", MessageButtons.YesNoCancel);
            yesNo.Show();
            Screenshot(yesNo, "dialog-message");
            Click(FindButton(yesNo, "No"));
            Assert.Equal("no", yesNo.Result);

            var error = new MessageDialog("Failed to write file '/media/x/CONTENT/12AB0201/500304E0'", "Error", MessageButtons.Ok, error: true);
            error.Show();
            Screenshot(error, "dialog-error");
            Click(FindButton(error, "OK"));
            Assert.Equal("ok", error.Result);

            var hex = new InputDialog("Enter value", "Please enter the audio ID you want (8 hex digits)", "1234567", s => s.Length == 8 ? null : "8 digits");
            hex.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.False(FindButton(hex, "OK").IsEnabled);
            hex.GetVisualDescendants().OfType<TextBox>().Single().Text = "12345678";
            Dispatcher.UIThread.RunJobs();
            Click(FindButton(hex, "OK"));
            Assert.Equal("12345678", hex.Result);
        }

        [AvaloniaFact]
        public void OtherDialogs_Render()
        {
            var report = new ReportDialog(" Reporting 1 files\n-----------------------------------\n...", "tester");
            report.Show();
            Screenshot(report, "dialog-report");
            Click(FindButton(report, "Send"));
            Assert.Equal("tester", report.Result.Username);

            var dump = new TagDumpDialog(true, "E00403500102AB12DEADBEEF");
            dump.Show();
            Assert.Throws<InvalidOperationException>(() => FindButton(dump, "Cancel"));
            Screenshot(dump, "dialog-tagdump");
            dump.Close();

            var update = new UpdateDialog("v2.0 (yours: v1.0)", "Big release", canInstall: false);
            update.Show();
            Screenshot(update, "dialog-update");
            update.Close();

            var about = new AboutDialog("9.9.9");
            about.Show();
            Screenshot(about, "dialog-about");
            about.Close();

            IOperationHandle op = new OperationDialog("Tag Operation...", cancellable: true);
            ((Window)op).Show();
            Screenshot((Window)op, "dialog-operation");
            Click(FindButton((Window)op, "Cancel"));
            Assert.True(op.Cancelled.IsCancellationRequested);

            var measurement = new Proxmark3.MeasurementResult { vHF = 27.5f, vLF125 = 31.2f, vLF134 = 28.9f, peakF = 94, peakV = 33.0f };
            for (int i = 0; i < measurement.amplitudes.Length; i++)
            {
                measurement.amplitudes[i] = (byte)(60 * Math.Exp(-Math.Pow((i - 94) / 18.0, 2)));
            }
            var plot = new AntennaPlotWindow(measurement);
            plot.Show();
            Screenshot(plot, "dialog-antenna");
            plot.Close();
            Assert.Equal("good", AntennaPlotWindow.RateHf(27.5).rating);
            Assert.Equal("critical", AntennaPlotWindow.RateHf(46).rating);

            Logger.Log(Logger.eLogLevel.Error, "[UiTests] log window line");
            var log = new LogWindow();
            log.Show();
            Screenshot(log, "dialog-log");
            log.Close();
            Assert.True(log.IsVisible == false);
        }
    }
}

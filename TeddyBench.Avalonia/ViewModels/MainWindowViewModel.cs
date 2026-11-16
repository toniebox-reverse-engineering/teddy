using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeddyBench.Avalonia.Services;
using TeddyBench.Core;
using TonieFile;

namespace TeddyBench.Avalonia.ViewModels
{
    /// <summary>Core services the main window works with; replaceable for tests.</summary>
    public class MainWindowServices
    {
        public TonieDatabase Database;
        public TonieFileCache HeaderCache = new TonieFileCache();
        public ImageCache Images;
        public CardMonitor CardMonitor;
        public TonieAudioPlayer Player = new TonieAudioPlayer();
        public string SettingsFile;
        /// <summary>NFC reader for the configured type ("Proxmark3" or "Pn5180Esp").</summary>
        public Func<string, RfidReaderBase> CreateReader = type => type == "Pn5180Esp" ? (RfidReaderBase)new Pn5180Esp() : new Proxmark3();
        public UpdateChecker UpdateChecker = new UpdateChecker();
        /// <summary>(payload, sender, message, filename) -> server answer.</summary>
        public Func<string, string, string, string, Task<DiagStatus>> SendDiagnostics;
        /// <summary>Whether audio previews are possible on this platform.</summary>
        public Func<bool> CanPlayAudio = () => PlatformServices.AudioOutputFactory != null;
        /// <summary>Version shown to the user and sent with reports.</summary>
        public string Version = "";
        /// <summary>Git tag of this build, used for the update check and the tonies.json request.</summary>
        public string BaseTag = "";
        /// <summary>false in tests: no threads, timers, downloads or readers are started.</summary>
        public bool StartBackgroundWork = true;

        public static MainWindowServices CreateDefault(string version, string baseTag)
        {
            return new MainWindowServices
            {
                Database = new TonieDatabase(),
                Images = new ImageCache(),
                CardMonitor = new CardMonitor(),
                SettingsFile = AppPaths.SettingsFile,
                SendDiagnostics = (payload, sender, message, filename) => new DiagnosticsClient(version).SendAsync(payload, sender, message, filename),
                Version = version,
                BaseTag = baseTag
            };
        }
    }

    /// <summary>
    /// The TeddyBench main window: card content, file operations, NFC reader, playback.
    /// Port of the WinForms TeddyMain on top of TeddyBench.Core.
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject
    {
        internal readonly IDialogService Dialogs;
        internal readonly IUiDispatcher Ui;
        internal readonly ICoverImages Covers;
        internal readonly MainWindowServices Services;
        internal readonly Settings Settings;

        private readonly Dictionary<string, IImage> CoverCache = new Dictionary<string, IImage>();
        private FileSystemWatcher DirectoryWatcher;
        private bool RefreshPending;
        private bool AutoOpenDrive = true;
        private Timer StatusTimer;

        private SafeThread AnalyzeThread;
        private volatile bool AnalyzeStop;
        /* snapshot of the list for the analysis thread, replaced on every refresh */
        private volatile TonieItemViewModel[] AnalyzeItems = new TonieItemViewModel[0];

        private bool UpdatingPlayPosition;

        public ObservableCollection<TonieItemViewModel> Items { get; } = new ObservableCollection<TonieItemViewModel>();
        public ObservableCollection<TonieItemViewModel> SelectedItems { get; } = new ObservableCollection<TonieItemViewModel>();

        public string[] SortModes { get; } = { "UID", "Product Number", "Date", "Type" };

        /// <summary>Asks the view to bring an item into view (tag found by the reader).</summary>
        public event Action<TonieItemViewModel> ScrollIntoViewRequested;
        /// <summary>Asks the view to close the main window.</summary>
        public event Action CloseRequested;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCardOpen))]
        private string currentDirectory;

        public bool IsCardOpen => CurrentDirectory != null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGroupedByType))]
        private int sortMode = 2;

        /// <summary>Sorting by type shows the group (Unknown/Custom/Retail) on each tile.</summary>
        public bool IsGroupedByType => SortMode == 3;

        [ObservableProperty]
        private string statusText = "";

        /// <summary>A file is being encoded/copied: the encode log replaces the card view.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIdle))]
        private bool isBusy;

        public bool IsIdle => !IsBusy;

        [ObservableProperty]
        private string encodeLog = "";

        [ObservableProperty]
        private string playButtonText = "Play file";

        [ObservableProperty]
        private double playPosition;

        [ObservableProperty]
        private double playLength = 1;

        [ObservableProperty]
        private string playTimeText = "--:-- / --:--";

        public bool CanPlay => Services.CanPlayAudio();

        [ObservableProperty]
        private bool downloadJsonOnStartup;

        [ObservableProperty]
        private bool debugWindow;

        public MainWindowViewModel(IDialogService dialogs, IUiDispatcher ui, ICoverImages covers, MainWindowServices services)
        {
            Dialogs = dialogs;
            Ui = ui;
            Covers = covers;
            Services = services;
            Settings = Settings.FromFile(services.SettingsFile);

            /* initial values must not trigger the change handlers (they save settings, start readers) */
#pragma warning disable MVVMTK0034
            title = TitleString;
            downloadJsonOnStartup = Settings.DownloadJson;
            debugWindow = Settings.DebugWindow;
            nfcAutodetect = Settings.NfcEnabled;
#pragma warning restore MVVMTK0034

            if (Settings.DebugWindow)
            {
                Logger.LogLevel = Logger.eLogLevel.DebugVerbose;
            }

            Services.Database.Error += ReportException;
            Services.CardMonitor.CardFound += NotifyDrive;
            Services.CardMonitor.CurrentDirectoryLost += NotifyDriveLost;
            Services.Player.Progress += Player_Progress;
            Services.Player.Stopped += Player_Stopped;
            SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;
        }

        private string TitleString => "TeddyBench - " + Services.Version;

        /// <summary>Starts card detection, the tonies.json download, the update check and the NFC reader.</summary>
        public void Start()
        {
            UpdateStatusBar();

            if (!Services.StartBackgroundWork)
            {
                return;
            }

            Services.CardMonitor.Start();
            new SafeThread(() => LoadJson(), "JSON Downloader").Start();
            new SafeThread(() => UpdateCheck().GetAwaiter().GetResult(), "UpdateCheckThread").Start();
            StatusTimer = new Timer(_ => Ui.Post(UpdateStatusBar), null, 250, 250);
            UpdateNfcReader();
            if (Settings.DebugWindow)
            {
                Dialogs.SetLogWindowVisible(true);
            }
        }

        /// <summary>Stops all background work and saves the settings.</summary>
        public void Shutdown()
        {
            StatusTimer?.Dispose();
            StatusTimer = null;
            Services.CardMonitor.Stop();
            Services.Player.Shutdown(1000);
            StopAnalyzeThread();
            DirectoryWatcher?.Dispose();
            DirectoryWatcher = null;
            StopNfcReader();
            SaveSettings();
        }

        internal void SaveSettings()
        {
            Settings.Save(Services.SettingsFile);
        }

        internal void LoadJson(bool force = false)
        {
            Services.Database.Load(Settings.DownloadJson || force, TonieDatabase.DefaultUrl + Services.BaseTag);
        }

        partial void OnDownloadJsonOnStartupChanged(bool value)
        {
            Settings.DownloadJson = value;
            SaveSettings();
        }

        partial void OnDebugWindowChanged(bool value)
        {
            Settings.DebugWindow = value;
            SaveSettings();
            Logger.LogLevel = value ? Logger.eLogLevel.DebugVerbose : Logger.eLogLevel.Warning;
            Dialogs.SetLogWindowVisible(value);
        }

        #region Card / directory

        private void NotifyDrive(string contentDirectory)
        {
            Ui.Post(() =>
            {
                if (AutoOpenDrive)
                {
                    try
                    {
                        OpenPath(contentDirectory);
                        AutoOpenDrive = false;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(Logger.eLogLevel.Error, "Wanted to open " + contentDirectory + " but that caused a: " + ex.Message);
                    }
                }
            });
        }

        private void NotifyDriveLost()
        {
            Ui.Post(() =>
            {
                CloseDirectory();
                AutoOpenDrive = true;
            });
        }

        /// <summary>Shows the content of a Toniebox CONTENT directory (or a folder with exported tonies).</summary>
        public void OpenPath(string rootDirectory)
        {
            if (!Directory.Exists(rootDirectory))
            {
                CloseDirectory();
                return;
            }

            CurrentDirectory = rootDirectory;
            Services.CardMonitor.CurrentDirectory = rootDirectory;

            DirectoryWatcher?.Dispose();
            DirectoryWatcher = null;
            /* the watcher raises events on pool threads; tests (no background work) refresh explicitly */
            if (Services.StartBackgroundWork)
            {
                DirectoryWatcher = new FileSystemWatcher(rootDirectory);
                DirectoryWatcher.Changed += OnDirectoryChanged;
                DirectoryWatcher.Created += OnDirectoryChanged;
                DirectoryWatcher.Deleted += OnDirectoryChanged;
                DirectoryWatcher.Renamed += OnDirectoryChanged;
                DirectoryWatcher.EnableRaisingEvents = true;
            }

            RefreshCardContent();
        }

        private void CloseDirectory()
        {
            StopAnalyzeThread();
            DirectoryWatcher?.Dispose();
            DirectoryWatcher = null;
            CurrentDirectory = null;
            Services.CardMonitor.CurrentDirectory = null;
            Items.Clear();
            Title = TitleString;
        }

        private void OnDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            /* file operations raise bursts of events: refresh once */
            Ui.Post(() =>
            {
                if (RefreshPending)
                {
                    return;
                }
                RefreshPending = true;
                Ui.Post(() =>
                {
                    RefreshPending = false;
                    RefreshCardContent();
                });
            });
        }

        /// <summary>Rescans the directory, keeps the selection, and restarts the analysis.</summary>
        public void RefreshCardContent()
        {
            if (CurrentDirectory == null)
            {
                return;
            }

            StopAnalyzeThread();
            Title = TitleString + " - " + CurrentDirectory;

            var selected = new HashSet<string>(SelectedItems.Select(i => i.Entry.FileName));
            var known = Items.ToDictionary(i => i.Entry.FileName);
            List<CardEntry> entries;

            try
            {
                entries = new CardContent(CurrentDirectory).Scan();
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.eLogLevel.Error, "Exception while scanning directory '" + CurrentDirectory + "':" + ex.Message);
                entries = new List<CardEntry>();
            }

            Items.Clear();
            foreach (var entry in entries)
            {
                /* keep already identified items unless the file changed */
                if (known.TryGetValue(entry.FileName, out var old) && old.Entry.FileInfo.LastWriteTimeUtc == entry.FileInfo.LastWriteTimeUtc && !old.IsUnknown)
                {
                    Items.Add(old);
                }
                else
                {
                    Items.Add(new TonieItemViewModel(entry, Covers.Unknown));
                }
            }
            ApplySorting();

            foreach (var item in Items.Where(i => selected.Contains(i.Entry.FileName)))
            {
                SelectedItems.Add(item);
            }

            StartAnalyzeThread();
        }

        #endregion

        #region Analysis

        private void StartAnalyzeThread()
        {
            AnalyzeItems = Items.ToArray();

            if (!Services.StartBackgroundWork)
            {
                return;
            }

            AnalyzeStop = false;
            AnalyzeThread = new SafeThread(AnalyzeMain, "AnalyzeThread");
            AnalyzeThread.Start();
        }

        private void StopAnalyzeThread()
        {
            if (AnalyzeThread != null)
            {
                AnalyzeStop = true;
                if (!AnalyzeThread.Join(2000))
                {
                    Logger.Log(Logger.eLogLevel.Error, "Failed to stop Analyze Thread");
                    AnalyzeThread.Abort();
                }
                AnalyzeThread = null;
            }
        }

        private void AnalyzeMain()
        {
            while (!AnalyzeStop)
            {
                AnalyzeOnce();
                Thread.Sleep(250);
            }
        }

        /// <summary>
        /// Identifies all still unknown items (the database may arrive later, so unknown ones are retried).
        /// Runs on the analysis thread; public for tests.
        /// </summary>
        public void AnalyzeOnce()
        {
            foreach (var item in AnalyzeItems)
            {
                if (AnalyzeStop)
                {
                    return;
                }
                if (item.IsUnknown)
                {
                    ResolveItem(item);
                }
            }
        }

        /// <summary>Identifies one item and applies the result on the UI thread.</summary>
        internal void ResolveItem(TonieItemViewModel item)
        {
            try
            {
                TonieAudio header = Services.HeaderCache.GetHeader(item.Entry.FileName);
                bool live = false;
                try
                {
                    live = PlatformServices.LiveFlag.IsLive(item.Entry.FileName);
                }
                catch (Exception ex)
                {
                }

                TonieResolution res = TonieResolver.Resolve(item.Entry, header, Services.Database, live);
                IImage image = GetCover(res);

                if (res.Recognized || item.Text != res.DisplayText || item.ImageKey != res.ImageKey || item.ToolTip != res.ToolTip)
                {
                    Ui.Post(() =>
                    {
                        item.Text = res.DisplayText;
                        item.ToolTip = res.ToolTip;
                        item.ImageKey = res.ImageKey;
                        item.Image = image;
                        ApplySorting();
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.eLogLevel.DebugVerbose, "Failed to analyze '" + item.Entry.FileName + "': " + ex.Message);
            }
        }

        private IImage GetCover(TonieResolution res)
        {
            switch (res.Kind)
            {
                case TonieKind.Custom:
                    return Covers.Custom;
                case TonieKind.Unknown:
                    return Covers.Unknown;
            }

            lock (CoverCache)
            {
                if (CoverCache.TryGetValue(res.ImageKey, out IImage cached))
                {
                    return cached;
                }
            }

            IImage image = null;
            if (!string.IsNullOrEmpty(res.PicUrl))
            {
                byte[] data = Services.Images.GetImage(res.PicUrl, res.ImageKey);
                if (data != null)
                {
                    image = Covers.FromBytes(data);
                }
            }

            if (image == null)
            {
                return Covers.Unknown;
            }

            lock (CoverCache)
            {
                CoverCache[res.ImageKey] = image;
            }
            return image;
        }

        #endregion

        #region Sorting

        partial void OnSortModeChanged(int value)
        {
            ApplySorting();
        }

        /// <summary>Re-orders <see cref="Items"/> in place (Move keeps the selection).</summary>
        internal void ApplySorting()
        {
            var sorted = Items.ToList();
            sorted.Sort(Compare);

            for (int i = 0; i < sorted.Count; i++)
            {
                int current = Items.IndexOf(sorted[i]);
                if (current != i)
                {
                    Items.Move(current, i);
                }
            }
        }

        private int Compare(TonieItemViewModel a, TonieItemViewModel b)
        {
            switch (SortMode)
            {
                case 0:
                    return CompareStrings(a.Entry.Uid, b.Entry.Uid);
                case 1:
                    return CompareStrings(a.Entry.Info?.Model, b.Entry.Info?.Model);
                case 3:
                    int group = a.GroupOrder.CompareTo(b.GroupOrder);
                    return group != 0 ? group : CompareStrings(SortString(a), SortString(b));
                default:
                    return a.Entry.FileInfo.CreationTime.CompareTo(b.Entry.FileInfo.CreationTime);
            }
        }

        private static string SortString(TonieItemViewModel item)
        {
            try
            {
                return item.Entry.Info?.SortString;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static int CompareStrings(string a, string b)
        {
            if (a == null || b == null)
            {
                return (a == null ? 0 : 1) - (b == null ? 0 : 1);
            }
            return string.Compare(a, b, StringComparison.CurrentCulture);
        }

        #endregion

        #region Status bar

        internal void UpdateStatusBar()
        {
            string text = "";
            RfidReaderBase reader = RfidReader;

            if (reader != null && reader.Connected)
            {
                string voltageString = Settings.DebugWindow ? ", " + reader.AntennaVoltage.ToString("0.00", CultureInfo.InvariantCulture) + " V" : "";

                text += " | " + reader.HardwareType + " (FW: " + (reader.UnlockSupported ? "SLIX-L enabled" : "stock") + voltageString + ") found at " + reader.CurrentPort + ". The UID of the tag will be automatically used where applicable.";
            }

            if (Settings.NfcEnabled && (reader == null || !reader.Connected))
            {
                string[] denied = SerialPorts.PermissionDenied;
                if (denied.Length > 0)
                {
                    text += " | No permission to open " + string.Join(", ", denied) + " - add your user to the 'dialout' group and log in again";
                }
            }

            int tonieCount = Services.Database.Count;
            if (tonieCount == 0)
            {
                text += " | No tonies.json loaded, consider downloading it";
            }
            else
            {
                text += " | Have " + tonieCount + " entries in tonies.json";
            }

            string dbStatus = Services.Database.Status;
            if (!string.IsNullOrEmpty(dbStatus))
            {
                text += " " + dbStatus;
            }

            StatusText = text.Trim().Trim('|').Trim();
        }

        #endregion

        #region Playback

        internal void PlayStopCore()
        {
            if (Services.Player.IsPlaying)
            {
                Services.Player.Stop();
                return;
            }

            if (SelectedItems.Count != 1)
            {
                return;
            }

            string file = SelectedItems[0].Entry.FileName;
            try
            {
                Services.Player.Play(file);
                PlayButtonText = "Stop";
            }
            catch (Exception ex)
            {
                _ = Dialogs.ShowMessage("Failed to play file '" + file + "': " + ex.Message, "Playback", true);
            }
        }

        partial void OnPlayPositionChanged(double value)
        {
            if (!UpdatingPlayPosition)
            {
                Services.Player.Seek((int)value);
            }
        }

        private void Player_Progress(TimeSpan position, TimeSpan total)
        {
            Ui.Post(() =>
            {
                UpdatingPlayPosition = true;
                PlayLength = Math.Max(1, (int)total.TotalSeconds);
                PlayPosition = Math.Min(PlayLength, (int)position.TotalSeconds);
                UpdatingPlayPosition = false;
                PlayTimeText = position.ToString(@"hh\:mm\:ss") + "/" + total.ToString(@"hh\:mm\:ss");
            });
        }

        private void Player_Stopped(Exception error)
        {
            Ui.Post(() =>
            {
                PlayButtonText = "Play file";
                if (error != null)
                {
                    _ = Dialogs.ShowMessage("Failed to play file: " + error.Message, "Playback", true);
                }
            });
        }

        #endregion

        private void SelectedItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!SelectingFromReader)
            {
                AutoSelected = false;
            }
            NotifySelectionCommands();
        }

        internal void RequestScrollIntoView(TonieItemViewModel item)
        {
            ScrollIntoViewRequested?.Invoke(item);
        }

        internal void RequestClose()
        {
            CloseRequested?.Invoke();
        }
    }
}

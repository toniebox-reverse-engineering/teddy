using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeddyBench.Properties;
using TeddyBench.Core;
using TonieFile;
using Application = System.Windows.Forms.Application;
using Settings = TeddyBench.Core.Settings;

namespace TeddyBench
{
    public partial class TeddyMain : Form
    {
        private SafeThread AnalyzeThread = null;
        private bool AnalyzeThreadStop = false;
        private SafeThread EncodeThread = null;
        private SafeThread LogThread;
        private bool LogThreadStop;
        private SafeThread UpdateCheckThread = null;

        private string _CurrentDirectory = null;
        /* mirrored into the card monitor, which reports when it disappears */
        private string CurrentDirectory
        {
            get => _CurrentDirectory;
            set
            {
                _CurrentDirectory = value;
                CardMonitor.CurrentDirectory = value;
            }
        }
        private FileSystemWatcher DirectoryWatcher = null;
        private bool AutoOpenDrive = true;
        private Dictionary<CardEntry, ListViewItem> RegisteredItems = new Dictionary<CardEntry, ListViewItem>();

        private readonly TonieDatabase TonieDb = new TonieDatabase();
        private readonly TonieFileCache HeaderCache = new TonieFileCache();
        private readonly ImageCache Images = new ImageCache();
        private readonly CardMonitor CardMonitor = new CardMonitor();
        private readonly TonieAudioPlayer Player = new TonieAudioPlayer();
        private ListViewItem LastSelectediItem = null;
        private RfidReaderBase RfidReader;
        private bool AutoSelected;
        private Settings Settings = null;
        internal LogWindow Log;
        private string LastFoundUid = null;
        private SafeThread AsyncTagActionThread = null;
        private System.Windows.Forms.Timer StatusBarTimer = null;

        private string TitleString => "TeddyBench - " + GetVersion();

        private bool TrackMouseDown = false;

        public class ListViewItemComparer : IComparer
        {
            public int Characteristic = 0;
            public bool Order = true;

            public ListViewItemComparer(int ch)
            {
                Characteristic = ch;
            }

            public int Compare(object a, object b)
            {
                ListViewItem item1 = a as ListViewItem;
                ListViewItem item2 = b as ListViewItem;
                CardEntry t1 = item1.Tag as CardEntry;
                CardEntry t2 = item2.Tag as CardEntry;

                string s1 = "";
                string s2 = "";

                switch (Characteristic)
                {
                    case 0:
                        s1 = t1.Uid;
                        s2 = t2.Uid;
                        break;
                    case 1:
                        s1 = t1.Info?.Model;
                        s2 = t2.Info?.Model;
                        break;
                    case 2:
                        s1 = t1.FileInfo.CreationTime.ToString();
                        s2 = t2.FileInfo.CreationTime.ToString();
                        break;
                    case 3:
                        s1 = t1.Info?.SortString;
                        s2 = t2.Info?.SortString;
                        break;
                }

                if (s1 == null)
                {
                    return -1;
                }
                if (s2 == null)
                {
                    return 1;
                }

                int returnVal = String.Compare(s1, s2);

                return (Order ? 1 : -1) * returnVal;
            }
        }

        public void LoadJson(bool force = false)
        {
            TonieDb.Load(Settings.DownloadJson || force, TonieDatabase.DefaultUrl + ThisAssembly.Git.BaseTag);
        }

        public TeddyMain()
        {
            InitializeComponent();
            Log = new LogWindow();

            Settings = Settings.FromFile(AppPaths.SettingsFile);

            TonieDb.Error += ReportException;
            CardMonitor.CardFound += NotifyDrive;
            CardMonitor.CurrentDirectoryLost += NotifyDriveLost;
            Player.Progress += Player_Progress;
            Player.Stopped += Player_Stopped;

            lstTonies.LargeImageList = new ImageList();
            lstTonies.LargeImageList.ImageSize = new Size(128, 128);
            lstTonies.LargeImageList.ColorDepth = ColorDepth.Depth32Bit;
            lstTonies.LargeImageList.Images.Add("unknown", ResizeImage(Resources.unknown, 128, 128));
            lstTonies.LargeImageList.Images.Add("custom", ResizeImage(Resources.custom, 128, 128));
            lstTonies.ListViewItemSorter = new ListViewItemComparer(2);
            lstTonies.DoubleBuffered(true);
            cmbSorting.SelectedIndex = 2;
            Text = TitleString;


            StartThreads();

            UpdateCheckThread = new SafeThread(UpdateCheck, "UpdateCheckThread");
            UpdateCheckThread.Start();

            autodetectionEnabledToolStripMenuItem.Checked = Settings.NfcEnabled;
            ReportForm.DefaultUsername = Settings.Username;
            UpdateNfcReader();

            AllowDrop = true;

            if (Settings.DebugWindow)
            {
                Logger.LogLevel = Logger.eLogLevel.DebugVerbose;
                enableDebugModeToolStripMenuItem.Checked = true;
            }
            if (Settings.DownloadJson)
            {
                downloadToniesjsonOnStartupToolStripMenuItem.Checked = true;
            }

            StatusBarTimer = new System.Windows.Forms.Timer();
            StatusBarTimer.Tick += (object sender, EventArgs e) => { UpdateStatusBar(); };
            StatusBarTimer.Interval = 250;
            StatusBarTimer.Start();
        }

        void SaveSettings()
        {
            Settings.Save(AppPaths.SettingsFile);
        }


        private void Proxmark3_FlashResult(object sender, bool e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Proxmark3_FlashResult(sender, e)));
                return;
            }

            if (e)
            {
                MessageBox.Show("Flashing the device succeeded, will reconnect now", "Flashing Proxmark3 done");
            }
            else
            {
                MessageBox.Show(
                    "Flashing the device FAILED, it will reconnect now." + Environment.NewLine +
                    "" + Environment.NewLine +
                    "If Device starts with LEDs A and C lit and it doesn't show" + Environment.NewLine +
                    "on USB, replug the device with the button pressed and *keep*" + Environment.NewLine +
                    "it pressed until it's flashed again." + Environment.NewLine +
                    "" + Environment.NewLine +
                    "If this still fails, please" + Environment.NewLine +
                    " a) check the firmware file or" + Environment.NewLine +
                    " b) use the official flasher tool"
                    , "Flashing Proxmark3 failed");
            }
        }

        private void Proxmark3_FlashRequest(object sender, Proxmark3.FlashRequestContext e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Proxmark3_FlashRequest(sender, e)));
                return;
            }

            e.Proceed = false;

            if (e.Bootloader)
            {
                if (MessageBox.Show(
                    "=== FLASHING BOOTLOADER ===" + Environment.NewLine +
                    "" + Environment.NewLine +
                    "Please be aware that this procedure could fail and your device" + Environment.NewLine +
                    "gets bricked. To recover you need a special programmer." + Environment.NewLine +
                    "" + Environment.NewLine +
                    "Do you still want to flash the file " + e.FlashFile + "?",
                    "Flash Proxmark3?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    e.Proceed = true;
                }
            }
            else
            {

                if (MessageBox.Show(
                    "Please be aware that this procedure could fail and your device" + Environment.NewLine +
                    "main firmware is lost. (Device starts with LEDs A and C lit and" + Environment.NewLine +
                    "it doesn't enumerate on USB anymore)." + Environment.NewLine +
                    "No panic, the bootloader wasn't touched and you can reflash it." + Environment.NewLine +
                    "" + Environment.NewLine +
                    "If so, plug the device with the button pressed and *keep* it" + Environment.NewLine +
                    "pressed until it's flashed again." + Environment.NewLine +
                    "I did this procedure several times and it always worked." + Environment.NewLine +
                    "" + Environment.NewLine +
                    "If this still fails, please" + Environment.NewLine +
                    " a) check the firmware file or" + Environment.NewLine +
                    " b) use the official flasher tool" + Environment.NewLine +
                    "" + Environment.NewLine +
                    "Do you want to flash the file " + e.FlashFile + "?",
                    "Flash Proxmark3?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    e.Proceed = true;
                }
            }
        }

        private void Proxmark3_DeviceFound(object sender, string e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Proxmark3_DeviceFound(sender, e)));
                return;
            }

            if (e == null)
            {
                UpdateStatusBar();
                advancedActionsToolStripMenuItem.Enabled = false;
                reportProxmarkAnToolStripMenuItem.Enabled = false;
                reportNFCTagToolStripMenuItem.Enabled = false;
            }
            else
            {
                UpdateStatusBar();
                advancedActionsToolStripMenuItem.Enabled = RfidReader.UnlockSupported;
                reportProxmarkAnToolStripMenuItem.Enabled = true;
                reportNFCTagToolStripMenuItem.Enabled = true;

                flashBootloaderToolStripMenuItem.Enabled = (RfidReader.DeviceInfo & RfidReaderBase.eDeviceInfo.BootromPresent) != 0;
                flashFirmwareToolStripMenuItem.Enabled = (RfidReader.DeviceInfo & RfidReaderBase.eDeviceInfo.BootromPresent) != 0;
            }
        }

        private void UpdateStatusBar()
        {
            string text = "";

            if (RfidReader != null && RfidReader.Connected)
            {
                if (!statusStrip1.Visible)
                {
                    statusStrip1.Show();
                }
                string voltageString = Settings.DebugWindow ? ", " + RfidReader.AntennaVoltage.ToString("0.00", CultureInfo.InvariantCulture) + " V" : "";

                text += " | " + RfidReader.HardwareType + " (FW: " + (RfidReader.UnlockSupported ? "SLIX-L enabled" : "stock") + voltageString + ") found at " + RfidReader.CurrentPort + ". The UID of the tag will be automatically used where applicable.";
            }

            int tonieCount = TonieDb.Count;
            if (tonieCount == 0)
            {
                text += " | No tonies.json loaded, consider downloading it";
            }
            else
            {
                text += " | Have " + tonieCount + " entries in tonies.json";
            }
            string dbStatus = TonieDb.Status;
            if (!string.IsNullOrEmpty(dbStatus))
            {
                text += " " + dbStatus;
            }

            if (text == "")
            {
                if (statusStrip1.Visible)
                {
                    statusStrip1.Hide();
                }
            }
            else
            {
                if (!statusStrip1.Visible)
                {
                    statusStrip1.Show();
                }
                statusLabel.Text = text.Trim().Trim('|').Trim();
            }
        }

        private void Proxmark3_UidFound(object sender, string e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Proxmark3_UidFound(sender, e)));
                return;
            }

            bool found = false;

            if (e != null)
            {
                /* already handled this UID? then return */
                if (e == LastFoundUid)
                {
                    return;
                }

                foreach (ListViewItem item in lstTonies.Items)
                {
                    CardEntry tag = item.Tag as CardEntry;

                    if (tag.Uid == e)
                    {
                        if (lstTonies.SelectedItems.Count == 1 && lstTonies.SelectedItems[0] == item)
                        {
                            return;
                        }
                        foreach (ListViewItem sel in lstTonies.SelectedItems)
                        {
                            sel.Selected = false;
                        }
                        item.Selected = true;
                        item.EnsureVisible();
                        ActiveControl = lstTonies;
                        found = true;
                        AutoSelected = true;
                    }
                }
            }

            LastFoundUid = e;

            /* tag was removed and the selected item was auto selected because tag was found, deselect again */
            if (!found && AutoSelected)
            {
                foreach (ListViewItem sel in lstTonies.SelectedItems)
                {
                    sel.Selected = false;
                }
            }
        }

        public async void UpdateCheck()
        {
            try
            {
                string thisVersion = ThisAssembly.Git.BaseTag;
                if (string.IsNullOrEmpty(thisVersion))
                {
                    return;
                }
                Thread.Sleep(2000);

                UpdateInfo update = await new UpdateChecker().CheckAsync(thisVersion);
                if (update == null)
                {
                    return;
                }

                string destPath = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName;
                string zipName = Path.Combine(destPath, update.AssetName);

                BeginInvoke(new Action(async () =>
                {
                    UpdateNotifyDialog dlg = new UpdateNotifyDialog(update.LatestVersion + " (yours: " + thisVersion + ")", update.ReleaseName);
                    if (dlg.ShowDialog() == DialogResult.Yes)
                    {
                        await UpdateChecker.DownloadAsync(update.AssetUrl, zipName);
                        PlatformServices.Shell.OpenFolder(zipName);
                    }
                }));
            }
            catch (Exception ex)
            {
            }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            string[] drop = drgevent.Data.GetData(DataFormats.FileDrop) as string[];

            if (drop != null)
            {
                AddFiles(drop, GetAudioID());
            }
        }

        private uint GetAudioID()
        {
            if(MessageBox.Show("Do you want to set a specific Audio-ID? If you don't know, just say 'No'.", "Set specific Audio ID", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return uint.MaxValue;
            }

            AskHexForm form = new AskHexForm();
            if(form.ShowDialog() != DialogResult.OK)
            {
                return uint.MaxValue;
            }

            return form.Value;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopThreads();
            SaveSettings();
            base.OnFormClosing(e);
        }

        private void StopThreads()
        {
            CardMonitor.Stop();
            Player.Shutdown(1000);
            StopAnalyzeThread();
            if (UpdateCheckThread != null)
            {
                UpdateCheckThread.Join(100);
                UpdateCheckThread.Abort();
                UpdateCheckThread = null;
            }
            if (EncodeThread != null)
            {
                EncodeThread.Join(100);
                EncodeThread.Abort();
                EncodeThread = null;
            }
            if (LogThread != null)
            {
                LogThreadStop = true;
                LogThread.Join(100);
                LogThread.Abort();
                LogThread = null;
            }
            if (RfidReader != null)
            {
                RfidReader.Stop();
                RfidReader = null;
            }
        }

        private void StartThreads()
        {
            CardMonitor.Start();


            new SafeThread(() =>
            {
                LoadJson();
            }, "JSON Downloader").Start();
        }

        private void NotifyDriveLost()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => { NotifyDriveLost(); }));
                return;
            }
            grpCardContent.Visible = false;
            lblMessage.Visible = true;
            CurrentDirectory = null;
            AutoOpenDrive = true;
            Text = TitleString;
            StopAnalyzeThread();
        }

        private void NotifyDrive(string contentDirectory)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => { NotifyDrive(contentDirectory); }));
                return;
            }

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
                    Thread.Sleep(1000);
                }
            }
        }

        private void openDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                OpenPath(dlg.SelectedPath);
            }
        }

        private void OpenPath(string rootDirectory)
        {
            if (!Directory.Exists(rootDirectory))
            {
                CurrentDirectory = null;
                return;
            }

            CurrentDirectory = rootDirectory;

            DirectoryWatcher?.Dispose();
            DirectoryWatcher = new FileSystemWatcher(CurrentDirectory);

            DirectoryWatcher.Changed += OnDirectoryChanged;
            DirectoryWatcher.Created += OnDirectoryChanged;
            DirectoryWatcher.Deleted += OnDirectoryChanged;
            DirectoryWatcher.Renamed += OnDirectoryChanged;

            DirectoryWatcher.EnableRaisingEvents = true;
            RefreshCardContent();
        }

        private void OnDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            RefreshCardContent();
        }

        private void RefreshCardContent()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => { RefreshCardContent(); }));
                return;
            }

            StopAnalyzeThread();

            Text = TitleString + " - " + CurrentDirectory;

            lock (RegisteredItems)
            {
                btnAdd.Enabled = true;
                btnDelete.Enabled = true;
                btnSave.Enabled = true;

                txtLog.Visible = false;
                grpCardContent.Visible = true;
                lblMessage.Visible = false;
                lstTonies.Items.Clear();
                RegisteredItems.Clear();

                try
                {
                    foreach (CardEntry tag in new CardContent(CurrentDirectory).Scan())
                    {
                        ListViewItem item = new ListViewItem();

                        item.Tag = tag;
                        item.Text = tag.InitialText;
                        item.ImageKey = TonieResolver.ImageUnknown;
                        item.Group = lstTonies.Groups[3];

                        lstTonies.Items.Add(item);

                        RegisteredItems.Add(tag, item);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(Logger.eLogLevel.Error, "Exception while scanning directory '" + CurrentDirectory + "':" + ex.Message);
                }
                lstTonies.Sort();
            }

            StartAnalyzeThread();
        }

        private void StartAnalyzeThread()
        {
            AnalyzeThreadStop = false;
            AnalyzeThread = new SafeThread(AnalyzeMain, "AnalyzeThread");
            AnalyzeThread.Start();
        }

        private void StopAnalyzeThread()
        {
            if (AnalyzeThread != null)
            {
                AnalyzeThreadStop = true;
                if(!AnalyzeThread.Join(2000))
                {
                    Logger.Log(Logger.eLogLevel.Error, "Failed to stop Analyze Thread");
                }
                AnalyzeThread.Abort();
                AnalyzeThread = null;
            }
        }

        private void AnalyzeMain()
        {
            while (!AnalyzeThreadStop)
            {
                Thread.Sleep(100);

                lock (RegisteredItems)
                {
                    Logger.Log(Logger.eLogLevel.DebugVerbose, "Rescanning");
                    foreach (var entry in RegisteredItems)
                    {
                        CardEntry tag = entry.Key;
                        Logger.Log(Logger.eLogLevel.DebugVerbose, "  Filename: " + entry.Key.FileName);
                        Logger.Log(Logger.eLogLevel.DebugVerbose, "     Image: " + entry.Value.ImageKey);

                        if (entry.Value.ImageKey == TonieResolver.ImageUnknown)
                        {
                            try
                            {
                                Logger.Log(Logger.eLogLevel.DebugVerbose, "GetTonieAudio...");
                                TonieAudio dumpFile = HeaderCache.GetHeader(tag.FileName);
                                Logger.Log(Logger.eLogLevel.DebugVerbose, "GetTonieAudio... done");

                                bool live = PlatformServices.LiveFlag.IsLive(tag.FileName);
                                TonieResolution res = TonieResolver.Resolve(tag, dumpFile, TonieDb, live);

                                if (res.Kind == TonieKind.Known && !string.IsNullOrEmpty(res.PicUrl) && !lstTonies.LargeImageList.Images.ContainsKey(res.ImageKey))
                                {
                                    Image img = GetImage(res.PicUrl, res.ImageKey);
                                    if (img != null)
                                    {
                                        this.BeginInvoke(new Action(() =>
                                        {
                                            lstTonies.LargeImageList.Images.Add(res.ImageKey, img);
                                        }));
                                    }
                                }

                                Logger.Log(Logger.eLogLevel.DebugVerbose, "     Hash: " + tag.Hash);
                                Logger.Log(Logger.eLogLevel.DebugVerbose, "     AudioId: " + tag.AudioId);

                                bool update = res.Recognized;
                                update |= entry.Value.Text != res.DisplayText;
                                update |= entry.Value.ImageKey != res.ImageKey;
                                update |= entry.Value.ToolTipText != res.ToolTip;

                                if (update)
                                {
                                    this.BeginInvoke(new Action(() =>
                                    {
                                        Logger.Log(Logger.eLogLevel.DebugVerbose, "     Update list");
                                        entry.Value.Text = res.DisplayText;
                                        entry.Value.ImageKey = res.ImageKey;
                                        entry.Value.ToolTipText = res.ToolTip;
                                        UpdateSorting();
                                    }));
                                }
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                    }
                    Logger.Log(Logger.eLogLevel.DebugVerbose, "Done");
                }
            }
        }

        private Image GetImage(string pic, string hash)
        {
            byte[] data = Images.GetImage(pic, hash);
            if (data == null)
            {
                return null;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var img = Image.FromStream(stream))
                {
                    return ResizeImage(img, 128, 128);
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            double srcRatio = (double)image.Width / image.Height;
            int dstWidth = width;
            int dstHeight = (int)(dstWidth / srcRatio);

            if(dstHeight > image.Height)
            {
                dstHeight = height;
                dstWidth = (int)(dstHeight * srcRatio);
            }

            var destRect = new Rectangle(0, 0, dstWidth, dstHeight);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Multiselect = true;

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                AddFiles(dlg.FileNames, GetAudioID());
            }
        }

        private void AddFiles(string[] fileNames, uint id = uint.MaxValue)
        {
            AskUIDForm ask = new AskUIDForm(RfidReader);

            if (ask.ShowDialog() == DialogResult.OK)
            {
                if (fileNames.Count() == 1)
                {
                    string fileName = fileNames[0];

                    if (fileName.ToLower().EndsWith(".mp3") || fileName.ToLower().EndsWith(".ogg"))
                    {
                        switch (MessageBox.Show("You are about to encode a single MP3/Ogg, is this right?", "Encode a file", MessageBoxButtons.YesNo))
                        {
                            case DialogResult.No:
                                return;
                            case DialogResult.Yes:
                                EncodeFile(ask.Uid, new[] { fileName }, id);
                                return;
                        }
                    }
                    else
                    {
                        try
                        {
                            TonieAudio dumpFile = TonieAudio.FromFile(fileName);

                            if (dumpFile.FileContent.Length > 0)
                            {
                                CopyFile(ask.Uid, fileName);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("The file you have chosen is not supported.", "Add file...");
                            return;
                        }
                    }
                }
                else
                {
                    if (fileNames.Where(f => !(f.ToLower().EndsWith(".mp3") || f.ToLower().EndsWith(".ogg"))).Count() > 0)
                    {
                        MessageBox.Show("Please select MP3/Ogg files only.", "Add file...");
                        return;
                    }

                    TrackSortDialog sorter = new TrackSortDialog(fileNames);

                    if(sorter.ShowDialog() == DialogResult.Cancel)
                    {
                        return;
                    }

                    string[] sorted = sorter.SortedFiles;

                    EncodeFile(ask.Uid, sorted, id);
                }
            }
        }

        private void CopyFile(string uid, string fileName)
        {
            btnAdd.Enabled = false;
            btnDelete.Enabled = false;
            btnSave.Enabled = false;

            EncodeThread = new SafeThread(() =>
            {
                try
                {
                    new CardContent(CurrentDirectory).CopyTonieFile(uid, fileName);
                }
                catch (CardOperationException ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }

                RefreshCardContent();
            }, "EncodeThread");
            EncodeThread.Start();
        }

        public class EncodeCallback : TonieAudio.EncodeCallback
        {
            private readonly TeddyMain Main;
            private int LastPct = 0;

            public EncodeCallback(TeddyMain main)
            {
                Main = main;
            }

            public override void FileStart(int track, string sourceFile)
            {
                ParseName(track, sourceFile);
                Main.BeginInvoke(new Action(() => { Main.txtLog.Text += " File: " + ShortName; }));
            }

            public override void FileDone()
            {
                Main.BeginInvoke(new Action(() => { Main.txtLog.Text += "]" + Environment.NewLine; }));
            }

            public override void FileFailed(string message)
            {
                Main.BeginInvoke(new Action(() => { Main.txtLog.Text += "] FAILED" + Environment.NewLine; }));
            }

            public override void Progress(decimal pct)
            {
                LastPct = (int)(pct * 20);
                if (LastPct % 5 == 0)
                {
                    if (LastPct != 20)
                    {
                        Main.BeginInvoke(new Action(() => { Main.txtLog.Text += "" + (LastPct * 5) + "%"; }));
                    }
                }
                else
                {
                    Main.BeginInvoke(new Action(() => { Main.txtLog.Text += "."; }));
                }
            }

            public override void Failed(string message)
            {
                Main.BeginInvoke(new Action(() => { Main.txtLog.Text += "] FAILED" + Environment.NewLine; }));
            }

            public override void Warning(string message)
            {
                Main.BeginInvoke(new Action(() => { Main.txtLog.Text += Environment.NewLine + "Warning" + Environment.NewLine; }));
            }
        }

        private void EncodeFile(string uid, string[] filenames, uint id = uint.MaxValue)
        {
            btnAdd.Enabled = false;
            btnDelete.Enabled = false;
            btnSave.Enabled = false;

            grpCardContent.Visible = false;
            txtLog.Visible = true;

            EncodeThread = new SafeThread(() =>
            {
                try
                {
                    TonieAudio audio = CardContent.Encode(filenames, id, new EncodeCallback(this));
                    new CardContent(CurrentDirectory).WriteTonieFile(uid, audio.FileContent);
                }
                catch (CardOperationException ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }

                RefreshCardContent();
            }, "EncodeThread");
            EncodeThread.Start();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutDialog dlg = new AboutDialog();
            dlg.ShowDialog();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
        }



        #region Context menu for tonie files


        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (LastSelectediItem != null/* && LastSelectediItem.ImageKey == "custom"*/)
            {
                LastSelectediItem.BeginEdit();
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSelected();
        }

        private void exportTooggToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveSelected();
        }

        private void exportToToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportSelected();
        }

        private void assignNewUIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ReassignSelected();
        }

        private void showInExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (LastSelectediItem != null)
            {
                CardEntry tag = LastSelectediItem.Tag as CardEntry;
                PlatformServices.Shell.RevealFile(tag.FileName);
            }
        }

        private async void sendDiagnosticsReportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ReportSelected();
        }

        #endregion

        #region ListView callbacks

        private void lstTonies_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (lstTonies.FocusedItem.Bounds.Contains(e.Location))
                {
                    LastSelectediItem = lstTonies.SelectedItems[0];
                    TonieContextMenu.Show(System.Windows.Forms.Cursor.Position);
                }
            }
            else
            {
                if (lstTonies.SelectedItems.Count == 1)
                {
                    if (lstTonies.SelectedItems[0] == LastSelectediItem)
                    {
                        if (lstTonies.SelectedItems[0].ImageKey == "custom")
                        {
                            LastSelectediItem.BeginEdit();
                        }
                    }
                    LastSelectediItem = lstTonies.SelectedItems[0];
                }
            }
        }

        private void lstTonies_DoubleClick(object sender, EventArgs e)
        {
            ReassignSelected();
        }

        private void lstTonies_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.F2 && lstTonies.SelectedItems.Count > 0)
            {
                if (lstTonies.SelectedItems[0].ImageKey == "custom")
                {
                    lstTonies.SelectedItems[0].BeginEdit();
                }
            }
            if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem item in lstTonies.Items)
                {
                    item.Selected = true;
                }
            }
        }

        private void lstTonies_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected)
            {
                LastSelectediItem = null;
            }
            AutoSelected = false;
        }

        private void lstTonies_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            ListViewItem item = lstTonies.Items[e.Item];
            CardEntry tag = item.Tag as CardEntry;

            if (e.Label == null || tag.Hash == null)
            {
                return;
            }

            TonieDb.SetCustomName(tag.Hash, e.Label);
        }

        #endregion


        #region Selected item commands

        private void ReassignSelected()
        {
            if (lstTonies.SelectedItems.Count == 1)
            {
                ListViewItem item = lstTonies.SelectedItems[0];

                CardEntry tag = item.Tag as CardEntry;
                var fi = new FileInfo(tag.FileName);
                string oldUid = TonieTools.ReverseUid(fi.Directory.Name + fi.Name);

                AskUIDForm dlg = new AskUIDForm(RfidReader);
                dlg.Uid = oldUid;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        switch (new CardContent(CurrentDirectory).Reassign(tag, dlg.Uid))
                        {
                            case ReassignResult.Unchanged:
                                return;
                            case ReassignResult.MovedOldDirectoryKept:
                                MessageBox.Show("Success. Will not delete directory '" + fi.Directory.FullName + "' as it is not empty.", "Successfully re-assigned UID");
                                break;
                        }
                    }
                    catch (CardOperationException ex)
                    {
                        MessageBox.Show(ex.Message, ex.Title ?? "");
                        return;
                    }

                    RefreshCardContent();
                }
            }
        }

        private void DeleteSelected()
        {
            bool deleteAll = false;

            StopAnalyzeThread();

            if (lstTonies.SelectedItems.Count > 1)
            {
                switch (MessageBox.Show("Delete all selected files?", "Delete files?", MessageBoxButtons.YesNoCancel))
                {
                    case DialogResult.Cancel:
                        return;
                    case DialogResult.Yes:
                        deleteAll = true;
                        break;
                    case DialogResult.No:
                        break;
                }
            }

            foreach (ListViewItem item in lstTonies.SelectedItems)
            {
                CardEntry tag = item.Tag as CardEntry;
                string current = item.Text;

                if (!deleteAll)
                {
                    if (MessageBox.Show("Delete '" + current + "'?", "Delete files?", MessageBoxButtons.YesNo) == DialogResult.No)
                    {
                        continue;
                    }
                }
                try
                {
                    CardContent.Delete(tag);
                }
                catch (CardOperationException ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }
            }

            StartAnalyzeThread();
        }

        private void SaveSelected()
        {
            bool saveAll = true;
            bool createDirs = false;

            switch (MessageBox.Show("Create directories for selected file(s)?", "Save audio content", MessageBoxButtons.YesNoCancel))
            {
                case DialogResult.Cancel:
                    return;
                case DialogResult.Yes:
                    createDirs = true;
                    break;
                case DialogResult.No:
                    break;
            }

            FolderBrowserDialog dlg = new FolderBrowserDialog();

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                string outputLocation = dlg.SelectedPath;

                MessageBox.Show("You are about to export audio content of the tonie files on your SD card. " +
                    "Please *do not* share these files as they could contain information which can be used to identify your tonie ID. " +
                    "Also be aware that sharing these files is most likely illegal in your country.", "Legal information", MessageBoxButtons.OK);

                foreach (ListViewItem item in lstTonies.SelectedItems)
                {
                    CardEntry tag = item.Tag as CardEntry;
                    string current = item.Text;

                    if (!saveAll)
                    {
                        if (MessageBox.Show("Save '" + current + "'?", "Save audio content?", MessageBoxButtons.YesNo) == DialogResult.No)
                        {
                            continue;
                        }
                    }
                    try
                    {
                        CardContent.SaveAudio(tag, outputLocation, createDirs, TonieDb, GetVersion());
                    }
                    catch (CardOperationException ex)
                    {
                        MessageBox.Show(ex.Message);
                        return;
                    }
                }
            }
        }

        private void ExportSelected()
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();

            if (dlg.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            string outputLocation = dlg.SelectedPath;

            foreach (ListViewItem item in lstTonies.SelectedItems)
            {
                CardEntry tag = item.Tag as CardEntry;
                string current = item.Text;

                try
                {
                    CardContent.Export(tag, outputLocation);
                }
                catch (CardOperationException ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }
            }
        }

        #endregion

        private void cmbSorting_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSorting();
        }

        private void UpdateSorting()
        {
            foreach (ListViewItem item in lstTonies.Items)
            {
                if (cmbSorting.SelectedIndex == 3)
                {
                    switch (item.ImageKey)
                    {
                        case "unknown":
                            item.Group = lstTonies.Groups[0];
                            break;
                        case "custom":
                            item.Group = lstTonies.Groups[1];
                            break;
                        default:
                            item.Group = lstTonies.Groups[2];
                            break;
                    }
                }
                else
                {
                    item.Group = lstTonies.Groups[3];
                }
            }
            (lstTonies.ListViewItemSorter as ListViewItemComparer).Characteristic = cmbSorting.SelectedIndex;
            lstTonies.Sort();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopThreads();
            Application.Exit();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveSelected();
        }

        private string GetVersion()
        {
            return Application.ProductVersion + (ThisAssembly.Git.IsDirty ? ",dirty" : "");
        }

        private async void reportselectedFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ReportSelected();
        }

        private void toggleLiveFlagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool bUnhideAll = (lstTonies.SelectedItems.Count > 1);

            foreach (ListViewItem t in lstTonies.SelectedItems)
            {
                CardEntry tag = t.Tag as CardEntry;

                bool live = PlatformServices.LiveFlag.IsLive(tag.FileName);
                PlatformServices.LiveFlag.SetLive(tag.FileName, !(bUnhideAll || live));
            }

            RefreshCardContent();
        }

        private async Task<bool> ReportSelected()
        {
            if (lstTonies.SelectedItems.Count == 0)
            {
                return false;
            }

            var entries = lstTonies.SelectedItems.Cast<ListViewItem>().Select(t => t.Tag as CardEntry).ToList();
            string report = DiagnosticReports.AudioFiles(entries, TonieDb);

            ReportForm form = new ReportForm(report);

            if (form.ShowDialog() == DialogResult.OK)
            {
                Settings.Username = form.Username;
                SaveSettings();

                DiagStatus ret = await DiagnosticsSendInfo(report, form.Username, form.Message, "Audio Report.txt");
                if (ret.Success)
                {
                    MessageBox.Show("Report Successfully sent." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return true;
                }
                else
                {
                    MessageBox.Show("Error sending the report." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            return false;
        }


        private async void reportNFCTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RfidReader == null || AsyncTagActionThread != null)
            {
                return;
            }

            AsyncTagActionThread = new SafeThread(() =>
            {
                try
                {
                    byte[] data = RfidReader.ReadMemory();

                    Invoke(new Action(async () =>
                    {

                        if (data != null)
                        {
                            string report = DiagnosticReports.NfcTag(data);

                            ReportForm form = new ReportForm(report);

                            if (form.ShowDialog() == DialogResult.OK)
                            {
                                Settings.Username = form.Username;
                                SaveSettings();

                                DiagStatus ret = await DiagnosticsSendInfo(report, form.Username, form.Message, "Tag Dump.txt");
                                if (ret.Success)
                                {
                                    MessageBox.Show("Report Successfully sent." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                                else
                                {
                                    MessageBox.Show("Error sending the report." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }

                            }
                        }
                        else
                        {
                            MessageBox.Show("No tag found. Please make sure you position it correctly.", "Failed");
                        }
                    }));
                }
                catch (Exception ex)
                {
                }
                AsyncTagActionThread = null;
            }, "AsyncTagActionThread");
            AsyncTagActionThread.Start();
            TagOperationDialog opDlg = new TagOperationDialog(true);

            opDlg.Show();

            await Task.Run(() =>
            {
                while (AsyncTagActionThread != null)
                {
                    Thread.Sleep(100);
                    if (opDlg.DialogResult == DialogResult.Cancel)
                    {
                        AsyncTagActionThread.Abort();
                        AsyncTagActionThread = null;
                    }
                }
                Invoke(new Action(() => opDlg.Close()));
            });
        }

        private async void reportProxmarkAnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TagOperationDialog opDlg = new TagOperationDialog();

            opDlg.Show();
            Proxmark3.MeasurementResult result = RfidReader.MeasureAntenna();

            opDlg.Close();

            if (result == null)
            {
                MessageBox.Show("Measurement failed.", "Failed");
                return;
            }

            string content = DiagnosticReports.Antenna(result);

            ReportForm form = new ReportForm(content);

            if (form.ShowDialog() == DialogResult.OK)
            {
                Settings.Username = form.Username;
                SaveSettings();

                DiagStatus ret = await DiagnosticsSendInfo(content, form.Username, form.Message, "Antenna Report.txt");
                if (ret.Success)
                {
                    MessageBox.Show("Report Successfully sent." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Error sending the report." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }
        }

        private Task<DiagStatus> DiagnosticsSendInfo(string payload, string sender, string message, string filename)
        {
            return new DiagnosticsClient(GetVersion()).SendAsync(payload, sender, message, filename);
        }

        private async void readContentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RfidReader == null || AsyncTagActionThread != null)
            {
                return;
            }

            AsyncTagActionThread = new SafeThread(() =>
            {
                try
                {
                    byte[] data = RfidReader.ReadMemory();

                    Invoke(new Action(() =>
                    {
                        if (data != null)
                        {
                            TagDumpDialog dlg = new TagDumpDialog(true, BitConverter.ToString(data).Replace("-", ""));
                            dlg.ShowDialog();
                        }
                        else
                        {
                            MessageBox.Show("No tag found. Please make sure you position it correctly.", "Failed");
                        }
                    }));
                }
                catch (Exception ex)
                {
                }
                AsyncTagActionThread = null;
            }, "AsyncTagActionThread");
            AsyncTagActionThread.Start();
            TagOperationDialog opDlg = new TagOperationDialog(true);

            opDlg.Show();

            await Task.Run(() =>
            {
                while (AsyncTagActionThread != null)
                {
                    Thread.Sleep(100);
                    if (opDlg.DialogResult == DialogResult.Cancel)
                    {
                        AsyncTagActionThread.Abort();
                        AsyncTagActionThread = null;
                    }
                }
                Invoke(new Action(() => opDlg.Close()));
            });
        }

        private async void emulateTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RfidReader == null || AsyncTagActionThread != null)
            {
                return;
            }

            string previous = "";

            while (true)
            {
                TagDumpDialog dlg = new TagDumpDialog(false, previous);

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    byte[] data = null;

                    previous = dlg.String;

                    try
                    {
                        data = Helpers.ConvertHexStringToByteArray(previous);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to parse '" + previous + "'."+Environment.NewLine+ "The hex code must represent 40 byte in hexadecimal numbers:" + Environment.NewLine + "8 byte UUID and 32 byte data.");
                        continue;
                    }

                    AsyncTagActionThread = new SafeThread(() =>
                    {
                        try
                        {
                            RfidReader.EmulateTag(data);
                        }
                        catch (Exception ex)
                        {
                        }
                        AsyncTagActionThread = null;
                    }, "AsyncTagActionThread");
                    AsyncTagActionThread.Start();
                }
                break;
            }

            TagOperationDialog opDlg = new TagOperationDialog();

            opDlg.Show();

            await Task.Run(() =>
            {
                while (AsyncTagActionThread != null)
                {
                    Thread.Sleep(100);
                    if (opDlg.DialogResult == DialogResult.Cancel)
                    {
                        AsyncTagActionThread.Abort();
                        AsyncTagActionThread = null;
                    }
                }
                Invoke(new Action(() => opDlg.Close()));
            });
        }

        private void autodetectionEnabledToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Settings.NfcEnabled = autodetectionEnabledToolStripMenuItem.Checked;
            SaveSettings();
            UpdateNfcReader();
        }

        private void UpdateNfcReader()
        {
            if (Settings.NfcEnabled)
            {
                if (RfidReader == null)
                {
                    if (Settings.NfcType == "Proxmark3")
                    {
                        RfidReader = new Proxmark3();
                        RfidReader.UidFound += Proxmark3_UidFound;
                        RfidReader.DeviceFound += Proxmark3_DeviceFound;
                        RfidReader.FlashRequest += Proxmark3_FlashRequest;
                        RfidReader.FlashResult += Proxmark3_FlashResult;
                        RfidReader.Start();
                    }
                    else if (Settings.NfcType == "Pn5180Esp")
                    {
                        RfidReader = new Pn5180Esp();
                        RfidReader.UidFound += Proxmark3_UidFound;
                        RfidReader.DeviceFound += Proxmark3_DeviceFound;
                        RfidReader.Start();
                    }
                }
            }
            else
            {
                if (RfidReader != null)
                {
                    RfidReader.Stop();
                    RfidReader = null;
                }
            }
        }

        private void autodetectionEnabledToolStripMenuItem_Click(object sender, EventArgs e)
        {
            autodetectionEnabledToolStripMenuItem.Checked ^= true;
        }

        private void enableDebugModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            enableDebugModeToolStripMenuItem.Checked ^= true;

            Settings.DebugWindow = enableDebugModeToolStripMenuItem.Checked;
            SaveSettings();

            if (Settings.DebugWindow)
            {
                Logger.LogLevel = Logger.eLogLevel.DebugVerbose;
                Log.Show();
            }
            else
            {
                Logger.LogLevel = Logger.eLogLevel.Warning;
                Log.Hide();
            }
        }

        private void measureAnteannaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TagOperationDialog opDlg = new TagOperationDialog();

            opDlg.Show();
            try
            {
                Proxmark3.MeasurementResult result = RfidReader.MeasureAntenna();

                opDlg.Close();

                if (result == null)
                {
                    MessageBox.Show("Measurement failed.", "Failed");
                    return;
                }

                PlotAntennaForm form = new PlotAntennaForm(result);

                form.ShowDialog();
            }
            catch (Exception ex)
            {
            }
        }

        private void flashFirmwareToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Title = "Please select the fullimage.elf you want to flash";
            dlg.Filter = "Firmware ELF files (*.elf)|*.elf|All files (*.*)|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    RfidReader.EnterBootloader(dlg.FileName);
                }
                catch (Exception ex)
                {
                }
            }
        }

        private void flashBootloaderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Title = "Please select the bootrom.elf you want to flash";
            dlg.Filter = "Bootloader ELF files (*.elf)|*.elf|All files (*.*)|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    RfidReader.EnterBootloader(dlg.FileName);
                }
                catch (Exception ex)
                {
                }
            }
        }

        private void consoleModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!consoleModeToolStripMenuItem.Checked)
            {
                consoleModeToolStripMenuItem.Checked = true;
                RfidReader.EnterConsole();
            }
            else
            {
                consoleModeToolStripMenuItem.Checked = false;
                RfidReader.ExitConsole();
            }
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (!Player.IsPlaying)
            {
                if (lstTonies.SelectedItems.Count != 1)
                {
                    return;
                }

                CardEntry tag = lstTonies.SelectedItems[0].Tag as CardEntry;

                try
                {
                    Player.Play(tag.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to play file '" + tag.FileName + "': " + ex.Message);
                    return;
                }
                btnPlay.Text = "Stop";
            }
            else
            {
                Player.Stop();
            }
        }

        private void Player_Progress(TimeSpan position, TimeSpan total)
        {
            int pos = (int)position.TotalSeconds;
            int len = (int)total.TotalSeconds;
            string time = position.ToString(@"hh\:mm\:ss") + "/" + total.ToString(@"hh\:mm\:ss");

            BeginInvoke(new Action(() =>
            {
                if (!TrackMouseDown)
                {
                    if (trackPlayPosition.Maximum != len)
                    {
                        trackPlayPosition.Maximum = len;
                        trackPlayPosition.TickFrequency = 60;
                    }
                    trackPlayPosition.Value = pos;
                }
                lblPlayTime.Text = time;
            }));
        }

        private void Player_Stopped(Exception error)
        {
            BeginInvoke(new Action(() =>
            {
                btnPlay.Text = "Play file";
                if (error != null)
                {
                    MessageBox.Show("Failed to play file: " + error.Message);
                }
            }));
        }

        private void trackPlayPosition_Scroll(object sender, EventArgs e)
        {
            Player.Seek(trackPlayPosition.Value);
        }

        private void trackPlayPosition_MouseDown(object sender, MouseEventArgs e)
        {
            TrackMouseDown = true;
        }

        private void trackPlayPosition_MouseUp(object sender, MouseEventArgs e)
        {
            TrackMouseDown = false;
        }

        internal void ReportException(string name, Exception e)
        {
            if (MessageBox.Show("Sorry, there was an exception in '" + name + "'. Do you want to report that crash?", "Some Exception happened", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }

            string report = DiagnosticReports.Exception(name, e);

            ReportForm form = new ReportForm(report);

            if (form.ShowDialog() == DialogResult.OK)
            {
                Settings.Username = form.Username;
                SaveSettings();

                DiagStatus ret = DiagnosticsSendInfo(report, form.Username, form.Message, "Exception.txt").Result;
                if (ret.Success)
                {
                    MessageBox.Show("Report Successfully sent." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Error sending the report." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }
        }

        private void downloadToniesjsonOnStartupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.DownloadJson = !downloadToniesjsonOnStartupToolStripMenuItem.Checked;
            SaveSettings();
        }

        private void downloadToniesjsonNowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new SafeThread(() =>
            {
                LoadJson(true);
            }, "JSON Downloader").Start();
        }
    }
}

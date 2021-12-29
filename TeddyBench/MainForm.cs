using AutoUpdateViaGitHubRelease;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScottPlot;
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
using System.Windows.Forms.VisualStyles;
using TeddyBench.Properties;
using TonieFile;
using Application = System.Windows.Forms.Application;

namespace TeddyBench
{
    public partial class TeddyMain : Form
    {
        private Thread ScanCardThread = null;
        private bool ScanCardThreadStop = false;
        private Thread AnalyzeThread = null;
        private bool AnalyzeThreadStop = false;
        private Thread EncodeThread = null;
        private Thread LogThread;
        private bool LogThreadStop;
        private Thread UpdateCheckThread = null;

        private string CurrentDirectory = null;
        private bool AutoOpenDrive = true;
        private Dictionary<ListViewTag, ListViewItem> RegisteredItems = new Dictionary<ListViewTag, ListViewItem>();
        private TextWriter ConsoleWriter = null;
        private static TonieTools.TonieData[] TonieInfos;
        private static Dictionary<string, string> CustomTonies = new Dictionary<string, string>();
        private Dictionary<string, Tuple<TonieAudio, DateTime>> CachedAudios = new Dictionary<string, Tuple<TonieAudio, DateTime>>();
        private ListViewItem LastSelectediItem = null;
        private Proxmark3 Proxmark3;
        private bool AutoSelected;
        private Settings Settings = null;
        internal LogWindow Log;
        private string LastFoundUid = null;
        private Thread AsyncTagActionThread = null;
        private System.Windows.Forms.Timer StatusBarTimer = null;
        private Update Updater;

        private string TitleString => "TeddyBench (beta) - " + GetVersion();

        public class ListViewTag
        {
            public string FileName;
            public FileInfo FileInfo;
            public string Hash;
            public TonieTools.TonieData Info;
            public string Uid;
            public int AudioId;

            public ListViewTag(string filename)
            {
                FileName = filename;

                FileInfo = new FileInfo(FileName);
                Uid = ReverseUid(FileInfo.Directory.Name + FileInfo.Name);
            }
        }

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
                ListViewTag t1 = item1.Tag as ListViewTag;
                ListViewTag t2 = item2.Tag as ListViewTag;

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

        public static void LoadJson()
        {
            try
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://gt-blog.de/JSON/tonies.json?source=TeddyBench&version=" + ThisAssembly.Git.BaseTag);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    TextReader reader = new StreamReader(response.GetResponseStream());
                    string content = reader.ReadToEnd();
                    File.WriteAllText("tonies.json", content);
                }
                catch (Exception e)
                {
                }

                try
                {
                    TonieInfos = JsonConvert.DeserializeObject<TonieTools.TonieData[]>(File.ReadAllText("tonies.json"));
                }
                catch (Exception e)
                {
                    TonieInfos = new TonieTools.TonieData[0];
                }

                try
                {
                    CustomTonies = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("customTonies.json"));
                }
                catch (Exception e)
                {
                    CustomTonies = new Dictionary<string, string>();
                }
            }
            catch (Exception e)
            {
                return;
            }
        }

        void SaveJson()
        {
            try
            {
                File.WriteAllText("customTonies.json", JsonConvert.SerializeObject(CustomTonies, Formatting.Indented));
            }
            catch (Exception e)
            {
                return;
            }
        }

        public TeddyMain()
        {
            InitializeComponent();
            Log = new LogWindow();

            Settings = Settings.FromFile("teddyBench.cfg");

            lstTonies.LargeImageList = new ImageList();
            lstTonies.LargeImageList.ImageSize = new Size(128, 128);
            lstTonies.LargeImageList.ColorDepth = ColorDepth.Depth32Bit;
            lstTonies.LargeImageList.Images.Add("unknown", ResizeImage(Resources.unknown, 128, 128));
            lstTonies.LargeImageList.Images.Add("custom", ResizeImage(Resources.custom, 128, 128));
            lstTonies.ListViewItemSorter = new ListViewItemComparer(2);
            cmbSorting.SelectedIndex = 2;
            Text = TitleString;

            LoadJson();
            StartThreads();


            UpdateCheckThread = new Thread(UpdateCheck);
            UpdateCheckThread.Start();

            autodetectionEnabledToolStripMenuItem.Checked = Settings.NfcEnabled;
            ReportForm.DefaultUsername = Settings.Username;
            UpdateNfcReader();

            AllowDrop = true;

            if (Settings.DebugWindow)
            {
                LogWindow.LogLevel = LogWindow.eLogLevel.Debug;
                enableDebugModeToolStripMenuItem.Checked = true;
            }

            StatusBarTimer = new System.Windows.Forms.Timer();
            StatusBarTimer.Tick += (object sender, EventArgs e) => { UpdateStatusBar(); };
            StatusBarTimer.Interval = 500;
            StatusBarTimer.Start();
        }

        private void Update_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Updater.Available)
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Version version = assembly.GetName().Version;
                Updater.StartInstall(assembly.Location);
                Application.Exit();
            }
        }

        void SaveSettings()
        {
            Settings.Save("teddyBench.cfg");
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
                MessageBox.Show("Flashing the device succeeded, it will reconnect now", "Flashing Proxmark3 done");
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
            }
            else
            {
                UpdateStatusBar();
                advancedActionsToolStripMenuItem.Enabled = Proxmark3.UnlockSupported;
                reportProxmarkAnToolStripMenuItem.Enabled = true;
            }
        }

        private void UpdateStatusBar()
        {
            if (Proxmark3 != null && Proxmark3.Connected)
            {
                if (!statusStrip1.Visible)
                {
                    statusStrip1.Show();
                }
                string voltageString = Settings.DebugWindow ? ", " + Proxmark3.AntennaVoltage.ToString("0.00", CultureInfo.InvariantCulture) + " V" : "";
                statusLabel.Text = "Proxmark3 (FW: " + (Proxmark3.UnlockSupported ? "SLIX-L enabled" : "stock") + voltageString + ") found at " + Proxmark3.CurrentPort + ". The UID of the tag will be automatically used where applicable.";
            }
            else
            {
                if (statusStrip1.Visible)
                {
                    statusStrip1.Hide();
                }
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
                if(e == LastFoundUid)
                {
                    return;
                }

                foreach (ListViewItem item in lstTonies.Items)
                {
                    ListViewTag tag = item.Tag as ListViewTag;

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
                Thread.Sleep(2000);

                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; TeddyBench/1.0)");
                async Task<JObject> GithubApiGet(string path) => JObject.Parse(await client.GetStringAsync($"https://api.github.com/{path}"));
                async Task<JObject> GithubLastRelease(string user, string repo) => await GithubApiGet($"repos/{user}/{repo}/releases/latest");

                async Task DownloadFile(string url, string destinationFileName)
                {
                    using var stream = await client.GetStreamAsync(url);
                    using var file = new FileStream(destinationFileName, FileMode.Create);
                    stream.CopyTo(file);
                }

                dynamic latestRelease = await GithubLastRelease("toniebox-reverse-engineering", "teddy");
                string latestVersion = latestRelease.tag_name;

                if (latestVersion != ThisAssembly.Git.BaseTag)
                {
                    string destPath = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName;
                    string zipName = Path.Combine(destPath, "TeddyBench.zip");

                    if (latestRelease.assets[0].name == "TeddyBench.zip")
                    {
                        string url = latestRelease.assets[0].browser_download_url;

                        BeginInvoke(new Action(async () =>
                        {
                            UpdateNotifyDialog dlg = new UpdateNotifyDialog(latestVersion, (string)latestRelease.name);
                            if (dlg.ShowDialog() == DialogResult.Yes)
                            {
                                await DownloadFile(url, zipName);
                                ProcessStartInfo startInfo = new ProcessStartInfo
                                {
                                    Arguments = zipName,
                                    FileName = "explorer.exe"
                                };

                                Process.Start(startInfo);
                            }
                        }));
                    }
                }
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
                AddFiles(drop);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopThreads();
            SaveSettings();
            base.OnFormClosing(e);
        }

        private void StopThreads()
        {
            if (ScanCardThread != null)
            {
                ScanCardThreadStop = true;
                ScanCardThread.Join(100);
                //ScanCardThread.Abort();
                ScanCardThread = null;
            }
            StopAnalyzeThread();
            if (UpdateCheckThread != null)
            {
                UpdateCheckThread.Join(100);
                //UpdateCheckThread.Abort();
                UpdateCheckThread = null;
            }
            if (EncodeThread != null)
            {
                EncodeThread.Join(100);
                //EncodeThread.Abort();
                EncodeThread = null;
            }
            if (LogThread != null)
            {
                LogThreadStop = true;
                LogThread.Join(100);
                //LogThread.Abort();
                LogThread = null;
            }
            if (Proxmark3 != null)
            {
                Proxmark3.Stop();
                Proxmark3 = null;
            }
        }

        private void StartThreads()
        {
            ScanCardThreadStop = false;
            ScanCardThread = new Thread(ScanCardMain);
            ScanCardThread.Start();
        }

        private void ScanCardMain()
        {
            while (!ScanCardThreadStop)
            {
                try
                {
                    var drives = DriveInfo.GetDrives();

                    foreach (var drive in drives)
                    {
                        if (drive.DriveType == DriveType.Removable)
                        {
                            try
                            {
                                if (drive.IsReady && drive.RootDirectory.GetDirectories().Where(d => d.Name == "CONTENT").Count() == 1)
                                {
                                    NotifyDrive(drive);
                                }
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                    }

                    if (CurrentDirectory != null)
                    {
                        bool failedRead = false;

                        try
                        {
                            if (!new DirectoryInfo(CurrentDirectory).Exists)
                            {
                                failedRead = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            failedRead = true;
                        }

                        if (failedRead)
                        {
                            NotifyDriveLost();
                        }
                    }
                }
                catch (Exception ex)
                {
                }

                Thread.Sleep(500);
            }
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

        private void NotifyDrive(DriveInfo drive)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => { NotifyDrive(drive); }));
                return;
            }

            if (AutoOpenDrive)
            {
                OpenPath(Path.Combine(drive.RootDirectory.FullName, "CONTENT"));
                AutoOpenDrive = false;
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

            FileSystemWatcher watcher = new FileSystemWatcher(CurrentDirectory);

            watcher.Changed += OnDirectoryChanged;
            watcher.Created += OnDirectoryChanged;
            watcher.Deleted += OnDirectoryChanged;
            watcher.Renamed += OnDirectoryChanged;

            watcher.EnableRaisingEvents = true;
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
                    foreach (var dir in new DirectoryInfo(CurrentDirectory).GetDirectories())
                    {
                        var file = dir.GetFiles("*0304E0").FirstOrDefault();

                        if (file != null)
                        {
                            ListViewItem item = new ListViewItem();
                            ListViewTag tag = new ListViewTag(file.FullName);

                            item.Tag = tag;
                            item.Text = tag.Uid;
                            item.ImageKey = "unknown";
                            item.Group = lstTonies.Groups[3];

                            lstTonies.Items.Add(item);

                            RegisteredItems.Add(item.Tag as ListViewTag, item);
                        }
                    }
                }
                catch (Exception ex)
                {
                }
                lstTonies.Sort();
            }

            StartAnalyzeThread();
        }

        private void StartAnalyzeThread()
        {
            AnalyzeThreadStop = false;
            AnalyzeThread = new Thread(AnalyzeMain);
            AnalyzeThread.Start();
        }

        private void StopAnalyzeThread()
        {
            if (AnalyzeThread != null)
            {
                AnalyzeThreadStop = true;
                AnalyzeThread.Join(200);
                //AnalyzeThread.Abort();
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
                    LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "Rescanning");
                    foreach (var entry in RegisteredItems)
                    {
                        ListViewTag tag = entry.Key;
                        LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "  Filename: " + entry.Key.FileName);
                        LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     Image: " + entry.Value.ImageKey);

                        if (entry.Value.ImageKey == "unknown")
                        {
                            try
                            {
                                TonieAudio dumpFile = GetTonieAudio(tag.FileName);
                                string image = "";
                                string hash = BitConverter.ToString(dumpFile.Header.Hash).Replace("-", "");
                                var found = TonieInfos.Where(t => t.Hash.Where(h => h == hash).Count() > 0);
                                string tonieName = "[" + tag.Uid + "]" + Environment.NewLine + "(unknown: " + dumpFile.Header.AudioId.ToString("X8") + ")";

                                bool update = false;

                                if (found.Count() > 0)
                                {
                                    LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     Found");
                                    var info = found.First();
                                    tag.Info = info;
                                    tonieName = info.Title;

                                    if (!string.IsNullOrEmpty(info.Model))
                                    {
                                        tonieName = info.Model + " - " + tonieName;
                                    }
                                    if (!string.IsNullOrEmpty(info.Pic) && !lstTonies.LargeImageList.Images.ContainsKey(hash))
                                    {
                                        Image img = GetImage(info.Pic, hash);
                                        if (img != null)
                                        {
                                            this.BeginInvoke(new Action(() =>
                                            {
                                                lstTonies.LargeImageList.Images.Add(hash, img);
                                            }));
                                        }
                                    }
                                    update = true;
                                    image = hash;
                                }
                                else
                                {
                                    tag.Info = new TonieTools.TonieData();
                                    image = "unknown";

                                    if (CustomTonies.ContainsKey(hash))
                                    {
                                        LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     known custom tonie");
                                        tonieName = CustomTonies[hash];
                                        tag.Info.Title = tonieName;
                                        image = "custom";
                                        update = true;
                                    }
                                    else if (dumpFile.Header.AudioId < 0x50000000)
                                    {
                                        LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     unknown custom tonie");
                                        tonieName = "Unnamed Teddy - " + tonieName;
                                        tag.Info.Title = "Unnamed Teddy";
                                        image = "custom";
                                        update = true;
                                    }
                                    else
                                    {
                                        LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     Not found -> unknown");
                                    }
                                }

                                tag.Hash = hash;
                                tag.AudioId = dumpFile.Header.AudioId;
                                LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     Hash: " + tag.Hash);
                                LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     AudioId: " + tag.AudioId);

                                string newText = tonieName;
                                string newImakeKey = image;
                                string newToolTipText =
                                    "File:     " + tag.FileName + Environment.NewLine +
                                    "UID:      " + tag.Uid + Environment.NewLine +
                                    "Date:     " + tag.FileInfo.CreationTime + Environment.NewLine +
                                    "AudioID:  0x" + tag.AudioId.ToString("X8") + Environment.NewLine +
                                    "Chapters: " + dumpFile.Header.AudioChapters.Length + Environment.NewLine;

                                update |= entry.Value.Text != newText;
                                update |= entry.Value.ImageKey != newImakeKey;
                                update |= entry.Value.ToolTipText != newToolTipText;

                                if (update)
                                {
                                    this.BeginInvoke(new Action(() =>
                                    {
                                        LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     Update list");
                                        entry.Value.Text = newText;
                                        entry.Value.ImageKey = newImakeKey;
                                        entry.Value.ToolTipText = newToolTipText;
                                        UpdateSorting();
                                    }));
                                }
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                    }
                    LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "Done");
                }
            }
        }

        private Image GetImage(string pic, string hash)
        {
            string cacheFileName = Path.Combine("cache", hash + ".png");

            try
            {
                if(!Directory.Exists("cache"))
                {
                    Directory.CreateDirectory("cache");
                }

                if (File.Exists(cacheFileName))
                {
                    return Image.FromFile(cacheFileName);
                }
            }
            catch (Exception ex)
            {
            }

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(pic);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Image img = ResizeImage(Image.FromStream(response.GetResponseStream()), 128, 128);

                try
                {
                    img.Save(cacheFileName);
                }
                catch (Exception ex)
                {
                }
                return img;
            }
            catch (Exception ex)
            {
            }

            return null;
        }

        private TonieAudio GetTonieAudio(string fileName)
        {
            FileInfo info = new FileInfo(fileName);
            if (CachedAudios.ContainsKey(fileName))
            {
                Tuple<TonieAudio, DateTime> cachedItem = CachedAudios[fileName];
                if(cachedItem.Item2 == info.LastWriteTime)
                {
                    return cachedItem.Item1;
                }
                CachedAudios.Remove(fileName);
            }

            TonieAudio file = TonieAudio.FromFile(fileName, false);
            CachedAudios.Add(fileName, new Tuple<TonieAudio, DateTime>(file, info.LastWriteTime));

            return file;
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
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
                AddFiles(dlg.FileNames);
            }
        }

        private void AddFiles(string[] fileNames)
        {
            AskUIDForm ask = new AskUIDForm(Proxmark3);

            if (ask.ShowDialog() == DialogResult.OK)
            {
                if (fileNames.Count() == 1)
                {
                    string fileName = fileNames[0];

                    if (fileName.ToLower().EndsWith(".mp3"))
                    {
                        switch (MessageBox.Show("You are about to encode a single MP3, is this right?", "Encode a MP3 file", MessageBoxButtons.YesNo))
                        {
                            case DialogResult.No:
                                return;
                            case DialogResult.Yes:
                                EncodeFile(ask.Uid, new[] { fileName });
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
                    if (fileNames.Where(f => !f.ToLower().EndsWith(".mp3")).Count() > 0)
                    {
                        MessageBox.Show("Please select MP3 files only.", "Add file...");
                        return;
                    }

                    EncodeFile(ask.Uid, fileNames);
                }
            }
        }

        private void CopyFile(string uid, string fileName)
        {
            btnAdd.Enabled = false;
            btnDelete.Enabled = false;
            btnSave.Enabled = false;

            EncodeThread = new Thread(() =>
            {
                string newDir = Path.Combine(CurrentDirectory, ReverseUid(uid).Substring(0, 8));
                string newFile = Path.Combine(newDir, ReverseUid(uid).Substring(8, 8));

                try
                {
                    Directory.CreateDirectory(newDir);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to create directory '" + newDir + "'");
                    return;
                }

                try
                {
                    File.WriteAllBytes(newFile, File.ReadAllBytes(fileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to write file '" + newFile + "'");
                    return;
                }

                RefreshCardContent();
            });
            EncodeThread.Start();
        }

        private void EncodeFile(string uid, string[] v)
        {
            btnAdd.Enabled = false;
            btnDelete.Enabled = false;
            btnSave.Enabled = false;

            grpCardContent.Visible = false;
            txtLog.Visible = true;

            EncodeThread = new Thread(() =>
            {
                string newDir = Path.Combine(CurrentDirectory, ReverseUid(uid).Substring(0, 8));
                string newFile = Path.Combine(newDir, ReverseUid(uid).Substring(8, 8));

                TonieAudio audio = null;

                StartLog();

                try
                {
                    uint id = (uint)DateTimeOffset.Now.ToUnixTimeSeconds() - 0x50000000;
                    audio = new TonieAudio(v, id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to encode file. Sorry. Most likely a MP3 was corrupted." + Environment.NewLine + "(" + ex.Message + ")");
                    return;
                }

                try
                {
                    Directory.CreateDirectory(newDir);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to create directory '" + newDir + "'");
                    return;
                }
                try
                {
                    File.WriteAllBytes(newFile, audio.FileContent);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to write file '" + newFile + "'");
                    return;
                }

                StopLog();
                RefreshCardContent();
            });
            EncodeThread.Start();
        }

        private void StartLog()
        {
            ConsoleWriter = new StringWriter(new StringBuilder());
            Console.SetOut(ConsoleWriter);

            LogThreadStop = false;
            LogThread = new Thread(LogMain);
            LogThread.Start();
        }

        private void LogMain()
        {
            while (!LogThreadStop)
            {
                Thread.Sleep(50);
                Invoke(new Action(() =>
                {
                    txtLog.Text = ConsoleWriter.ToString();
                }));
            }
        }

        private void StopLog()
        {
            LogThreadStop = true;
            LogThread.Join(500);
            //LogThread.Abort();
        }

        public static string ReverseUid(string uid)
        {
            List<string> groups = (from Match m in Regex.Matches(uid, @"[A-Fa-f0-9]{2}") select m.Value).ToList();
            groups.Reverse();
            string ret = string.Join("", groups.ToArray());

            return ret;
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
            if (LastSelectediItem != null && LastSelectediItem.ImageKey == "custom")
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

        private void assignNewUIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ReassignSelected();
        }

        private void showInExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (LastSelectediItem != null)
            {
                ListViewTag tag = LastSelectediItem.Tag as ListViewTag;
                System.Diagnostics.Process.Start("explorer.exe", "/select, \"" + tag.FileName + "\"");
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
                    //TonieContextMenu.Show(Cursor.Position);
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
            ListViewTag tag = item.Tag as ListViewTag;

            if (e.Label == null)
            {
                return;
            }

            if (!CustomTonies.ContainsKey(tag.Hash))
            {
                CustomTonies.Add(tag.Hash, "");
            }
            CustomTonies[tag.Hash] = e.Label;

            SaveJson();
        }

        #endregion


        #region Selected item commands

        private void ReassignSelected()
        {
            if (lstTonies.SelectedItems.Count == 1)
            {
                ListViewItem item = lstTonies.SelectedItems[0];

                ListViewTag tag = item.Tag as ListViewTag;
                var fi = new FileInfo(tag.FileName);
                string oldUid = ReverseUid(fi.DirectoryName + fi.Name);

                AskUIDForm dlg = new AskUIDForm(Proxmark3);
                dlg.Uid = oldUid;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    string uid = dlg.Uid;
                    string newDir = Path.Combine(CurrentDirectory, ReverseUid(uid).Substring(0, 8));
                    string newFile = Path.Combine(newDir, ReverseUid(uid).Substring(8, 8));

                    if (new FileInfo(newFile).FullName == fi.FullName)
                    {
                        return;
                    }

                    if (new FileInfo(newFile).Exists)
                    {
                        MessageBox.Show("Failed to assign UID '" + uid + "' as this file already exists", "Re-assigning UID failed");
                        return;
                    }

                    try
                    {
                        Directory.CreateDirectory(newDir);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to create directory '" + newDir + "'", "Re-assigning UID failed");
                        return;
                    }

                    try
                    {
                        DirectoryInfo oldDir = fi.Directory;
                        fi.MoveTo(newFile);
                        if (oldDir.EnumerateFiles().Count() != 0 || (oldDir.EnumerateDirectories().Count() != 0))
                        {
                            MessageBox.Show("Success. Will not delete directory '" + oldDir.FullName + "' as it is not empty.", "Successfully re-assigned UID");
                        }
                        else
                        {
                            oldDir.Delete();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to write file '" + newFile + "'", "Re-assigning UID failed");
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
                ListViewTag tag = item.Tag as ListViewTag;
                string current = item.Text;

                if (!deleteAll)
                {
                    if (MessageBox.Show("Delete '" + current + "'?", "Delete files?", MessageBoxButtons.YesNo) == DialogResult.No)
                    {
                        continue;
                    }
                }
                FileInfo info = new FileInfo(tag.FileName);
                try
                {
                    info.Delete();
                    info.Directory.Delete();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to delete file/directory '" + tag.FileName + "'");
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
                    ListViewTag tag = item.Tag as ListViewTag;
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
                        string file = tag.FileName;

                        TonieAudio dump = TonieAudio.FromFile(file);

                        string[] titles = null;
                        List<string> tags = new List<string>();
                        tags.Add("TeddyVersion=" + GetVersion());
                        tags.Add("TeddyFile=" + file);

                        string hashString = BitConverter.ToString(dump.Header.Hash).Replace("-", "");
                        var found = TonieInfos.Where(t => t.Hash.Contains(hashString));
                        TonieTools.TonieData info = null;

                        if (found.Count() > 0)
                        {
                            info = found.First();
                            titles = info.Tracks;
                            tags.Add("ALBUM=" + info.Title);
                            tags.Add("ARTIST=" + info.Series);
                            tags.Add("LANGUAGE=" + info.Language);
                        }
                        tags.Add("HASH=" + hashString);

                        string inFile = new FileInfo(file).Name;
                        string inDir = new FileInfo(file).DirectoryName;
                        string outDirectory = !string.IsNullOrEmpty(outputLocation) ? outputLocation : inDir;

                        if (createDirs)
                        {
                            if (info == null)
                            {
                                outDirectory = Path.Combine(outDirectory, dump.Header.AudioId.ToString("X8") + " - " + tag.Uid);
                            }
                            else
                            {
                                outDirectory = Path.Combine(outDirectory, info.Model + " - " + dump.Header.AudioId.ToString("X8") + " - " + RemoveInvalidChars(info.Title).Trim());
                            }
                            if (!Directory.Exists(outDirectory))
                            {
                                Directory.CreateDirectory(outDirectory);
                            }
                        }

                        if (!Directory.Exists(outDirectory))
                        {
                            Console.WriteLine("Error: Output directory '" + outDirectory + "' does not exist");
                            return;
                        }

                        try
                        {
                            dump.DumpAudioFiles(outDirectory, inFile + "-" + dump.Header.AudioId.ToString("X8"), false, tags.ToArray(), titles);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[ERROR] Failed to write .ogg/.cue'");
                            Console.WriteLine("   Message:    " + ex.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to save file '" + tag.FileName + "'");
                        return;
                    }
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

        public static string RemoveInvalidChars(string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

        private string GetVersion()
        {
            return Application.ProductVersion + (ThisAssembly.Git.IsDirty ? ",dirty" : "");
        }

        private async void reportselectedFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ReportSelected();
        }

        private async Task<bool> ReportSelected()
        { 
            if (lstTonies.SelectedItems.Count == 0)
            {
                return false;
            }

            StringBuilder str = new StringBuilder();

            str.AppendLine("Dumping " + lstTonies.SelectedItems.Count + " files");

            foreach (ListViewItem t in lstTonies.SelectedItems)
            {
                ListViewTag tag = t.Tag as ListViewTag;

                AddInfo(str, tag);
            }

            ReportForm form = new ReportForm();

            if (form.ShowDialog() == DialogResult.OK)
            {
                Settings.Username = form.Username;

                bool success = await DiagnosticsSendInfo(str.ToString(), form.Username, form.Message);
                if (success)
                {
                    MessageBox.Show("Success", "Report sent");
                    return true;
                }
                else
                {
                    MessageBox.Show("Error sending the report", "Report failure");
                }
            }

            return false;
        }

        private async void reportallFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(lstTonies.Items.Count == 0)
            {
                return;
            }

            StringBuilder str = new StringBuilder();

            str.AppendLine("Dumping " + lstTonies.Items.Count + " files");

            foreach (ListViewItem t in lstTonies.Items)
            {
                ListViewTag tag = t.Tag as ListViewTag;

                if ((tag.Info == null || tag.Info.Model == null) && tag.AudioId > 0x50000000)
                {
                    AddInfo(str, tag);
                }
            }

            ReportForm form = new ReportForm();

            if (form.ShowDialog() == DialogResult.OK)
            {
                Settings.Username = form.Username;

                bool success = await DiagnosticsSendInfo(str.ToString(), form.Username, form.Message);
                if (success)
                {
                    MessageBox.Show("Success", "Report sent");
                }
                else
                {
                    MessageBox.Show("Error sending the report", "Report failure");
                }
            }
        }

        private async void reportProxmarkAnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TagOperationDialog opDlg = new TagOperationDialog();

            opDlg.Show();
            Proxmark3.MeasurementResult result = Proxmark3.MeasureAntenna();

            opDlg.Close();

            if (result == null)
            {
                MessageBox.Show("Measurement failed.", "Failed");
                return;
            }

            string content = "";

            content += "HF:     " + result.vHF.ToString("0.00", CultureInfo.InvariantCulture) + " V" + Environment.NewLine;
            content += "LF125:  " + result.vLF125.ToString("0.00", CultureInfo.InvariantCulture) + " V" + Environment.NewLine;
            content += "LF134:  " + result.vLF134.ToString("0.00", CultureInfo.InvariantCulture) + " V" + Environment.NewLine;
            content += "LFfopt: " + (result.GetPeakFrequency() / 1000.0f).ToString("0.00", CultureInfo.InvariantCulture) + " kHz" + Environment.NewLine;
            content += "LFVopt: " + result.peakV.ToString("0.00", CultureInfo.InvariantCulture) + " V" + Environment.NewLine;

            ReportForm form = new ReportForm();

            if (form.ShowDialog() == DialogResult.OK)
            {
                Settings.Username = form.Username;

                bool success = await DiagnosticsSendInfo(content, form.Username, form.Message);
                if (success)
                {
                    MessageBox.Show("Success", "Report sent");
                }
                else
                {
                    MessageBox.Show("Error sending the report", "Report failure");
                }
            }
        }

        private async Task<bool> DiagnosticsSendInfo(string payload, string sender, string message)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                MultipartFormDataContent form = new MultipartFormDataContent();

                form.Add(new StringContent(sender), "sender");
                form.Add(new StringContent(payload), "payload");
                form.Add(new StringContent(message), "message");
                form.Add(new StringContent(GetVersion()), "version");

                HttpResponseMessage response = await httpClient.PostAsync("https://g3gg0.magiclantern.fm/diag.php", form);
                response.EnsureSuccessStatusCode();
                httpClient.Dispose();
                string sd = response.Content.ReadAsStringAsync().Result;

                LogWindow.Log(LogWindow.eLogLevel.Debug, sd);

                if(sd != "")
                {
                    return true;
                }
                return false;
            }
            catch (Exception ee)
            {
            }
            return false;
        }

        private void AddInfo(StringBuilder str, ListViewTag tag)
        {
            TonieTools.DumpInfo(str, TonieTools.eDumpFormat.FormatText, tag.FileName, TonieInfos);
        }

        private async void readContentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(Proxmark3 == null || AsyncTagActionThread != null)
            {
                return;
            }

            AsyncTagActionThread = new Thread(() =>
            {
                try
                {
                    byte[] data = Proxmark3.ReadMemory();

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
                catch(Exception ex)
                {
                }
                AsyncTagActionThread = null;
            });
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
                        //AsyncTagActionThread.Abort();
                        AsyncTagActionThread = null;
                        return;
                    }
                }
                Invoke(new Action(() => opDlg.Close()));
            });
        }

        private async void emulateTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Proxmark3 == null || AsyncTagActionThread != null)
            {
                return;
            }

            TagDumpDialog dlg = new TagDumpDialog(false);

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                byte[] data = Helpers.ConvertHexStringToByteArray(dlg.String);

                AsyncTagActionThread = new Thread(() => 
                {
                    try
                    {
                        Proxmark3.EmulateTag(data);
                    }
                    catch (Exception ex)
                    {
                    }
                    AsyncTagActionThread = null;
                });
                AsyncTagActionThread.Start();
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
                        //AsyncTagActionThread.Abort();
                        AsyncTagActionThread = null;
                        return;
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
            if(Settings.NfcEnabled)
            {
                if (Proxmark3 == null)
                {
                    Proxmark3 = new Proxmark3();
                    Proxmark3.UidFound += Proxmark3_UidFound;
                    Proxmark3.DeviceFound += Proxmark3_DeviceFound;
                    Proxmark3.FlashRequest += Proxmark3_FlashRequest;
                    Proxmark3.FlashResult += Proxmark3_FlashResult;
                    Proxmark3.Start();
                }
            }
            else
            {
                if(Proxmark3 != null)
                {
                    Proxmark3.Stop();
                    Proxmark3 = null;
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
                LogWindow.LogLevel = LogWindow.eLogLevel.Debug;
                Log.Show();
            }
            else
            {
                LogWindow.LogLevel = LogWindow.eLogLevel.Warning;
                Log.Hide();
            }
        }

        private void measureAnteannaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TagOperationDialog opDlg = new TagOperationDialog();

            opDlg.Show();
            Proxmark3.MeasurementResult result = Proxmark3.MeasureAntenna();

            opDlg.Close();

            if (result == null)
            {
                MessageBox.Show("Measurement failed.", "Failed");
                return;
            }

            PlotAntennaForm form = new PlotAntennaForm(result);

            form.ShowDialog();
        }

        private void flashFirmwareToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Title = "Please select the fullimage.elf you want to flash";
            dlg.Filter = "Firmware ELF files (*.elf)|*.elf|All files (*.*)|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                Proxmark3.EnterBootloader(dlg.FileName);
            }
        }

        private void flashBootloaderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Title = "Please select the bootrom.elf you want to flash";
            dlg.Filter = "Bootloader ELF files (*.elf)|*.elf|All files (*.*)|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                Proxmark3.EnterBootloader(dlg.FileName);
            }
        }

        private void consoleModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(!consoleModeToolStripMenuItem.Checked)
            {
                consoleModeToolStripMenuItem.Checked = true;
                Proxmark3.EnterConsole();
            }
            else
            {
                consoleModeToolStripMenuItem.Checked = false;
                Proxmark3.ExitConsole();
            }
        }
    }
}

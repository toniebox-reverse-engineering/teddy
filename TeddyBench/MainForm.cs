using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using TeddyBench.Properties;
using TonieFile;

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

        private string CurrentDirectory = null;
        private bool AutoOpenDrive = true;
        private Dictionary<ListViewTag, ListViewItem> RegisteredItems = new Dictionary<ListViewTag, ListViewItem>();
        private TextWriter ConsoleWriter = null;
        private static TonieData[] TonieInfos;
        private static Dictionary<string, string> CustomTonies = new Dictionary<string, string>();
        private ListViewItem LastSelectediItem = null;
        private string TitleString => "TeddyBench (beta) - " + Application.ProductVersion + (ThisAssembly.Git.IsDirty ? ",dirty" : "");


        public class TonieData
        {
            [JsonProperty("no")]
            public string SortNumber_;
            public string SortString
            {
                get
                {
                    string ret = "";

                    if (!string.IsNullOrEmpty(Language))
                    {
                        ret += Language;
                    }
                    if (!string.IsNullOrEmpty(SortNumber_) && SortNumber_ != "na")
                    {
                        ret += int.Parse(SortNumber_).ToString("0000");
                    }
                    return ret;
                }
            }
            [JsonProperty("model")]
            public string Model;
            [JsonProperty("audio_id")]
            public string[] AudioId_;
            [JsonIgnore]
            public long[] AudioIds
            {
                get
                {
                    List<long> ids = new List<long>();
                    foreach (var id in AudioId_)
                    {
                        if (id != "" && id != "na")
                        {
                            ids.Add(long.Parse(id));
                        }
                    }
                    return ids.ToArray();
                }
            }
            [JsonProperty("hash")]
            public string[] Hash;
            [JsonProperty("title")]
            public string Title;
            [JsonProperty("series")]
            public string Series;
            [JsonProperty("episodes")]
            public string Episodes;
            [JsonProperty("tracks")]
            public string[] Tracks;
            [JsonProperty("release")]
            public string Release;
            [JsonProperty("language")]
            public string Language;
            [JsonProperty("category")]
            public string Category;
            [JsonProperty("pic")]
            public string Pic;
        }

        public class ListViewTag
        {
            public string FileName;
            public FileInfo FileInfo;
            public string Hash;
            public TonieData Info;
            public string Uid;

            public ListViewTag(string filename)
            {
                FileName = filename;

                FileInfo = new FileInfo(FileName);
                Uid = ReverseUid(FileInfo.DirectoryName + FileInfo.Name);
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
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://gt-blog.de/JSON/tonies.json?source=TeddyBench");
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
                    TonieInfos = JsonConvert.DeserializeObject<TonieData[]>(File.ReadAllText("tonies.json"));
                }
                catch (Exception e)
                {
                    TonieInfos = new TonieData[0];
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

            listView1.LargeImageList = new ImageList();
            listView1.LargeImageList.ImageSize = new Size(128, 128);
            listView1.LargeImageList.ColorDepth = ColorDepth.Depth32Bit;
            listView1.LargeImageList.Images.Add("unknown", ResizeImage(Resources.unknown, 128, 128));
            listView1.LargeImageList.Images.Add("custom", ResizeImage(Resources.unknown, 128, 128));
            listView1.ListViewItemSorter = new ListViewItemComparer(2);
            cmbSorting.SelectedIndex = 2;
            Text = TitleString;

            LoadJson();
            StartThreads();

            AllowDrop = true;
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

        protected override void OnClosing(CancelEventArgs e)
        {
            StopThreads();
            base.OnClosing(e);
        }

        private void StopThreads()
        {
            if (ScanCardThread != null)
            {
                ScanCardThreadStop = true;
                ScanCardThread.Join(100);
                ScanCardThread.Abort();
                ScanCardThread = null;
            }
            StopAnalyzeThread();
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
                                if (drive.RootDirectory.GetDirectories().Where(d => d.Name == "CONTENT").Count() == 1)
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
                Enabled = true;
                txtLog.Visible = false;
                grpCardContent.Visible = true;
                listView1.Items.Clear();
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
                            item.Group = listView1.Groups[3];

                            listView1.Items.Add(item);

                            RegisteredItems.Add(item.Tag as ListViewTag, item);
                        }
                    }
                }
                catch (Exception ex)
                {
                }
                listView1.Sort();
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
                    foreach (var entry in RegisteredItems)
                    {
                        ListViewTag tag = entry.Key;

                        if (entry.Value.ImageKey == "unknown")
                        {
                            try
                            {
                                TonieAudio dumpFile = TonieAudio.FromFile(tag.FileName, false);
                                string image = "";
                                string hash = BitConverter.ToString(dumpFile.Header.Hash).Replace("-", "");
                                var found = TonieInfos.Where(t => t.Hash.Where(h => h == hash).Count() > 0);
                                string tonieName = "[" + tag.Uid + "]" + Environment.NewLine + "(unknown: " + dumpFile.Header.AudioId.ToString("X8") + ")";

                                if (found.Count() > 0)
                                {
                                    var info = found.First();
                                    tag.Info = info;
                                    tonieName = info.Title;

                                    if (!string.IsNullOrEmpty(info.Model))
                                    {
                                        tonieName = info.Model + " - " + tonieName;
                                    }
                                    if (!string.IsNullOrEmpty(info.Pic) && !listView1.LargeImageList.Images.ContainsKey(hash))
                                    {
                                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(info.Pic);
                                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                                        Image img = ResizeImage(Image.FromStream(response.GetResponseStream()), 128, 128);

                                        this.BeginInvoke(new Action(() =>
                                        {
                                            listView1.LargeImageList.Images.Add(hash, img);
                                        }));
                                    }
                                    image = hash;
                                }
                                else
                                {
                                    tag.Info = new TonieData();

                                    if (CustomTonies.ContainsKey(hash))
                                    {
                                        tonieName = CustomTonies[hash];
                                    }
                                    image = "custom";
                                }

                                tag.Hash = hash;

                                this.BeginInvoke(new Action(() =>
                                {
                                    entry.Value.Text = tonieName;
                                    entry.Value.ImageKey = image;
                                    entry.Value.ToolTipText =
                                    "File:     " + tag.FileName + Environment.NewLine +
                                    "UID:      " + tag.Uid + Environment.NewLine +
                                    "Date:     " + tag.FileInfo.CreationTime + Environment.NewLine +
                                    "AudioID:  " + dumpFile.Header.AudioId + Environment.NewLine +
                                    "Chapters: " + dumpFile.Header.AudioChapters.Length + Environment.NewLine
                                    ;
                                }));
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                    }
                }
            }
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
            AskUIDForm ask = new AskUIDForm();

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
            Enabled = false;
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
            Enabled = false;
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
                    uint id = (uint)DateTimeOffset.Now.ToUnixTimeSeconds();
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
            LogThread.Abort();
        }

        public static string ReverseUid(string uid)
        {
            List<string> groups = (from Match m in Regex.Matches(uid, @"[A-F0-9]{2}") select m.Value).ToList();
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
            bool deleteAll = false;

            StopAnalyzeThread();

            if (listView1.SelectedItems.Count > 1)
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

            foreach (ListViewItem item in listView1.SelectedItems)
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

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                ListViewItem item = listView1.SelectedItems[0];

                ListViewTag tag = item.Tag as ListViewTag;
                var fi = new FileInfo(tag.FileName);
                string oldUid = ReverseUid(fi.DirectoryName + fi.Name);

                AskUIDForm dlg = new AskUIDForm();
                dlg.Uid = oldUid;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    string uid = dlg.Uid;
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
                        DirectoryInfo oldDir = fi.Directory;
                        fi.MoveTo(newFile);
                        oldDir.Delete();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to write file '" + newFile + "'");
                        return;
                    }

                    RefreshCardContent();
                }
            }
        }

        private void listView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.F2 && listView1.SelectedItems.Count > 0)
            {
                if (listView1.SelectedItems[0].ImageKey == "custom")
                {
                    listView1.SelectedItems[0].BeginEdit();
                }
            }
        }

        private void listView1_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                if (listView1.SelectedItems[0] == LastSelectediItem)
                {
                    if (listView1.SelectedItems[0].ImageKey == "custom")
                    {
                        LastSelectediItem.BeginEdit();
                    }
                }
                LastSelectediItem = listView1.SelectedItems[0];
            }
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected)
            {
                LastSelectediItem = null;
            }
        }

        private void listView1_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            ListViewItem item = listView1.Items[e.Item];
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

        private void cmbSorting_SelectedIndexChanged(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.Items)
            {
                if (cmbSorting.SelectedIndex == 3)
                {
                    switch (item.ImageKey)
                    {
                        case "unknown":
                            item.Group = listView1.Groups[0];
                            break;
                        case "custom":
                            item.Group = listView1.Groups[1];
                            break;
                        default:
                            item.Group = listView1.Groups[2];
                            break;
                    }
                }
                else
                {
                    item.Group = listView1.Groups[3];
                }
            }
            (listView1.ListViewItemSorter as ListViewItemComparer).Characteristic = cmbSorting.SelectedIndex;
            listView1.Sort();
        }
    }
}

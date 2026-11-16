using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TonieFile;

namespace TeddyBench.Core
{
    /// <summary>An operation on the card failed; <see cref="Exception.Message"/> is meant for the user.</summary>
    public class CardOperationException : Exception
    {
        public string Title { get; }

        public CardOperationException(string message, string title = null, Exception inner = null) : base(message, inner)
        {
            Title = title;
        }
    }

    public enum ReassignResult
    {
        /// <summary>The new UID maps to the same file, nothing was done.</summary>
        Unchanged,
        /// <summary>Moved, the old (now empty) directory was removed.</summary>
        Moved,
        /// <summary>Moved, but the old directory was kept because it is not empty.</summary>
        MovedOldDirectoryKept
    }

    /// <summary>
    /// File operations on an opened Toniebox CONTENT directory (or any directory with exported tonies).
    /// No UI: errors are reported as <see cref="CardOperationException"/>.
    /// </summary>
    public class CardContent
    {
        /// <summary>Exported tonie files as written by "export": "12345678 - 1A2B3C4D - title" or "12-3456 - ...".</summary>
        private static readonly Regex ExportedFilePattern = new Regex("(?<prod>[0-9]{8}|[0-9]{2}-[0-9]{4}) - [0-9A-F]{8} - (?<name>.*)");

        public string RootDirectory { get; }

        public CardContent(string rootDirectory)
        {
            RootDirectory = rootDirectory;
        }

        /// <summary>Lists tonie files: CONTENT/XXXXXXXX/*0304E0 and exported files directly in the root.</summary>
        public List<CardEntry> Scan()
        {
            var entries = new List<CardEntry>();

            foreach (var dir in new DirectoryInfo(RootDirectory).GetDirectories())
            {
                /* case-insensitive: vfat may be mounted with lower case short names on Linux */
                var file = dir.GetFiles().FirstOrDefault(f => f.Name.EndsWith("0304E0", StringComparison.OrdinalIgnoreCase));

                if (file != null)
                {
                    entries.Add(new CardEntry(file.FullName));
                }
            }

            foreach (var file in new DirectoryInfo(RootDirectory).GetFiles())
            {
                var match = ExportedFilePattern.Match(file.Name);
                if (match.Success)
                {
                    var entry = new CardEntry(file.FullName);
                    entry.InitialText = match.Groups["prod"].Value + " - " + match.Groups["name"].Value;
                    entries.Add(entry);
                }
            }

            return entries;
        }

        /// <summary>CONTENT/XXXXXXXX/YYYYYYYY path for a tag UID ("E0040350...").</summary>
        public string GetTonieFilePath(string uid)
        {
            string reversed = TonieTools.ReverseUid(uid);
            if (reversed.Length != 16)
            {
                throw new ArgumentException("UID must have 8 bytes: '" + uid + "'", nameof(uid));
            }
            return Path.Combine(RootDirectory, reversed.Substring(0, 8), reversed.Substring(8, 8));
        }

        /// <summary>Writes a tonie file for the tag UID, creating its directory.</summary>
        public string WriteTonieFile(string uid, byte[] content)
        {
            string newFile = GetTonieFilePath(uid);
            string newDir = Path.GetDirectoryName(newFile);

            try
            {
                Directory.CreateDirectory(newDir);
            }
            catch (Exception ex)
            {
                throw new CardOperationException("Failed to create directory '" + newDir + "'", null, ex);
            }

            try
            {
                File.WriteAllBytes(newFile, content);
            }
            catch (Exception ex)
            {
                throw new CardOperationException("Failed to write file '" + newFile + "'", null, ex);
            }

            return newFile;
        }

        /// <summary>Copies an existing tonie file to the slot of the tag UID.</summary>
        public string CopyTonieFile(string uid, string sourceFile)
        {
            return WriteTonieFile(uid, File.ReadAllBytes(sourceFile));
        }

        /// <summary>Audio ID for new custom tonies: below 0x50000000 so they are recognized as custom.</summary>
        public static uint NewCustomAudioId()
        {
            return (uint)(DateTimeOffset.Now.ToUnixTimeSeconds() - 0x50000000);
        }

        /// <summary>Encodes MP3/Ogg files into a tonie file. <paramref name="audioId"/> uint.MaxValue picks a new custom ID.</summary>
        public static TonieAudio Encode(string[] files, uint audioId = uint.MaxValue, TonieAudio.EncodeCallback callback = null)
        {
            if (audioId == uint.MaxValue)
            {
                audioId = NewCustomAudioId();
            }

            try
            {
                return new TonieAudio(files, audioId, cbr: callback);
            }
            catch (Exception ex)
            {
                throw new CardOperationException("Failed to encode file. Sorry. Most likely a MP3 was corrupted." + Environment.NewLine + "(" + ex.Message + ")", null, ex);
            }
        }

        /// <summary>Moves a tonie file to the slot of another tag UID.</summary>
        public ReassignResult Reassign(CardEntry entry, string newUid)
        {
            const string title = "Re-assigning UID failed";
            var fi = new FileInfo(entry.FileName);
            string newFile = GetTonieFilePath(newUid);
            string newDir = Path.GetDirectoryName(newFile);

            if (new FileInfo(newFile).FullName == fi.FullName)
            {
                return ReassignResult.Unchanged;
            }

            if (File.Exists(newFile))
            {
                throw new CardOperationException("Failed to assign UID '" + newUid + "' as this file already exists", title);
            }

            try
            {
                Directory.CreateDirectory(newDir);
            }
            catch (Exception ex)
            {
                throw new CardOperationException("Failed to create directory '" + newDir + "'", title, ex);
            }

            try
            {
                DirectoryInfo oldDir = fi.Directory;
                fi.MoveTo(newFile);
                if (oldDir.EnumerateFileSystemInfos().Any())
                {
                    return ReassignResult.MovedOldDirectoryKept;
                }
                oldDir.Delete();
                return ReassignResult.Moved;
            }
            catch (Exception ex)
            {
                throw new CardOperationException("Failed to write file '" + newFile + "'", title, ex);
            }
        }

        /// <summary>Deletes the tonie file and its (then empty) directory.</summary>
        public static void Delete(CardEntry entry)
        {
            var info = new FileInfo(entry.FileName);
            try
            {
                info.Delete();
                info.Directory.Delete();
            }
            catch (Exception ex)
            {
                throw new CardOperationException("Failed to delete file/directory '" + entry.FileName + "'", null, ex);
            }
        }

        /// <summary>File name used when exporting a tonie file: "model - AUDIOID - title".</summary>
        public static string GetExportFileName(CardEntry entry)
        {
            return entry.Info?.Model + " - " + entry.AudioId.ToString("X8") + " - " + TonieTools.RemoveInvalidChars(entry.Info?.Title ?? "").Trim();
        }

        /// <summary>Copies the tonie file as-is to <paramref name="outputDirectory"/> (does not overwrite).</summary>
        public static string Export(CardEntry entry, string outputDirectory)
        {
            string destName = Path.Combine(outputDirectory, GetExportFileName(entry));
            try
            {
                File.Copy(entry.FileName, destName, false);
            }
            catch (Exception ex)
            {
                throw new CardOperationException("Failed to save file '" + entry.FileName + "': " + ex.Message, null, ex);
            }
            return destName;
        }

        /// <summary>
        /// Writes the audio as one .ogg file per chapter to <paramref name="outputDirectory"/>, tagged with the
        /// tonies.json information if known. With <paramref name="createDirectory"/> a sub directory
        /// per tonie is created.
        /// </summary>
        public static void SaveAudio(CardEntry entry, string outputDirectory, bool createDirectory, TonieDatabase database, string version)
        {
            string file = entry.FileName;
            TonieAudio dump;

            try
            {
                dump = TonieAudio.FromFile(file);
            }
            catch (Exception ex)
            {
                throw new CardOperationException("Failed to save file '" + file + "'", null, ex);
            }

            string[] titles = null;
            var tags = new List<string>();
            tags.Add("TeddyVersion=" + version);
            tags.Add("TeddyFile=" + file);

            string hashString = BitConverter.ToString(dump.Header.Hash).Replace("-", "");
            TonieTools.TonieData info = database?.Find(hashString);

            if (info != null)
            {
                titles = info.Tracks;
                tags.Add("ALBUM=" + info.Title);
                tags.Add("ARTIST=" + info.Series);
                tags.Add("LANGUAGE=" + info.Language);
            }
            tags.Add("HASH=" + hashString);

            string inFile = new FileInfo(file).Name;
            string outDirectory = !string.IsNullOrEmpty(outputDirectory) ? outputDirectory : new FileInfo(file).DirectoryName;

            if (createDirectory)
            {
                if (info == null)
                {
                    outDirectory = Path.Combine(outDirectory, dump.Header.AudioId.ToString("X8") + " - " + entry.Uid);
                }
                else
                {
                    outDirectory = Path.Combine(outDirectory, info.Model + " - " + dump.Header.AudioId.ToString("X8") + " - " + TonieTools.RemoveInvalidChars(info.Title).Trim());
                }
                Directory.CreateDirectory(outDirectory);
            }

            if (!Directory.Exists(outDirectory))
            {
                throw new CardOperationException("Output directory '" + outDirectory + "' does not exist");
            }

            try
            {
                dump.DumpAudioFiles(outDirectory, inFile + "-" + dump.Header.AudioId.ToString("X8"), false, tags.ToArray(), titles);
            }
            catch (Exception ex)
            {
                throw new CardOperationException("Failed to write .ogg/.cue for '" + file + "': " + ex.Message, null, ex);
            }
        }
    }
}

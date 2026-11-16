using System;
using System.Collections.Generic;
using System.IO;
using TonieFile;

namespace TeddyBench.Core
{
    public enum TonieKind
    {
        /// <summary>Not in tonies.json and not recognizable as custom.</summary>
        Unknown,
        /// <summary>Custom tonie: named by the user, or audio ID below 0x50000000.</summary>
        Custom,
        /// <summary>Official tonie found in tonies.json.</summary>
        Known
    }

    /// <summary>How a tonie file should be presented.</summary>
    public class TonieResolution
    {
        public TonieKind Kind;
        /// <summary>Text for the list ("[live] " prefixed if live).</summary>
        public string DisplayText;
        public string ToolTip;
        /// <summary>Picture URL from tonies.json (Known only).</summary>
        public string PicUrl;
        /// <summary>Image key: "unknown", "custom" or the audio hash for known tonies.</summary>
        public string ImageKey;
        /// <summary>The entry was recognized (known or custom), the view should refresh it even if the text looks unchanged.</summary>
        public bool Recognized;
    }

    /// <summary>Identifies tonie files using the database. Fills Hash/Info/AudioId of the <see cref="CardEntry"/>.</summary>
    public static class TonieResolver
    {
        public const string ImageUnknown = "unknown";
        public const string ImageCustom = "custom";

        public static TonieResolution Resolve(CardEntry entry, TonieAudio header, TonieDatabase database, bool live)
        {
            var result = new TonieResolution();
            string hash = BitConverter.ToString(header.Header.Hash).Replace("-", "");
            string tonieName = "[" + entry.Uid + "]" + Environment.NewLine + "(unknown: " + header.Header.AudioId.ToString("X8") + ")";
            string customName = database.GetCustomName(hash);
            TonieTools.TonieData info = database.Find(hash);

            if (info != null)
            {
                Logger.Log(Logger.eLogLevel.DebugVerbose, "     Found");
                entry.Info = info;
                tonieName = info.Title;

                if (!string.IsNullOrEmpty(info.Model))
                {
                    tonieName = info.Model + " - " + tonieName;
                }
                if (customName != null)
                {
                    Logger.Log(Logger.eLogLevel.DebugVerbose, "     known tonie, overriding name");
                    tonieName = customName;
                }
                result.Kind = TonieKind.Known;
                result.PicUrl = info.Pic;
                result.ImageKey = hash;
                result.Recognized = true;
            }
            else
            {
                entry.Info = new TonieTools.TonieData();
                result.ImageKey = ImageUnknown;
                result.Kind = TonieKind.Unknown;

                if (customName != null)
                {
                    Logger.Log(Logger.eLogLevel.DebugVerbose, "     known custom tonie");
                    tonieName = customName;
                    entry.Info.Title = tonieName;
                    result.Kind = TonieKind.Custom;
                    result.ImageKey = ImageCustom;
                    result.Recognized = true;
                }
                else if (header.Header.AudioId < 0x50000000)
                {
                    Logger.Log(Logger.eLogLevel.DebugVerbose, "     unknown custom tonie");
                    tonieName = "Unnamed Teddy - " + tonieName;
                    entry.Info.Title = "Unnamed Teddy";
                    result.Kind = TonieKind.Custom;
                    result.ImageKey = ImageCustom;
                    result.Recognized = true;
                }
                else
                {
                    Logger.Log(Logger.eLogLevel.DebugVerbose, "     Not found -> unknown");
                }
            }

            entry.Hash = hash;
            entry.AudioId = header.Header.AudioId;

            result.DisplayText = (live ? "[live] " : "") + tonieName;
            result.ToolTip =
                "File:     " + entry.FileName + Environment.NewLine +
                "Name:     " + entry.Info.Title + Environment.NewLine +
                "UID:      " + entry.Uid + Environment.NewLine +
                "Date:     " + entry.FileInfo.CreationTime + Environment.NewLine +
                "AudioID:  0x" + entry.AudioId.ToString("X8") + Environment.NewLine +
                "Chapters: " + header.Header.AudioChapters.Length + Environment.NewLine;

            return result;
        }
    }

    /// <summary>Caches parsed tonie headers per file until the file changes.</summary>
    public class TonieFileCache
    {
        private readonly Dictionary<string, Tuple<TonieAudio, DateTime>> Cache = new Dictionary<string, Tuple<TonieAudio, DateTime>>();

        /// <summary>Header only (no audio data) of a tonie file.</summary>
        public TonieAudio GetHeader(string fileName)
        {
            DateTime lastWrite = new FileInfo(fileName).LastWriteTime;

            lock (Cache)
            {
                if (Cache.TryGetValue(fileName, out var cached) && cached.Item2 == lastWrite)
                {
                    return cached.Item1;
                }
            }

            TonieAudio file = TonieAudio.FromFile(fileName, false);

            lock (Cache)
            {
                Cache[fileName] = new Tuple<TonieAudio, DateTime>(file, lastWrite);
            }
            return file;
        }
    }
}

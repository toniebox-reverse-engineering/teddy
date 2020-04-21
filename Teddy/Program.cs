/* Copyright (c) 2020 g3gg0.de

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using Mono.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TonieFile;

namespace Teddy
{
    class Program
    {
        public static int Verbosity = 0;

        private static TonieData[] TonieInfos = new TonieData[0];
        private enum eDumpFormat
        {
            FormatText,
            FormatCSV,
            FormatJSON
        };

        private class TonieData
        {
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
            [JsonProperty("title")]
            public string Title;
            [JsonProperty("tracks")]
            public string[] Tracks;
            [JsonProperty("model")]
            public string Model;
            [JsonProperty("category")]
            public string Category;
            [JsonProperty("pic")]
            public string Pic;
            [JsonProperty("pic_crop")]
            public string PicCrop;
        }

        public static void LoadJson(string path)
        {
            try
            {
                if (path.StartsWith("http"))
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(path);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    TextReader reader = new StreamReader(response.GetResponseStream());

                    TonieInfos = JsonConvert.DeserializeObject<TonieData[]>(reader.ReadToEnd());
                }
                else if (File.Exists(path))
                {
                    TonieInfos = JsonConvert.DeserializeObject<TonieData[]>(File.ReadAllText(path));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to load JSON:");
                Console.WriteLine(e.Message);
                return;
            }
        }

        public static string FormatGranuleCue(ulong granule)
        {
            ulong time = 75 * granule / 48000;
            ulong frames = time % 75;
            ulong seconds = (time / 75) % 60;
            ulong minutes = (time / 75 / 60);

            return minutes.ToString("00") + ":" + seconds.ToString("00") + ":" + frames.ToString("00");
        }

        public static string BuildCueSheet(TonieAudio file)
        {
            ulong[] positions = file.ParsePositions();
            string[] titles = null;
            string title = null;
            StringBuilder cue = new StringBuilder();

            var found = TonieInfos.Where(t => t.AudioIds.Contains(file.Header.AudioId));
            if (found.Count() > 0)
            {
                var info = found.First();

                title = info.Title;
                titles = info.Tracks;
            }

            if (title != null)
            {
                cue.Append("TITLE \"").Append(title).AppendLine("\"");
            }

            cue.Append("FILE ").Append(file.FilenameShort).Append(".ogg").AppendLine(" MP3");

            for (int chapter = 0; chapter < file.Header.AudioChapters.Length; chapter++)
            {
                /* seems the chapter is the page, but off by one */
                uint offset = file.Header.AudioChapters[chapter];
                ulong granule = file.GetGranuleByPage((offset > 0) ? (offset - 1) : 0);

                cue.Append("  TRACK ").Append(chapter + 1).AppendLine(" AUDIO");
                if (titles != null)
                {
                    cue.Append("    TITLE \"").Append(titles[chapter]).AppendLine("\"");
                }
                string time = FormatGranuleCue(granule);
                cue.Append("    INDEX 1 ").AppendLine(time);
            }

            return cue.ToString();
        }

        public static string RemoveInvalidChars(string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

        static void Main(string[] args)
        {
            bool showLicense = false;
            bool showHelp = false;
            eDumpFormat dumpFormat = eDumpFormat.FormatText;
            bool useVbr = false;
            bool reallyRename = false;
            string mode = "";
            string outputLocation = "";
            string prefixLocation = null;
            string audioId = "";
            string jsonFile = "tonies.json";

            int bitRate = 96;

            var p = new OptionSet {
                { "m|mode=",    "Operating mode: info, decode, encode, rename",     (string n) => mode = n },
                { "o|output=",  "Location where to write the file(s) to",           (string r) => outputLocation = r },
                { "p|prefix=",  "Location where to find prefix files",              (string r) => prefixLocation = r },
                { "i|id=",      "Set AudioID for encoding (default: current time)", (string r) => audioId = r },
                { "b|bitrate=", "Set opus bit rate (default: "+bitRate+" kbps)",    (int r) => bitRate = r },
                { "vbr",        "Use VBR encoding",                                 r => useVbr = true },
                { "j|json=",    "Set JSON file/URL with details about tonies",      (string r) => jsonFile = r },
                { "f|format=",  "Output details as: csv, json or text",             v => { switch(v) { case "csv":  dumpFormat = eDumpFormat.FormatCSV; break; case "json":  dumpFormat = eDumpFormat.FormatJSON; break; case "text":  dumpFormat = eDumpFormat.FormatText; break; } } },
                { "y",          "really rename files",                              v => { reallyRename = true; } },
                { "v",          "increase debug message verbosity",                 v => { if (v != null) ++Verbosity; } },
                { "h|help",     "show this message and exit",                       h => showHelp = true },
                { "license",    "show licenses and disclaimer",                     h => showLicense = true },
            };

            List<string> extra;
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("Teddy.exe: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `Teddy.exe --help' for more information.");
                return;
            }

            if (showLicense)
            {
                ShowLicense(p);
                return;
            }

            if (showHelp)
            {
                ShowHelp(p);
                return;
            }

            LoadJson(jsonFile);

            switch (mode)
            {
                default:
                    ShowHelp(p);
                    return;

                case "rename":
                    {
                        Dictionary<string, string> renameList = new Dictionary<string, string>();
                        List<string> files = new List<string>();

                        if (extra.Count < 2 || (!Directory.Exists(extra[0]) && !Directory.Exists(extra[1])))
                        {
                            Console.WriteLine("Error: You must specify a source and a destination directory");
                            return;
                        }

                        FindTonieFiles(files, extra[0]);

                        Console.WriteLine(" Scan files...");
                        foreach (string file in files.ToArray())
                        {
                            try
                            {
                                TonieAudio dumpFile = TonieAudio.FromFile(file);

                                var found = TonieInfos.Where(t => t.AudioIds.Contains(dumpFile.Header.AudioId));
                                string destFileName = "(unknown)";

                                if (found.Count() > 0)
                                {
                                    var info = found.First();
                                    destFileName = info.Title;
                                    if (!string.IsNullOrEmpty(info.Model))
                                    {
                                        string destName = extra[1] + Path.DirectorySeparatorChar + info.Model + " - " + dumpFile.Header.AudioId.ToString("X8") + " - " + RemoveInvalidChars(destFileName).Trim();
                                        renameList.Add(file, destName);
                                    }
                                }

                                Console.WriteLine("  '" + file + "' -> '" + destFileName + "'");
                            }
                            catch (FileNotFoundException ex)
                            {
                                Console.WriteLine("File not found: " + file);
                            }
                            catch (InvalidDataException ex)
                            {
                                Console.WriteLine("  '" + file + "' -> (corrupt)");
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine();
                                Console.WriteLine("[ERROR] Failed to process '" + file + "'");
                                Console.WriteLine("   Exception:  " + e.GetType());
                                Console.WriteLine("   Message:    " + e.Message);
                                Console.WriteLine("   Stacktrace: " + e.StackTrace);
                            }
                        }

                        Console.WriteLine("");
                        Console.WriteLine(" Rename files...");
                        int renamed = 0;
                        foreach (var key in renameList.Keys)
                        {
                            string dest = renameList[key];

                            Console.WriteLine("  Rename '" + key + "' -> '" + dest + "'");
                            if (File.Exists(dest))
                            {
                                if (FileChecksum(dest) == FileChecksum(key))
                                {
                                    Console.WriteLine("    Skipped, destination file content matches");
                                }
                                else
                                {
                                    Console.WriteLine("    Skipped, destination file already exists but has different content");
                                }
                                continue;
                            }

                            if (reallyRename)
                            {
                                var di = new FileInfo(key).Directory;

                                try
                                {
                                    File.Move(key, dest);
                                    renamed++;
                                    if (di.GetFiles().Length == 0)
                                    {
                                        di.Delete();
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("[ERROR] Failed to rename '" + key + "'");
                                }
                            }
                        }
                        Console.WriteLine("");
                        Console.WriteLine(" Renamed " + renamed + " files");

                        break;
                    }


                case "info":
                    {
                        if (extra.Count < 1 || (!Directory.Exists(extra[0]) && !File.Exists(extra[0])))
                        {
                            Console.WriteLine("Error: You must specify a file");
                            return;
                        }

                        switch (dumpFormat)
                        {
                            case eDumpFormat.FormatCSV:
                                if (extra.Count > 1 || Directory.Exists(extra[0]))
                                {
                                    Console.WriteLine("UID;AudioID;AudioDate;HeaderLength;HeaderOK;Padding;AudioLength;AudioLengthCheck;AudioHash;AudioHashCheck;Chapters;Segments;MinSegmentsPerPage;MaxSegmentsPerPage;SegLengthSum;HighestGranule;Time;MinGranules;MaxGranules;MinTime;MaxTime;");
                                }
                                break;
                            case eDumpFormat.FormatJSON:
                                Console.WriteLine("[");
                                break;
                            case eDumpFormat.FormatText:
                                Console.WriteLine("[Mode: dump information]");
                                break;
                        }

                        List<string> files = new List<string>();

                        foreach (string file in extra)
                        {
                            if (Directory.Exists(file))
                            {
                                FindTonieFiles(files, file);
                            }
                            else
                            {
                                if (!File.Exists(file))
                                {
                                    Console.WriteLine("Error: file '" + file + "' does not exist");
                                    return;
                                }
                                files.Add(file);
                            }
                        }

                        bool first = true;
                        foreach (string file in files.ToArray())
                        {
                            try
                            {
                                TonieAudio dumpFile = TonieAudio.FromFile(file);

                                dumpFile.CalculateStatistics(out long segCount, out long segLength, out int minSegs, out int maxSegs, out ulong minGranule, out ulong maxGranule, out ulong highestGranule);
                                string uidrev = new FileInfo(file).Directory.Name + new FileInfo(file).Name;
                                List<string> groups = (from Match m in Regex.Matches(uidrev, @"[A-F0-9]{2}") select m.Value).ToList();
                                groups.Reverse();
                                string uid = string.Join("", groups.ToArray());
                                var date = DateTimeOffset.FromUnixTimeSeconds(dumpFile.Header.AudioId);

                                switch (dumpFormat)
                                {
                                    case eDumpFormat.FormatCSV:
                                        {

                                            Console.Write(uid + ";");
                                            Console.Write(dumpFile.Header.AudioId.ToString("X8") + ";");
                                            Console.Write(date.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss") + ";");
                                            Console.Write(dumpFile.HeaderLength + ";");
                                            Console.Write(((dumpFile.HeaderLength != 0xFFC) ? "[WARNING: EXTRA DATA]" : "[OK]") + ";");
                                            Console.Write(dumpFile.Header.Padding.Length + ";");
                                            Console.Write(dumpFile.Header.AudioLength + ";");
                                            Console.Write((dumpFile.Header.AudioLength == dumpFile.Audio.Length ? "[OK]" : "[INCORRECT]") + ";");
                                            Console.Write(BitConverter.ToString(dumpFile.Header.Hash).Replace("-", "") + ";");
                                            Console.Write((dumpFile.HashCorrect ? "[OK]" : "[INCORRECT]") + ";");
                                            foreach (var offset in dumpFile.Header.AudioChapters)
                                            {
                                                Console.Write(offset + " ");
                                            }
                                            Console.Write(";");

                                            Console.Write(segCount + ";");
                                            Console.Write(minSegs + ";");
                                            Console.Write(maxSegs + ";");
                                            Console.Write(segLength + ";");
                                            Console.Write(highestGranule + ";");
                                            Console.Write(TonieAudio.FormatGranule(highestGranule) + ";");
                                            Console.Write(minGranule + ";");
                                            Console.Write(maxGranule + ";");
                                            Console.Write((1000 * minGranule / 48000.0f) + ";");
                                            Console.Write((1000 * maxGranule / 48000.0f) + ";");
                                            Console.WriteLine();
                                            break;
                                        }

                                    case eDumpFormat.FormatText:
                                        {
                                            Console.WriteLine("Dump of " + dumpFile.Filename + " (UID " + uid + "):");

                                            Console.WriteLine("  Header: AudioID     0x" + dumpFile.Header.AudioId.ToString("X8") + " (" + date.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss") + ")");

                                            string[] titles = null;
                                            var found = TonieInfos.Where(t => t.AudioIds.Contains(dumpFile.Header.AudioId));
                                            if (found.Count() > 0)
                                            {
                                                var info = found.First();
                                                titles = info.Tracks;

                                                Console.WriteLine("  Header: JSON Name   '" + info.Title + "'");
                                            }
                                            Console.WriteLine("  Header: Length      0x" + dumpFile.HeaderLength.ToString("X8") + " " + ((dumpFile.HeaderLength != 0xFFC) ? " [WARNING: EXTRA DATA]" : "[OK]"));
                                            Console.WriteLine("  Header: Padding     0x" + dumpFile.Header.Padding.Length.ToString("X8"));
                                            Console.WriteLine("  Header: AudioLen    0x" + dumpFile.Header.AudioLength.ToString("X8") + " " + (dumpFile.Header.AudioLength == dumpFile.Audio.Length ? "[OK]" : "[INCORRECT]"));
                                            Console.WriteLine("  Header: Checksum    " + BitConverter.ToString(dumpFile.Header.Hash).Replace("-", "") + " " + (dumpFile.HashCorrect ? "[OK]" : "[INCORRECT]"));
                                            Console.WriteLine("  Header: Chapters    ");

                                            TimeSpan prevTime = new TimeSpan();
                                            for (int track = 1; track <= dumpFile.Header.AudioChapters.Length; track++)
                                            {
                                                uint off = dumpFile.GetHighestPage();

                                                if (track < dumpFile.Header.AudioChapters.Length)
                                                {
                                                    off = dumpFile.Header.AudioChapters[track];
                                                }
                                                ulong granule = dumpFile.GetGranuleByPage(off);
                                                string lengthString = "@" + off;

                                                if (granule != ulong.MaxValue)
                                                {
                                                    TimeSpan trackOffset = TimeSpan.FromSeconds(granule / 48000.0f);
                                                    lengthString = (trackOffset - prevTime).ToString(@"mm\:ss\.ff");
                                                    prevTime = trackOffset;
                                                }

                                                string title = "";

                                                if (titles != null && track - 1 < titles.Length)
                                                {
                                                    title = titles[track - 1];
                                                }
                                                Console.WriteLine("    Track #" + track.ToString("00") + "  " + lengthString + "  " + title);
                                            }

                                            Console.WriteLine("  Ogg: Segments       " + segCount + " (min: " + minSegs + " max: " + maxSegs + " per OggPage)");
                                            Console.WriteLine("  Ogg: net payload    " + segLength + " byte");
                                            Console.WriteLine("  Ogg: granules       total: " + highestGranule + " (" + TonieAudio.FormatGranule(highestGranule) + " hh:mm:ss.ff)");
                                            Console.WriteLine("  Ogg: granules/page  min: " + minGranule + " max: " + maxGranule + " (" + (1000 * minGranule / 48000.0f) + "ms - " + (1000 * maxGranule / 48000.0f) + "ms)");
                                            Console.WriteLine();
                                            break;
                                        }

                                    case eDumpFormat.FormatJSON:
                                        {
                                            if (!first)
                                            {
                                                Console.WriteLine(",");
                                            }
                                            Console.WriteLine("  {");
                                            Console.WriteLine("    \"uid\": \"" + uid + "\",");
                                            Console.WriteLine("    \"audio_id\": \"" + dumpFile.Header.AudioId.ToString("X8") + "\",");
                                            Console.WriteLine("    \"audio_date\": \"" + date.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss") + "\",");
                                            Console.WriteLine("    \"header_length\": " + dumpFile.HeaderLength + ",");
                                            Console.WriteLine("    \"header_ok\": \"" + ((dumpFile.HeaderLength != 0xFFC) ? "FALSE" : "TRUE") + "\",");
                                            Console.WriteLine("    \"padding\": " + dumpFile.Header.Padding.Length + ",");
                                            Console.WriteLine("    \"audio_length\": " + dumpFile.Header.AudioLength + ",");
                                            Console.WriteLine("    \"audio_length_check\": \"" + (dumpFile.Header.AudioLength == dumpFile.Audio.Length ? "TRUE" : "FALSE") + "\",");
                                            Console.WriteLine("    \"audio_hash\": \"" + BitConverter.ToString(dumpFile.Header.Hash).Replace("-", "") + "\",");
                                            Console.WriteLine("    \"audio_hash_check\": \"" + (dumpFile.HashCorrect ? "TRUE" : "FALSE") + "\",");
                                            Console.Write("    \"chapters\": [ 0");
                                            foreach (var offset in dumpFile.Header.AudioChapters)
                                            {
                                                Console.Write(", " + offset);
                                            }
                                            Console.WriteLine(" ],");
                                            Console.WriteLine("    \"segments\": " + segCount + ",");
                                            Console.WriteLine("    \"min_segments_per_page\": " + minSegs + ",");
                                            Console.WriteLine("    \"max_segments_per_page\": " + maxSegs + ",");
                                            Console.WriteLine("    \"segment_length_sum\": " + segLength + ",");
                                            Console.WriteLine("    \"highest_granule\": " + highestGranule + ",");
                                            Console.WriteLine("    \"time\": \"" + TonieAudio.FormatGranule(highestGranule) + "\",");
                                            Console.WriteLine("    \"min_granules\": " + minGranule + ",");
                                            Console.WriteLine("    \"max_granules\": " + maxGranule + ",");
                                            Console.WriteLine("    \"min_time\": " + (1000 * minGranule / 48000.0f) + ",");
                                            Console.WriteLine("    \"max_time\": " + (1000 * maxGranule / 48000.0f) + "");
                                            Console.WriteLine("  }");

                                            break;
                                        }
                                }
                            }
                            catch (FileNotFoundException ex)
                            {
                                Console.WriteLine("File not found: " + file);
                            }
                            catch (InvalidDataException ex)
                            {
                                Console.WriteLine("File corrupt: " + file);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine();
                                Console.WriteLine("[ERROR] Failed to process '" + file + "'");
                                Console.WriteLine("   Exception:  " + e.GetType());
                                Console.WriteLine("   Message:    " + e.Message);
                                Console.WriteLine("   Stacktrace: " + e.StackTrace);
                            }

                            if (first)
                            {
                                first = false;
                            }
                        }

                        switch (dumpFormat)
                        {
                            case eDumpFormat.FormatCSV:
                                break;
                            case eDumpFormat.FormatJSON:
                                Console.WriteLine("]");
                                break;
                            case eDumpFormat.FormatText:
                                break;
                        }
                        break;
                    }

                case "decode":
                    {
                        if (extra.Count < 1 || (!Directory.Exists(extra[0]) && !File.Exists(extra[0])))
                        {
                            Console.WriteLine("Error: You must specify a file");
                            return;
                        }

                        List<string> files = new List<string>();

                        foreach (string file in extra)
                        {
                            if (Directory.Exists(file))
                            {
                                FindTonieFiles(files, file);
                            }
                            else
                            {
                                if (!File.Exists(file))
                                {
                                    Console.WriteLine("Error: file '" + file + "' does not exist");
                                    return;
                                }
                                files.Add(file);
                            }
                        }

                        Console.WriteLine("[Mode: decode file]");

                        foreach (string file in files.ToArray())
                        {
                            try
                            {
                                Console.WriteLine("Dumping '" + file + "'");
                                TonieAudio dump2 = TonieAudio.FromFile(file);
                                string inFile = new FileInfo(file).Name;
                                string inDir = new FileInfo(file).DirectoryName;
                                string outDirectory = !string.IsNullOrEmpty(outputLocation) ? outputLocation : inDir;

                                if (!Directory.Exists(outDirectory))
                                {
                                    Console.WriteLine("Error: Output directory '" + outDirectory + "' does not exist");
                                    return;
                                }
                                string outFile = outDirectory + Path.DirectorySeparatorChar + inFile;

                                try
                                {
                                    File.WriteAllBytes(outFile + ".ogg", dump2.Audio);
                                    File.WriteAllText(outFile + ".cue", BuildCueSheet(dump2), Encoding.UTF8);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("[ERROR] Failed to write file '" + outFile + ".occ/.cue'");
                                    Console.WriteLine("   Message:    " + ex.Message);
                                }
                                Console.WriteLine("Written content to " + outFile + ".ogg/.cue");
                            }
                            catch (FileNotFoundException ex)
                            {
                                Console.WriteLine("File not found: " + file);
                            }
                            catch (InvalidDataException ex)
                            {
                                Console.WriteLine("File corrupt: " + file);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine();
                                Console.WriteLine("[ERROR] Failed to process '" + file + "'");
                                Console.WriteLine("   Exception:  " + e.GetType());
                                Console.WriteLine("   Message:    " + e.Message);
                                Console.WriteLine("   Stacktrace: " + e.StackTrace);
                            }
                        }
                        break;
                    }

                case "encode":
                    Console.WriteLine("[Mode: encode, " + bitRate + " kbps, " + (useVbr ? "VBR" : "CBR") + "]");

                    if (extra.Count < 1)
                    {
                        Console.WriteLine("Error: You must specify a directory or files to encode");
                        return;
                    }
                    /*
                    if ((bitRate % 24) != 0)
                    {
                        Console.WriteLine("Error: You must specify a multiple of 24 kbps, else block alignment in output file would produce incompatible files");
                        return;
                    }*/

                    uint id = (uint)DateTimeOffset.Now.ToUnixTimeSeconds();
                    if (audioId != "")
                    {
                        if (audioId.Trim().StartsWith("0x"))
                        {
                            if (!uint.TryParse(audioId.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out id))
                            {
                                Console.WriteLine("Error: You must specify the AudioID as hex value like 0x5E034216 or as decimal number like 1577271830");
                                return;
                            }
                        }
                        else
                        {
                            if (!uint.TryParse(audioId, System.Globalization.NumberStyles.Integer, null, out id))
                            {
                                Console.WriteLine("Error: You must specify the AudioID as hex value like 0x5E034216 or as decimal number like 1577271830");
                                return;
                            }
                        }
                    }

                    try
                    {
                        string outLocationEncode = ((!string.IsNullOrEmpty(outputLocation)) ? outputLocation : ".");
                        string outFile;

                        if (!Directory.Exists(outLocationEncode))
                        {
                            string baseDirOutFile = new FileInfo(outLocationEncode).DirectoryName;
                            if (!Directory.Exists(baseDirOutFile))
                            {
                                Console.WriteLine("Error: Specified output directory '" + outLocationEncode + "' does not exist and file '" + baseDirOutFile + "' not reachable.");
                                return;
                            }
                            outFile = outLocationEncode;
                        }
                        else
                        {
                            outFile = outLocationEncode + Path.DirectorySeparatorChar + "500304E0";
                        }

                        TonieAudio generated = new TonieAudio(extra.ToArray(), id, bitRate * 1000, useVbr, prefixLocation);
                        try
                        {
                            File.WriteAllBytes(outFile, generated.FileContent);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[ERROR] Failed to write file '" + outFile + "'");
                            Console.WriteLine("   Message:    " + ex.Message);
                        }

                        Console.WriteLine("");
                        Console.WriteLine("Written content to " + outFile);
                    }
                    catch (FileNotFoundException ex)
                    {
                        Console.WriteLine("[ERROR] Failed to process due to a missing file");
                        Console.WriteLine("   Message:    " + ex.Message);
                    }
                    catch (InvalidDataException ex)
                    {
                        Console.WriteLine("[ERROR] Failed to process due to an invalid file");
                        Console.WriteLine("   Message:    " + ex.Message);
                    }
                    catch (TonieAudio.EncodingException ex)
                    {
                        Console.WriteLine("[ERROR] Failed to encode audio");
                        Console.WriteLine("   Message:    " + ex.Message);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine();
                        Console.WriteLine("[ERROR] Failed to process");
                        Console.WriteLine("   Exception:  " + e.GetType());
                        Console.WriteLine("   Message:    " + e.Message);
                        Console.WriteLine("   Stacktrace: " + e.StackTrace);
                    }
                    break;
            }
        }

        private static string FileChecksum(string filename)
        {
            using (var sha = SHA1.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private static void ShowLicense(OptionSet p)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Contains MsPL license for 'Concentus.Oggfile' (https://github.com/lostromb/concentus.oggfile) and 'NAudio' (https://github.com/naudio/NAudio)");
            Console.WriteLine(@"
   Copyright (C) 2014, Andrew Ward <afward@gmail.com>
   
   This license governs use of the accompanying software. If you use the software, you accept this license.
   If you do not accept the license, do not use the software.

   1. Definitions
   The terms 'reproduce,' 'reproduction,' 'derivative works,' and 'distribution' have the
   same meaning here as under U.S. copyright law.

   A 'contribution' is the original software, or any additions or changes to the software.

   A 'contributor' is any person that distributes its contribution under this license.

   'Licensed patents' are a contributor's patent claims that read directly on its contribution.

   2. Grant of Rights

   (A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3,
   each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution,
   prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.

   (B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3,
   each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make,
   have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative
   works of the contribution in the software.

   3. Conditions and Limitations

   (A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.

   (B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software,
   your patent license from such contributor to the software ends automatically.

   (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution
   notices that are present in the software.

   (D) If you distribute any portion of the software in source code form, you may do so only under this license by including
   a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object
   code form, you may only do so under a license that complies with this license.

   (E) The software is licensed 'as-is.' You bear the risk of using it. The contributors give no express warranties,
   guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot change.
   To the extent permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness
   for a particular purpose and non-infringement.
");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Contains 3-Clause BSD license for 'Concentus' (https://github.com/lostromb/concentus) ");
            Console.WriteLine(@"
   Copyright(c) 2008        Thorvald Natvig
   Copyright(c) 2003 - 2004 Mark Borgerding
   Copyright(c) 2007 - 2008 CSIRO
   Copyright(c) 2007 - 2011 Xiph.Org Foundation
   Copyright(c) 2007 - 2008 Jean - Marc Valin
   Copyright(c) 2001 - 2011 Timothy B.Terriberry
   Copyright(c) 2006 - 2011 Skype Limited.
   Copyright(c) 2008 - 2011 Octasic Inc.
   Copyright(c) 2016        Logan Stromberg

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Contains Apache license for 'ID3.NET' (https://github.com/JeevanJames/Id3) ");
            Console.WriteLine(@"Copyright (c) 2005-2019 Jeevan James
All rights reserved.

Licensed under the Apache License, Version 2.0 (the 'License');
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an 'AS IS' BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Contains 3-Clause BSD license for 'TonieFile' ");
            Console.WriteLine(@"
   Copyright (c) 2020 g3gg0.de

   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions
   are met:

   - Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.

   - Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

   - Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
   ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
   LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
   A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
   OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
   EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
   PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
   PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
   LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
   NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
   SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(@"
-------------
 Disclaimer:
-------------
   This tool is in no way endorsed with the creator of the Tonie Boxes.
   They did not support this tool nor was there any other exchange of information.

   The author distances herewith explicit from any use of this tool for piracy or 
   alike and takes no responsibility if someone else uses this tool for these goals.
   Its purpose is solely meant for understanding the file exchange format, technology
   and only to be used for the goal of interoperability for personal use.

   All information required for building this tool was gained by reading the files
   stored on SD card for legally acquired tonies. No decryption or authentication was
   overridden.

");
        }

        private static void FindTonieFiles(List<string> files, string v)
        {
            foreach (var file in new DirectoryInfo(v).GetFiles())
            {
                if (file.Name == "500304E0")
                {
                    files.Add(v + Path.DirectorySeparatorChar + "500304E0");
                }
            }
            foreach (var dir in new DirectoryInfo(v).GetDirectories())
            {
                if (!dir.Name.StartsWith("."))
                {
                    FindTonieFiles(files, dir.FullName);
                }
            }
        }

        private static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("");
            Console.WriteLine(" Tonie Encoder Decoder for DIYs - (c)2020 Team RevvoX");
            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine("                         (build " + ThisAssembly.Git.SemVer.Major + "." + ThisAssembly.Git.SemVer.Minor + "." + ThisAssembly.Git.SemVer.Patch + "-" + ThisAssembly.Git.Branch + "+" + ThisAssembly.Git.Commit + (ThisAssembly.Git.IsDirty ? ",dirty" : "") + ")");
            Console.WriteLine("");
            Console.WriteLine("Start with:");
            Console.WriteLine("  Teddy.exe -m decode [options] <toniefile>          - Dump tonie file content");
            Console.WriteLine("  Teddy.exe -m info [options] <toniefile>            - Show header details");
            Console.WriteLine("  Teddy.exe -m encode [options] <folder>             - Create a tonie file from all MP3 in this folder");
            Console.WriteLine("  Teddy.exe -m encode [options] <file1> <file2> ...  - Create a tonie file from specified MP3 files");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
            Console.WriteLine("");
            Console.WriteLine("Hints:");
            Console.WriteLine("  Prefix files are files named '0001.mp3', '0002.mp3', ..., '9999.mp3' and will get prepended to the");
            Console.WriteLine("  real track audio data. These are meant to add the track number in front of the file so the");
            Console.WriteLine("  kids know which track number is played right now.");
            Console.WriteLine("");
            Console.WriteLine("  As JSON file you could specify also a link like e.g. http://gt-blog.de/JSON/tonies.json");
        }
    }
}

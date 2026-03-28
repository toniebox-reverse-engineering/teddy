using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TonieFile
{
    public class TonieTools
    {
        public enum eDumpFormat
        {
            FormatText,
            FormatCSV,
            FormatJSON
        };

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
            public string Episodes; // unused
            [JsonProperty("tracks")]
            public string[] Tracks;
            [JsonProperty("release")]
            public string Release; // unused
            [JsonProperty("language")]
            public string Language;
            [JsonProperty("category")]
            public string Category; // unused
            [JsonProperty("pic")]
            public string Pic;
        }

        public static bool DumpInfo(StringBuilder message, eDumpFormat dumpFormat, string file, TonieData[] tonieInfos, string customName = null)
        {
            try
            {
                TonieAudio dumpFile = TonieAudio.FromFile(file);
                dumpFile.CalculateStatistics(out long segCount, out long segLength, out int minSegs, out int maxSegs, out ulong minGranule, out ulong maxGranule, out ulong highestGranule);
                string uidrev = new FileInfo(file).Directory.Name + new FileInfo(file).Name;
                List<string> groups = (from Match m in Regex.Matches(uidrev, @"[A-F0-9]{2}") select m.Value).ToList();
                groups.Reverse();
                string uid = string.Join("", groups.ToArray());

                string dateExtra = "";
                uint id = dumpFile.Header.AudioId;

                if (id < 0x50000000)
                {
                    dateExtra = "custom file, real date ";
                    id += 0x50000000;
                }
                var date = DateTimeOffset.FromUnixTimeSeconds(id);
                bool first = false;

                switch (dumpFormat)
                {
                    case eDumpFormat.FormatCSV:
                        {
                            message.Append(uid + ";");
                            message.Append(dumpFile.Header.AudioId.ToString("X8") + ";");
                            message.Append(date.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss") + ";");
                            message.Append(dumpFile.HeaderLength + ";");
                            message.Append(((dumpFile.HeaderLength != 0xFFC) ? "[WARNING: EXTRA DATA]" : "[OK]") + ";");
                            message.Append(dumpFile.Header.Padding.Length + ";");
                            message.Append(dumpFile.Header.AudioLength + ";");
                            message.Append((dumpFile.Header.AudioLength == dumpFile.Audio.Length ? "[OK]" : "[INCORRECT]") + ";");
                            message.Append(BitConverter.ToString(dumpFile.Header.Hash).Replace("-", "") + ";");
                            message.Append((dumpFile.HashCorrect ? "[OK]" : "[INCORRECT]") + ";");
                            foreach (var offset in dumpFile.Header.AudioChapters)
                            {
                                message.Append(offset + " ");
                            }
                            message.Append(";");

                            message.Append(segCount + ";");
                            message.Append(minSegs + ";");
                            message.Append(maxSegs + ";");
                            message.Append(segLength + ";");
                            message.Append(highestGranule + ";");
                            message.Append(TonieAudio.FormatGranule(highestGranule) + ";");
                            message.Append(minGranule + ";");
                            message.Append(maxGranule + ";");
                            message.Append((1000 * minGranule / 48000.0f) + ";");
                            message.Append((1000 * maxGranule / 48000.0f) + ";");
                            message.AppendLine();
                            break;
                        }

                    case eDumpFormat.FormatText:
                        {
                            message.AppendLine("Dump of " + dumpFile.Filename + " (UID " + uid + "):");
                            message.AppendLine("  Header: AudioID     0x" + dumpFile.Header.AudioId.ToString("X8") + " (" + dateExtra + date.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss") + ")");

                            string[] titles = null;
                            string hashString = BitConverter.ToString(dumpFile.Header.Hash).Replace("-", "");
                            var found = tonieInfos.Where(t => t.Hash.Contains(hashString));
                            TonieData info = null;
                            string infoHashString = null;
                            int infoIndex = 0;
                            if (found.Count() > 0)
                            {
                                info = found.First();
                                titles = info.Tracks;
                                infoIndex = Array.IndexOf(info.AudioIds, dumpFile.Header.AudioId);
                                Array.Resize(ref info.Hash, info.AudioIds.Length);
                                infoHashString = info.Hash[infoIndex];

                                message.AppendLine("  Header: JSON Name   '" + info.Title + "'");
                            }
                            if (!string.IsNullOrEmpty(customName))
                            {
                                message.AppendLine("  Header: Custom Name '" + customName + "'");
                            }

                            message.AppendLine("  Header: Length      0x" + dumpFile.HeaderLength.ToString("X8") + " " + ((dumpFile.HeaderLength != 0xFFC) ? " [WARNING: EXTRA DATA]" : "[OK]"));
                            message.AppendLine("  Header: Padding     0x" + dumpFile.Header.Padding.Length.ToString("X8"));
                            message.AppendLine("  Header: AudioLen    0x" + dumpFile.Header.AudioLength.ToString("X8") + " " + (dumpFile.Header.AudioLength == dumpFile.Audio.Length ? "[OK]" : "[INCORRECT]"));
                            message.AppendLine("  Header: Checksum    " + hashString + " " + (dumpFile.HashCorrect ? "[OK]" : "[INCORRECT]") + " " + (infoHashString != null ? (infoHashString == hashString ? "[JSON MATCH]" : "[JSON MISMATCH]") : "[NO JSON INFO]"));
                            message.AppendLine("  Header: Chapters    ");

                            if (info != null && infoHashString == null)
                            {
                                info.Hash[infoIndex] = hashString;
                            }

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
                                    lengthString = (trackOffset - prevTime).ToString(@"hh\:mm\:ss\.ff");
                                    prevTime = trackOffset;
                                }

                                string title = "";

                                if (titles != null && track - 1 < titles.Length)
                                {
                                    title = titles[track - 1];
                                }
                                message.AppendLine("    Track #" + track.ToString("00") + "  " + lengthString + "  " + title);
                            }

                            message.AppendLine("  Ogg: Segments       " + segCount + " (min: " + minSegs + " max: " + maxSegs + " per OggPage)");
                            message.AppendLine("  Ogg: net payload    " + segLength + " byte");
                            message.AppendLine("  Ogg: granules       total: " + highestGranule + " (" + TonieAudio.FormatGranule(highestGranule) + " hh:mm:ss.ff)");
                            message.AppendLine("  Ogg: granules/page  min: " + minGranule + " max: " + maxGranule + " (" + (1000 * minGranule / 48000.0f) + "ms - " + (1000 * maxGranule / 48000.0f) + "ms)");
                            message.AppendLine();
                            break;
                        }

                    case eDumpFormat.FormatJSON:
                        {
                            if (!first)
                            {
                                message.AppendLine(",");
                            }
                            message.AppendLine("  {");
                            message.AppendLine("    \"uid\": \"" + uid + "\",");
                            message.AppendLine("    \"audio_id\": \"" + dumpFile.Header.AudioId.ToString("X8") + "\",");
                            message.AppendLine("    \"audio_date\": \"" + date.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss") + "\",");
                            message.AppendLine("    \"header_length\": " + dumpFile.HeaderLength + ",");
                            message.AppendLine("    \"header_ok\": \"" + ((dumpFile.HeaderLength != 0xFFC) ? "FALSE" : "TRUE") + "\",");
                            message.AppendLine("    \"padding\": " + dumpFile.Header.Padding.Length + ",");
                            message.AppendLine("    \"audio_length\": " + dumpFile.Header.AudioLength + ",");
                            message.AppendLine("    \"audio_length_check\": \"" + (dumpFile.Header.AudioLength == dumpFile.Audio.Length ? "TRUE" : "FALSE") + "\",");
                            message.AppendLine("    \"audio_hash\": \"" + BitConverter.ToString(dumpFile.Header.Hash).Replace("-", "") + "\",");
                            message.AppendLine("    \"audio_hash_check\": \"" + (dumpFile.HashCorrect ? "TRUE" : "FALSE") + "\",");
                            message.Append("    \"chapters\": [ 0");
                            foreach (var offset in dumpFile.Header.AudioChapters)
                            {
                                message.Append(", " + offset);
                            }
                            message.AppendLine(" ],");
                            message.AppendLine("    \"segments\": " + segCount + ",");
                            message.AppendLine("    \"min_segments_per_page\": " + minSegs + ",");
                            message.AppendLine("    \"max_segments_per_page\": " + maxSegs + ",");
                            message.AppendLine("    \"segment_length_sum\": " + segLength + ",");
                            message.AppendLine("    \"highest_granule\": " + highestGranule + ",");
                            message.AppendLine("    \"time\": \"" + TonieAudio.FormatGranule(highestGranule) + "\",");
                            message.AppendLine("    \"min_granules\": " + minGranule + ",");
                            message.AppendLine("    \"max_granules\": " + maxGranule + ",");
                            message.AppendLine("    \"min_time\": " + (1000 * minGranule / 48000.0f) + ",");
                            message.AppendLine("    \"max_time\": " + (1000 * maxGranule / 48000.0f) + "");
                            message.AppendLine("  }");

                            break;
                        }
                }

                return true;
            }
            catch (Exception ex)
            {
                message.Append("Failed: " + ex.ToString());
                return false;
            }
        }
    }
}

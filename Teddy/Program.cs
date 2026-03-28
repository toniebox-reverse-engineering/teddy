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
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TonieFile;

namespace Teddy
{
    class Program
    {
        public static int Verbosity = 0;

        private static TonieTools.TonieData[] TonieInfos = new TonieTools.TonieData[0];


        public static void LoadJson(string path)
        {
            try
            {
                if (path.StartsWith("http"))
                {
                    using (var httpClient = new HttpClient())
                    {
                        var json = httpClient.GetStringAsync(path).Result;
                        TonieInfos = JsonConvert.DeserializeObject<TonieTools.TonieData[]>(json);
                    }
                }
                else if (File.Exists(path))
                {
                    TonieInfos = JsonConvert.DeserializeObject<TonieTools.TonieData[]>(File.ReadAllText(path));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to load JSON:");
                Console.WriteLine(e.Message);
                return;
            }
        }

        public static void SaveJson(string path)
        {
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(TonieInfos, Formatting.Indented));
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
                if (titles != null && titles.Length > chapter)
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

        private static string FileChecksum(string filename)
        {
            using (var sha = SHA1.Create())
            {
                using (var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        static void Main(string[] args)
        {
            bool showLicense = false;
            bool showHelp = false;
            TonieTools.eDumpFormat dumpFormat = TonieTools.eDumpFormat.FormatText;
            bool useVbr = false;
            bool reallyRename = false;
            bool deleteDuplicates = false;
            bool singleOgg = false;
            string mode = "";
            string outputLocation = "";
            string prefixLocation = null;
            string audioId = "";
            string writeJson = null;
            string jsonFile = "https://api.revvox.de/tonies.json?source=Teddy&version=" + ThisAssembly.Git.BaseTag;

            int bitRate = 96;

            var p = new OptionSet {
                { "m|mode=",    "Operating mode: info, decode, encode, rename",             (string n) => mode = n },
                { "o|output=",  "Location where to write the file(s) to",                   (string r) => outputLocation = r },
                { "p|prefix=",  "encode: Location where to find prefix files",              (string r) => prefixLocation = r },
                { "i|id=",      "encode: Set AudioID for encoding (default: current time)", (string r) => audioId = r },
                { "b|bitrate=", "encode: Set opus bit rate (default: "+bitRate+" kbps)",    (int r) => bitRate = r },
                { "vbr",        "encode: Use VBR encoding",                                 r => useVbr = true },
                { "s",          "decode: Export as single .ogg file",                       r => singleOgg = true },
                { "y",          "rename: really rename files, else its a dry run",          v => { reallyRename = true; } },
                { "d",          "rename: delete duplicates",                                v => { deleteDuplicates = true; } },
                { "w|write=",   "info: write updated json to local file",                   (string v) => { writeJson = v; } },
                { "j|json=",    "Set JSON file/URL with details about tonies",              (string r) => jsonFile = r },
                { "f|format=",  "Output details as: csv, json or text",                     v => { switch(v) { case "csv":  dumpFormat = TonieTools.eDumpFormat.FormatCSV; break; case "json":  dumpFormat = TonieTools.eDumpFormat.FormatJSON; break; case "text":  dumpFormat = TonieTools.eDumpFormat.FormatText; break; } } },
                { "v",          "increase debug message verbosity",                         v => { if (v != null) ++Verbosity; } },
                { "h|help",     "show this message and exit",                               h => showHelp = true },
                { "license",    "show licenses and disclaimer",                             h => showLicense = true },
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

                                /* skip creative tonies for now */
                                if (dumpFile.Header.AudioId == 1)
                                {
                                    Console.WriteLine("  '" + file + "' -> '(creative)'");
                                    continue;
                                }

                                var found = TonieInfos.Where(t => t.AudioIds.Contains(dumpFile.Header.AudioId));
                                string destFileName = "(unknown)";

                                if (found.Count() > 0)
                                {
                                    var info = found.First();
                                    destFileName = info.Title;
                                    if (!string.IsNullOrEmpty(info.Model))
                                    {
                                        string destName = Path.Combine(extra[1], info.Model + " - " + dumpFile.Header.AudioId.ToString("X8") + " - " + RemoveInvalidChars(destFileName).Trim());
                                        if (file != destName)
                                        {
                                            renameList.Add(file, destName);
                                        }
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
                                    if (deleteDuplicates)
                                    {
                                        if (reallyRename)
                                        {
                                            var di = new FileInfo(key).Directory;
                                            File.Delete(key);
                                            renamed++;
                                            if (di.GetFiles().Length == 0)
                                            {
                                                di.Delete();
                                            }
                                        }
                                    }
                                    else
                                    {

                                        Console.WriteLine("    Skipped, destination file content matches");
                                    }
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
                            case TonieTools.eDumpFormat.FormatCSV:
                                if (extra.Count > 1 || Directory.Exists(extra[0]))
                                {
                                    Console.WriteLine("UID;AudioID;AudioDate;HeaderLength;HeaderOK;Padding;AudioLength;AudioLengthCheck;AudioHash;AudioHashCheck;Chapters;Segments;MinSegmentsPerPage;MaxSegmentsPerPage;SegLengthSum;HighestGranule;Time;MinGranules;MaxGranules;MinTime;MaxTime;");
                                }
                                break;
                            case TonieTools.eDumpFormat.FormatJSON:
                                Console.WriteLine("[");
                                break;
                            case TonieTools.eDumpFormat.FormatText:
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
                                StringBuilder message = new StringBuilder();

                                TonieTools.DumpInfo(message, dumpFormat, file, TonieInfos);

                                Console.Write(message.ToString());
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
                            case TonieTools.eDumpFormat.FormatCSV:
                                break;
                            case TonieTools.eDumpFormat.FormatJSON:
                                Console.WriteLine("]");
                                break;
                            case TonieTools.eDumpFormat.FormatText:
                                break;
                        }

                        if (writeJson != null)
                        {
                            SaveJson(writeJson);
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
                                TonieAudio dump2 = TonieAudio.FromFile(file);

                                string[] titles = null;
                                List<string> tags = new List<string>();
                                tags.Add("TeddyVersion=" + GetVersion());
                                tags.Add("TeddyFile=" + file);

                                string hashString = BitConverter.ToString(dump2.Header.Hash).Replace("-", "");
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

                                if (!Directory.Exists(outDirectory))
                                {
                                    Console.WriteLine("Error: Output directory '" + outDirectory + "' does not exist");
                                    return;
                                }

                                try
                                {
                                    dump2.DumpAudioFiles(outDirectory, inFile + "-" + dump2.Header.AudioId.ToString("X8"), singleOgg, tags.ToArray(), titles);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("[ERROR] Failed to write .ogg/.cue'");
                                    Console.WriteLine("   Message:    " + ex.Message);
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

                    uint id = (uint)DateTimeOffset.Now.ToUnixTimeSeconds() - 0x50000000;
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
                            outFile = Path.Combine(outLocationEncode, "500304E0");
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
                /* search for original tonie files or those we renamed */
                if (file.Name.EndsWith("0304E0") || Regex.Matches(file.Name, @"[A-Za-z0-9-]+ - [A-F0-9]+ - .*").Count == 1)
                {
                    files.Add(Path.Combine(v, file.Name));
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
            Console.WriteLine("                         (build " + GetVersion() + ")");
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
            Console.WriteLine("  As JSON file you could specify also a link like e.g. https://api.revvox.de/tonies.json");
        }

        private static string GetVersion()
        {
            return ThisAssembly.Git.SemVer.Major + "." + ThisAssembly.Git.SemVer.Minor + "." + ThisAssembly.Git.SemVer.Patch + "-" + ThisAssembly.Git.Branch + "+" + ThisAssembly.Git.Commit + (ThisAssembly.Git.IsDirty ? ",dirty" : "");
        }
    }
}

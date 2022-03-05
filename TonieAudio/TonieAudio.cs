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

using Concentus.Enums;
using Concentus.Oggfile;
using Concentus.Structs;
using Id3;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using static TonieFile.ProtoCoder;

namespace TonieFile
{
    public class TonieAudio
    {
        public class FileHeader
        {
            public byte[] Hash;
            public int AudioLength;
            public int AudioId;
            public uint[] AudioChapters;
            public byte[] Padding;
            [SkipEncode]
            /* in sfx.bin this is set to zero, all other files miss this field */
            public bool Usable = true;
        }

        public class EncodingException : Exception
        {
            public EncodingException(string message) : base(message)
            {
            }
        }

        private Dictionary<uint, ulong> PageGranuleMap = new Dictionary<uint, ulong>();
        public FileHeader Header = new FileHeader();
        public byte[] Audio = new byte[0];
        public byte[] FileContent = new byte[0];
        public List<string> FileList = new List<string>();
        public long HeaderLength { get; private set; }
        public bool HashCorrect = false;
        public string Filename { get; set; }
        public string FilenameShort => new FileInfo(Filename).Name;


        public static string FormatGranule(ulong granule)
        {
            ulong time = 100 * granule / 48000;
            ulong frames = time % 100;
            ulong seconds = (time / 100) % 60;
            ulong minutes = (time / 100 / 60) % 60;
            ulong hours = (time / 100 / 60 / 60);

            return hours.ToString("00") + ":" + minutes.ToString("00") + ":" + seconds.ToString("00") + "." + frames.ToString("00");
        }

        public ulong GetGranuleByPage(uint page)
        {
            if (PageGranuleMap.ContainsKey(page))
            {
                return PageGranuleMap[page];
            }
            return ulong.MaxValue;
        }

        public uint GetHighestPage()
        {
            return PageGranuleMap.Keys.Last();
        }


        public void CalculateStatistics(out long totalSegments, out long segLength, out int minSegs, out int maxSegs, out ulong minGranule, out ulong maxGranule, out ulong highestGranule)
        {
            totalSegments = 0;
            segLength = 0;
            minSegs = 0xff;
            maxSegs = 0;
            minGranule = long.MaxValue;
            maxGranule = 0;
            highestGranule = 0;
            var file = File.OpenRead(Filename);
            long lastPos = 0x1000;
            long curPos = 0x1000;
            ulong lastGranule = 0;
            byte[] oggPageBuf = new byte[27];

            while (curPos < file.Length)
            {
                lastPos = curPos;

                file.Seek(curPos, SeekOrigin.Begin);
                file.Read(oggPageBuf, 0, oggPageBuf.Length);

                if (oggPageBuf[0] != 'O' || oggPageBuf[1] != 'g' || oggPageBuf[2] != 'g')
                {
                    Console.WriteLine("[ERROR] Not an Ogg page header at 0x" + curPos.ToString("X8"));
                    break;
                }

                ulong granule = BitConverter.ToUInt64(oggPageBuf, 6);
                uint pageNum = BitConverter.ToUInt32(oggPageBuf, 0x12);
                byte segmentsCount = oggPageBuf[26];
                byte[] segmentLengths = new byte[segmentsCount];

                if (!PageGranuleMap.ContainsKey(pageNum))
                {
                    PageGranuleMap.Add(pageNum, granule);
                }

                file.Read(segmentLengths, 0, segmentLengths.Length);

                curPos += 27;
                curPos += segmentLengths.Length;

                foreach (var len in segmentLengths)
                {
                    totalSegments++;
                    segLength += len;
                    curPos += len;
                }

                long lastOffset = lastPos % 0x1000;
                long curOffset = curPos % 0x1000;
                if (lastOffset >= curOffset && curOffset != 0)
                {
                    Console.WriteLine("[ERROR] Ogg page ends in next block at 0x" + curPos.ToString("X8"));
                    break;
                }

                if (lastPos >= 0x2000 && curPos < file.Length)
                {
                    ulong granuleDelta = granule - lastGranule;

                    minSegs = Math.Min(minSegs, segmentsCount);
                    maxSegs = Math.Max(maxSegs, segmentsCount);
                    minGranule = Math.Min(minGranule, granuleDelta);
                    maxGranule = Math.Max(maxGranule, granuleDelta);
                }

                highestGranule = Math.Max(highestGranule, granule);
                lastGranule = granule;
            }
            file.Close();
        }

        public ulong[] ParsePositions()
        {
            List<ulong> positions = new List<ulong>();
            var file = File.OpenRead(Filename);

            int curChapter = 0;
            long curPos = 0x1000;
            positions.Add(0);

            while (file.Position < file.Length)
            {
                byte[] buf = new byte[0x16];

                file.Seek(curPos, SeekOrigin.Begin);
                file.Read(buf, 0, buf.Length);

                if (buf[0] != 'O' || buf[1] != 'g' || buf[2] != 'g')
                {
                    break;
                }

                ulong granule = BitConverter.ToUInt64(buf, 6);
                uint pageNum = BitConverter.ToUInt32(buf, 0x12);

                if (Header.AudioChapters.Length > curChapter && pageNum >= Header.AudioChapters[curChapter])
                {
                    positions.Add(granule);
                    curChapter++;
                }

                if (file.Position == file.Length)
                {
                    positions.Add(granule);
                    break;
                }

                curPos += 0x1000;
            }

            file.Close();
            return positions.ToArray();
        }

        public class EncodeCallback
        {
            protected string ShortName;
            protected string DisplayName;

            public virtual void Progress(decimal pct)
            {
                int lastPct = (int)(pct * 20);
                if (lastPct % 5 == 0)
                {
                    if (lastPct != 20)
                    {
                        Console.Write("" + (lastPct * 5) + "%");
                    }
                }
                else
                {
                    Console.Write(".");
                }
            }

            public virtual void FileStart(int track, string sourceFile)
            {
                ParseName(track, sourceFile);
                Console.Write(" Track " + track.ToString().PadLeft(3) + " - " + ShortName + "  [");
            }

            protected void ParseName(int track, string sourceFile)
            {
                int snipLen = 15;
                DisplayName = new FileInfo(sourceFile).Name;
                try
                {
                    var tag = new Mp3(sourceFile, Mp3Permissions.Read).GetAllTags().FirstOrDefault();
                    if (tag != null && tag.Title.IsAssigned)
                    {
                        DisplayName = tag.Title.Value;
                    }
                }
                catch (Exception ex)
                {

                }

                ShortName = DisplayName.PadRight(snipLen).Substring(0, snipLen);
            }

            public virtual void FileDone()
            {
               Console.WriteLine("]");
            }
            public virtual void FileFailed(string message)
            {
                Console.WriteLine("]");
                Console.WriteLine("File Failed: " + message);
            }

            public virtual void Failed(string message)
            {
                Console.WriteLine("]");
                Console.WriteLine("Failed: " + message);
            }

            public virtual void Warning(string message)
            {
                Console.WriteLine("");
                Console.WriteLine("Warning: " + message);
            }
        }

        public TonieAudio()
        {
        }

        public TonieAudio(string[] sources, uint audioId, int bitRate = 48000, bool useVbr = false, string prefixLocation = null, EncodeCallback cbr = null)
        {
            BuildFileList(sources);
            BuildFromFiles(FileList, audioId, bitRate, useVbr, prefixLocation, cbr);
        }

        public static TonieAudio FromFile(string file, bool readAudio = true)
        {
            TonieAudio audio = new TonieAudio();
            audio.ReadFile(file, readAudio);

            return audio;
        }

        private void BuildFileList(string[] sources)
        {
            foreach (var source in sources)
            {
                string item = source.Trim('"').Trim(Path.DirectorySeparatorChar);

                if (Directory.Exists(item))
                {
                    var filesInDir = Directory.GetFiles(item, "*.mp3").Concat(Directory.GetFiles(item, "*.ogg")).OrderBy(n => n).ToArray();
                    string[] sourceFiles = filesInDir;
                    bool failed = false;

                    try
                    {
                        var fileTuples = filesInDir.Select(f => new Tuple<string, Id3Tag>(f, new Mp3(f, Mp3Permissions.Read).GetAllTags().FirstOrDefault()));

                        sourceFiles = fileTuples.Where(t => t.Item2 != null).OrderBy(m => m.Item2.Track.Value).Select(t => t.Item1).ToArray();

                        if (sourceFiles.Length < filesInDir.Length)
                        {
                            failed = true;
                            sourceFiles = filesInDir;
                        }
                    }
                    catch (Exception ex)
                    {
                        failed = true;
                    }

                    if (failed)
                    {
                        Console.WriteLine("[INFO] Tried to sort using MP3 tag for track number, but not all files in");
                        Console.WriteLine("[INFO] this folder have valid ID3 tags. ");
                        Console.WriteLine("[INFO] Please make sure all files have correct ID3 fields to have them sorted correctly.");
                        Console.WriteLine("[INFO] ");
                        Console.WriteLine("[INFO] Sorting files by their filename.");
                        Console.WriteLine("");
                    }

                    FileList.AddRange(sourceFiles);
                }
                else if (File.Exists(item))
                {
                    if (!(item.ToLower().EndsWith(".mp3") || item.ToLower().EndsWith(".ogg")))
                    {
                        throw new InvalidDataException("Specified item '" + item + "' is no MP3/Ogg");
                    }
                    FileList.Add(item);
                }
                else
                {
                    throw new FileNotFoundException("Specified item '" + item + "' not found or supported");
                }
            }
        }

        public void ReadFile(string fileName, bool readAudio = true)
        {
            Filename = fileName;
            var file = File.OpenRead(fileName);
            if (file.Length < 0x2000)
            {
                throw new InvalidDataException();
            }

            long len = file.Length;
            if (!readAudio)
            {
                len = 4096;
            }
            byte[] buffer = new byte[len];

            if (file.Read(buffer, 0, buffer.Length) != buffer.Length)
            {
                throw new InvalidDataException();
            }
            FileContent = buffer;

            file.Close();
            ParseBuffer();
            CalculateStatistics(out _, out _, out _, out _, out _, out _, out _);
        }

        private void BuildFromFiles(List<string> sourceFiles, uint audioId, int bitRate, bool useVbr, string prefixLocation, EncodeCallback cbr)
        {
            GenerateAudio(sourceFiles, audioId, bitRate, useVbr, prefixLocation, cbr);
            FileContent = new byte[Audio.Length + 0x1000];
            Array.Copy(Audio, 0, FileContent, 0x1000, Audio.Length);
            WriteHeader();
        }

        private void WriteHeader()
        {
            int expectedSize = 0x1000 - 4;

            /* set protobuf header size */
            FileContent[0] = (byte)(expectedSize >> 24);
            FileContent[1] = (byte)(expectedSize >> 16);
            FileContent[2] = (byte)(expectedSize >> 8);
            FileContent[3] = (byte)(expectedSize >> 0);

            /* first use one byte padding */
            Header.Padding = new byte[1];

            var coder = new ProtoCoder();
            byte[] dataPre = coder.Serialize(Header);

            /* then determine how many extra bytes to fill */
            long padding = expectedSize - dataPre.Length;
            Header.Padding = new byte[padding];

            byte[] data = coder.Serialize(Header);

            Array.Copy(data, 0, FileContent, 4, data.Length);
        }

        private void GenerateAudio(List<string> sourceFiles, uint audioId, int bitRate, bool useVbr, string prefixLocation = null, EncodeCallback cbr = null)
        {
            int channels = 2;
            int samplingRate = 48000;
            List<uint> chapters = new List<uint>();

            var outFormat = new WaveFormat(samplingRate, 2);

            if(cbr == null)
            {
                cbr = new EncodeCallback();
            }

            OpusEncoder encoder = OpusEncoder.Create(48000, 2, OpusApplication.OPUS_APPLICATION_AUDIO);
            encoder.Bitrate = bitRate;
            encoder.UseVBR = useVbr;

            if (audioId == 0)
            {
                audioId = (uint)((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
            }

            string tempName = Path.GetTempFileName();

            using (Stream outputData = new FileStream(tempName, FileMode.OpenOrCreate))
            {
                byte[] buffer = new byte[2880 * channels * 2];
                OpusTags tags = new OpusTags();
                tags.Comment = "Lavf56.40.101";
                tags.Fields["encoder"] = "opusenc from opus-tools 0.1.9";
                tags.Fields["encoder_options"] = "--quiet --bitrate 96 --vbr";
                tags.Fields["pad"] = new string('0', 0x138);

                OpusOggWriteStream oggOut = new OpusOggWriteStream(encoder, outputData, tags, samplingRate, (int)audioId);

                uint lastIndex = 0;
                int track = 0;
                bool warned = false;
                long maxSize = 0x77359400;

                foreach (var sourceFile in sourceFiles)
                {
                    if ((outputData.Length + 0x1000) >= maxSize)
                    {
                        cbr.Warning("Close to 2 GiB, stopping");
                        break;
                    }

                    try
                    {
                        int bytesReturned = 1;
                        int totalBytesRead = 0;

                        track++;
                        chapters.Add(lastIndex);

                        int lastPct = 0;
                        cbr.FileStart(track, sourceFile);


                        /* prepend a audio file for e.g. chapter number */
                        if (prefixLocation != null)
                        {
                            string prefixFile = Path.Combine(prefixLocation, track.ToString("0000") + ".mp3");

                            if (!File.Exists(prefixFile))
                            {
                                throw new FileNotFoundException("Missing prefix file '" + prefixFile + "'");
                            }

                            try
                            {
                                var prefixStream = new Mp3FileReader(prefixFile);
                                var prefixResampled = new MediaFoundationResampler(prefixStream, outFormat);

                                while (true)
                                {
                                    bytesReturned = prefixResampled.Read(buffer, 0, buffer.Length);

                                    if (bytesReturned <= 0)
                                    {
                                        break;
                                    }

                                    bool isEmpty = (buffer.Where(v => v != 0).Count() == 0);
                                    if (!isEmpty)
                                    {
                                        float[] sampleBuffer = ConvertToFloat(buffer, bytesReturned, channels);

                                        if ((outputData.Length + 0x1000 + sampleBuffer.Length) >= maxSize)
                                        {
                                            break;
                                        }
                                        oggOut.WriteSamples(sampleBuffer, 0, sampleBuffer.Length);
                                    }
                                    lastIndex = (uint)oggOut.PageCounter;
                                }
                            }
                            catch (Exception ex)
                            {
                                throw new Exception("Failed processing prefix file '" + prefixFile + "'");
                            }
                        }

                        /* then the real audio file */
                        string type = sourceFile.Split('.').Last().ToLower();
                        WaveStream stream = null;

                        switch (type)
                        {
                            case "mp3":
                                stream = new Mp3FileReader(sourceFile);
                                break;

                            case "ogg":
                                stream = new OpusWaveStream(File.OpenRead(sourceFile), bitRate, channels);
                                break;
                        }

                        if(stream == null)
                        {
                            cbr.FileFailed("Unknown file type");
                            continue;
                        }

                        var streamResampled = new MediaFoundationResampler(stream, outFormat);

                        while (true)
                        {
                            bytesReturned = streamResampled.Read(buffer, 0, buffer.Length);

                            if (bytesReturned <= 0)
                            {
                                break;
                            }
                            totalBytesRead += bytesReturned;

                            decimal progress = (decimal)stream.Position / stream.Length;

                            if ((int)(progress * 20) != lastPct)
                            {
                                lastPct = (int)(progress * 20);
                                cbr.Progress(progress);
                            }

                            bool isEmpty = (buffer.Where(v => v != 0).Count() == 0);
                            if (!isEmpty)
                            {
                                float[] sampleBuffer = ConvertToFloat(buffer, bytesReturned, channels);

                                oggOut.WriteSamples(sampleBuffer, 0, sampleBuffer.Length);
                            }
                            lastIndex = (uint)oggOut.PageCounter;
                        }
                        stream.Close();

                        cbr.FileDone();
                    }
                    catch (OpusOggWriteStream.PaddingException e)
                    {
                        string msg = "Failed to pad opus data properly. Please try CBR with bitrates a multiple of 24 kbps";
                        cbr.Failed(msg);
                        throw new EncodingException(msg);
                    }
                    catch (FileNotFoundException e)
                    {
                        cbr.Failed(e.Message);
                        throw new FileNotFoundException(e.Message);
                    }
                    catch (InvalidDataException e)
                    {
                        string msg = "Failed processing " + sourceFile;
                        cbr.Failed(msg);
                        throw new Exception(msg);
                    }
                    catch (Exception e)
                    {
                        string msg = "Failed processing " + sourceFile;
                        cbr.Failed(msg);
                        throw new Exception(msg);
                    }

                    if (!warned && outputData.Length >= maxSize / 2)
                    {
                        cbr.Warning("Approaching 2 GiB, please reduce the bitrate");
                        warned = true;
                    }
                }

                oggOut.Finish();
                Header.AudioId = oggOut.LogicalStreamId;
            }

            Audio = File.ReadAllBytes(tempName);

            var prov = new SHA1CryptoServiceProvider();
            Header.Hash = prov.ComputeHash(Audio);
            Header.AudioChapters = chapters.ToArray();
            Header.AudioLength = Audio.Length;
            Header.Padding = new byte[0];

            File.Delete(tempName);
        }

        private static float ShortToSample(short pcmValue)
        {
            return pcmValue / 32768f;
        }

        private float[] ConvertToFloat(byte[] pcmBuffer, int bytes, int channels)
        {
            int bytesPerSample = 2 * channels;
            float[] samples = new float[channels * (pcmBuffer.Length / bytesPerSample)];

            for (int sample = 0; sample < bytes / bytesPerSample; sample++)
            {
                for (int chan = 0; chan < channels; chan++)
                {
                    samples[channels * sample + chan] = ShortToSample((short)(pcmBuffer[bytesPerSample * sample + 1 + chan * 2] << 8 | pcmBuffer[bytesPerSample * sample + 0 + chan * 2]));
                }
            }

            return samples;
        }

        private void ParseBuffer()
        {
            int protoBufLength = (FileContent[0] << 24) | (FileContent[1] << 16) | (FileContent[2] << 8) | FileContent[3];

            if (protoBufLength > 0x10000)
            {
                throw new InvalidDataException();
            }
            byte[] protoBuf = new byte[protoBufLength];
            int payloadStart = protoBufLength + 4;
            int payloadLength = FileContent.Length - payloadStart;
            byte[] payload = new byte[payloadLength];

            Array.Copy(FileContent, 4, protoBuf, 0, protoBufLength);
            Array.Copy(FileContent, protoBufLength + 4, payload, 0, payloadLength);

            var coder = new ProtoCoder();
            FileHeader header = coder.Deserialize<FileHeader>(protoBuf);

            var prov = new SHA1CryptoServiceProvider();
            var hash = prov.ComputeHash(payload);

            HashCorrect = true;
            if (!hash.SequenceEqual(header.Hash))
            {
                HashCorrect = false;
            }

            byte[] data = coder.Serialize(header);

            HeaderLength = data.Length;
            Audio = payload;
            Header = header;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct OggPageHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] Header;
            public byte Version;
            public byte Type;
            public ulong GranulePosition;
            public uint BitstreamSerialNumber;
            public uint PageSequenceNumber;
            public uint Checksum;
            public byte PageSegments;
        }

        private class OggPage
        {
            public OggPageHeader Header;
            public byte SegmentTableLength => (byte)SegmentTable.Length;
            public int TotalSegmentLengths => Segments.Sum(s => s.Length);
            public byte[] SegmentTable
            {
                get
                {
                    int lengthBytes = Segments.Length + Segments.Sum(s => s.Length / 0xFF);
                    byte[] table = new byte[lengthBytes];

                    int tableIndex = 0;
                    for (int pos = 0; pos < Segments.Length; pos++)
                    {
                        int len = Segments[pos].Length;

                        while (len >= 0xFF)
                        {
                            table[tableIndex++] = 0xFF;
                            len -= 0xFF;
                        }
                        table[tableIndex++] = (byte)len;
                    }
                    return table;
                }
            }
            public byte[][] Segments;
            public int Size;

            public class Crc
            {
                const uint CRC32_POLY = 0x04c11db7;
                static uint[] crcTable = new uint[256];

                static Crc()
                {
                    for (uint i = 0; i < 256; i++)
                    {
                        uint s = i << 24;
                        for (int j = 0; j < 8; ++j)
                        {
                            s = (s << 1) ^ (s >= (1U << 31) ? CRC32_POLY : 0);
                        }
                        crcTable[i] = s;
                    }
                }

                uint _crc;

                public Crc()
                {
                    Reset();
                }

                public void Reset()
                {
                    _crc = 0U;
                }

                public void Update(byte nextVal)
                {
                    _crc = (_crc << 8) ^ crcTable[nextVal ^ (_crc >> 24)];
                }

                public void Update(byte[] buf)
                {
                    foreach (byte val in buf)
                    {
                        Update(val);
                    }
                }

                public bool Test(uint checkCrc)
                {
                    return _crc == checkCrc;
                }

                public uint Value
                {
                    get
                    {
                        return _crc;
                    }
                }
            }

            public OggPage()
            {
            }

            public OggPage(OggPage src)
            {
                Header = src.Header;
                Size = src.Size;
                Segments = new byte[src.Segments.Length][];
                for (int pos = 0; pos < Segments.Length; pos++)
                {
                    Segments[pos] = new byte[src.Segments[pos].Length];
                    Array.Copy(src.Segments[pos], Segments[pos], Segments[pos].Length);
                }
            }

            public void Write(Stream outFile)
            {
                UpdateHeader();
                WriteInternal(outFile);
            }

            private void UpdateHeader()
            {
                MemoryStream memStream = new MemoryStream();

                Header.PageSegments = SegmentTableLength;
                Header.Checksum = 0;
                WriteInternal(memStream);

                OggPage.Crc crc = new OggPage.Crc();
                crc.Update(memStream.ToArray());

                Header.Checksum = crc.Value;
                Size = (int)memStream.Length;
            }

            private void WriteInternal(Stream stream)
            {
                byte[] data = StructureToByteArray(Header);

                stream.Write(data, 0, data.Length);
                stream.Write(SegmentTable, 0, SegmentTable.Length);

                foreach (var seg in Segments)
                {
                    stream.Write(seg, 0, seg.Length);
                }
            }
        }

        public void DumpAudioFiles(string outDirectory, string outFileName, bool singleOgg, string[] tags, string[] titles)
        {
            int hdrOffset = 0;
            OggPage[] metaPages = GetOggHeaders(ref hdrOffset);
            AddTags(metaPages, tags);

            if (singleOgg)
            {
                string outFile = Path.Combine(outDirectory, outFileName);

                File.WriteAllBytes(outFile + ".ogg", Audio);
                //File.WriteAllText(outFile + ".cue", BuildCueSheet(tonie), Encoding.UTF8);
            }
            else
            {
                for (int chapter = 0; chapter < Header.AudioChapters.Length; chapter++)
                {
                    string fileName = Path.Combine(outDirectory, outFileName + " - Track #" + (chapter + 1) + ".ogg");
                    FileStream outFile = File.Open(fileName, FileMode.Create, FileAccess.Write);
                    OggPage[] metaPagesTrack = metaPages.Select(p => new OggPage(p)).ToArray();

                    if (titles != null && chapter < titles.Length)
                    {
                        string[] trackTags = new[] { "TITLE=" + titles[chapter] };
                        AddTags(metaPagesTrack, trackTags);
                    }

                    foreach (OggPage page in metaPagesTrack)
                    {
                        page.Write(outFile);
                    }

                    int offset = Math.Max(0, (int)(0x1000 * (Header.AudioChapters[chapter] - 2)));
                    int endOffset = int.MaxValue;

                    if (chapter + 1 < Header.AudioChapters.Length)
                    {
                        endOffset = Math.Max(0, (int)(0x1000 * (Header.AudioChapters[chapter + 1] - 2)));
                    }

                    bool done = false;
                    ulong granuleStart = ulong.MaxValue;
                    uint pageStart = uint.MaxValue;
                    while (!done)
                    {
                        OggPage page = GetOggPage(ref offset);

                        if (page == null)
                        {
                            break;
                        }

                        /* reached the end of this chapter? */
                        if (offset >= endOffset || offset >= Audio.Length)
                        {
                            /* set EOS flag */
                            page.Header.Type = 4;
                            done = true;
                        }

                        /* do not write meta headers again. only applies to first chapter */
                        if (!IsMeta(page))
                        {
                            /* set granule position relative to chapter start */
                            if (granuleStart == ulong.MaxValue)
                            {
                                granuleStart = page.Header.GranulePosition;
                                pageStart = page.Header.PageSequenceNumber;
                            }

                            page.Header.GranulePosition -= granuleStart;
                            page.Header.PageSequenceNumber -= pageStart;
                            page.Header.PageSequenceNumber += 2;


                            page.Write(outFile);
                        }
                    }

                    outFile.Close();
                }
            }
        }


        private static byte[] StructureToByteArray(object obj)
        {
            int len = Marshal.SizeOf(obj);
            byte[] arr = new byte[len];
            IntPtr ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, arr, 0, len);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }

        private static void ByteArrayToStructure<T>(byte[] bytearray, int offset, ref T obj)
        {
            int len = Marshal.SizeOf(obj);
            IntPtr i = Marshal.AllocHGlobal(len);
            Marshal.Copy(bytearray, offset, i, len);
            obj = (T)Marshal.PtrToStructure(i, typeof(T));
            Marshal.FreeHGlobal(i);
        }

        private void AddTags(OggPage[] pages, string[] tags)
        {
            foreach(OggPage header in pages)
            {
                if (IsMetaType(header, "OpusTags"))
                {
                    uint entryPos = 8 + 4 + GetUint(header.Segments[0], 8);
                    foreach (string tag in tags)
                    {
                        byte[] tagBytes = Encoding.UTF8.GetBytes(tag);
                        byte[] append = new byte[4 + tagBytes.Length];

                        WriteUint(append, 0, (uint)tagBytes.Length);

                        Array.Copy(tagBytes, 0, append, 4, tagBytes.Length);
                        Array.Resize(ref header.Segments[0], header.Segments[0].Length + append.Length);

                        Array.Copy(append, 0, header.Segments[0], header.Segments[0].Length - append.Length, append.Length);

                        WriteUint(header.Segments[0], entryPos, GetUint(header.Segments[0], entryPos) + 1);
                    }
                }
            }
        }

        private OggPage[] GetOggHeaders(ref int offset)
        {
            List<OggPage> headers = new List<OggPage>();
            bool done = false;

            while (!done)
            {
                int curOffset = offset;
                OggPage header = GetOggPage(ref curOffset);

                if (header.Segments.Length < 1)
                {
                    done = true;
                }

                if (IsMeta(header))
                {
                    headers.Add(header);
                    offset = curOffset;
                }
                else
                {
                    done = true;
                }
            }

            return headers.ToArray();
        }

        private static void WriteUint(byte[] buf, uint pos, uint value)
        {
            buf[pos + 0] = (byte)value;
            buf[pos + 1] = (byte)(value >> 8);
            buf[pos + 2] = (byte)(value >> 16);
            buf[pos + 3] = (byte)(value >> 24);
        }

        private static uint GetUint(byte[] buf, uint pos)
        {
            return (uint)buf[pos] | ((uint)buf[pos + 1] << 8) | ((uint)buf[pos + 2] << 16) | ((uint)buf[pos + 3] << 24);
        }

        private static bool IsMetaType(OggPage header, string type)
        {
            return Encoding.UTF8.GetString(header.Segments[0], 0, 8) == type;
        }

        private static bool IsMeta(OggPage header)
        {
            switch (Encoding.UTF8.GetString(header.Segments[0], 0, 8))
            {
                case "OpusHead":
                case "OpusTags":
                    return true;
                default:
                    return false;
            }
        }

        private OggPage GetOggPage(ref int offset)
        {
            OggPageHeader hdr = new OggPageHeader();
            OggPage page = new OggPage();
            int pageSize = 0;
            ByteArrayToStructure(Audio, offset, ref hdr);

            if (hdr.Header[0] != 'O' || hdr.Header[1] != 'g' || hdr.Header[2] != 'g' || hdr.Header[3] != 'S')
            {
                return null;
            }

            page.Header = hdr;
            pageSize += Marshal.SizeOf(hdr);

            page.Segments = new byte[0][];

            /* where will the segment data start */
            int segmentDataPos = pageSize + hdr.PageSegments;
            /* position in page segment table */
            int pageSegTablePos = 0;
            /* logical number of the segment */
            int pageSegNum = 0;
            while (pageSegTablePos < hdr.PageSegments)
            {
                int lenEntry = Audio[offset + pageSize];
                int len = lenEntry;
                pageSize++;
                pageSegTablePos++;

                while (lenEntry == 0xFF)
                {
                    lenEntry = Audio[offset + pageSize];
                    len += lenEntry;
                    pageSize++;
                    pageSegTablePos++;
                }
                Array.Resize(ref page.Segments, pageSegNum + 1);
                page.Segments[pageSegNum] = new byte[len];
                Array.Copy(Audio, offset + segmentDataPos, page.Segments[pageSegNum], 0, len);
                segmentDataPos += page.Segments[pageSegNum].Length;
                pageSegNum++;
            }

            page.Size = segmentDataPos;
            offset += segmentDataPos;

            return page;
        }

    }
}
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
            if(PageGranuleMap.ContainsKey(page))
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

            return positions.ToArray();
        }

        public TonieAudio()
        {
        }

        public TonieAudio(string[] sources, uint audioId, int bitRate = 96000, bool useVbr = false, string prefixLocation = null)
        {
            BuildFileList(sources);
            BuildFromFiles(FileList, audioId, bitRate, useVbr, prefixLocation);
        }

        public static TonieAudio FromFile(string file)
        {
            TonieAudio audio = new TonieAudio();
            audio.ReadFile(file);

            return audio;
        }

        private void BuildFileList(string[] sources)
        {
            foreach (var source in sources)
            {
                string item = source.Trim('"').Trim(Path.DirectorySeparatorChar);

                if (Directory.Exists(item))
                {
                    var filesInDir = Directory.GetFiles(item, "*.mp3").OrderBy(n => n).ToArray();
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
                    if (!item.ToLower().EndsWith(".mp3"))
                    {
                        throw new InvalidDataException("Specified item '" + item + "' is no MP3");
                    }
                    FileList.Add(item);
                }
                else
                {
                    throw new FileNotFoundException("Specified item '" + item + "' not found or supported");
                }
            }
        }

        public void ReadFile(string fileName)
        {
            Filename = fileName;
            var file = File.OpenRead(fileName);
            if (file.Length < 0x2000)
            {
                throw new InvalidDataException();
            }
            byte[] buffer = new byte[file.Length];

            if (file.Read(buffer, 0, buffer.Length) != buffer.Length)
            {
                throw new InvalidDataException();
            }

            FileContent = buffer;
            ParseBuffer();
            CalculateStatistics(out _, out _, out _, out _, out _, out _, out _);
        }

        private void BuildFromFiles(List<string> sourceFiles, uint audioId, int bitRate, bool useVbr, string prefixLocation)
        {
            GenerateAudio(sourceFiles, audioId, bitRate, useVbr, prefixLocation);
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
            var stream = new MemoryStream();

            var coder = new ProtoCoder();
            byte[] dataPre = coder.Serialize<FileHeader>(Header);

            /* then determine how many extra bytes to fill */
            long padding = expectedSize - dataPre.Length;
            Header.Padding = new byte[padding];

            byte[] data = coder.Serialize<FileHeader>(Header);

            Array.Copy(data, 0, FileContent, 4, data.Length);
        }

        private void GenerateAudio(List<string> sourceFiles, uint audioId, int bitRate, bool useVbr, string prefixLocation = null)
        {
            int channels = 2;
            int samplingRate = 48000;
            List<uint> chapters = new List<uint>();

            var outFormat = new WaveFormat(samplingRate, 2);

            OpusEncoder encoder = OpusEncoder.Create(48000, 2, OpusApplication.OPUS_APPLICATION_AUDIO);
            encoder.Bitrate = bitRate;
            encoder.UseVBR = useVbr;

            if (audioId == 0)
            {
                audioId = (uint)((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
            }

            using (MemoryStream outputData = new MemoryStream())
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

                foreach (var sourceFile in sourceFiles)
                {
                    try
                    {
                        int bytesReturned = 1;
                        int totalBytesRead = 0;

                        track++;
                        chapters.Add(lastIndex);

                        int lastPct = 0;
                        int snipLen = 15;
                        string displayName = new FileInfo(sourceFile).Name;
                        try
                        {
                            var tag = new Mp3(sourceFile, Mp3Permissions.Read).GetAllTags().FirstOrDefault();
                            if (tag != null && tag.Title.IsAssigned)
                            {
                                displayName = tag.Title.Value;
                            }
                        }
                        catch (Exception ex)
                        {

                        }

                        string shortName = displayName.PadRight(snipLen).Substring(0, snipLen);

                        Console.Write(" Track " + track.ToString().PadLeft(3) + " - " + shortName + "  [");


                        /* prepend a audio file for e.g. chapter number */
                        if (prefixLocation != null)
                        {
                            string prefixFile = Path.Combine(prefixLocation, track.ToString("0000") + ".mp3");

                            if(!File.Exists(prefixFile))
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

                                        oggOut.WriteSamples(sampleBuffer, 0, sampleBuffer.Length);
                                    }
                                    lastIndex = (uint)oggOut.PageCounter;
                                }
                            }
                            catch(Exception ex)
                            {
                                throw new Exception("Failed processing prefix file '" + prefixFile + "'");
                            }
                        }

                        /* then the real audio file */
                        var stream = new Mp3FileReader(sourceFile);
                        var streamResampled = new MediaFoundationResampler(stream, outFormat);

                        while (true)
                        {
                            bytesReturned = streamResampled.Read(buffer, 0, buffer.Length);

                            if(bytesReturned <= 0)
                            {
                                break;
                            }
                            totalBytesRead += bytesReturned;

                            float progress = (float)stream.Position / stream.Length;

                            if ((int)(progress * 20) != lastPct)
                            {
                                lastPct = (int)(progress * 20);
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

                            bool isEmpty = (buffer.Where(v => v != 0).Count() == 0);
                            if (!isEmpty)
                            {
                                float[] sampleBuffer = ConvertToFloat(buffer, bytesReturned, channels);

                                oggOut.WriteSamples(sampleBuffer, 0, sampleBuffer.Length);
                            }
                            lastIndex = (uint)oggOut.PageCounter;
                        }

                        Console.WriteLine("]");
                        stream.Close();
                    }
                    catch(OpusOggWriteStream.PaddingException e)
                    {
                        Console.WriteLine();
                        throw new EncodingException("Failed to pad opus data properly. Please try CBR with bitrates a multiple of 24 kbps");
                    }
                    catch (FileNotFoundException e)
                    {
                        Console.WriteLine();
                        throw new FileNotFoundException(e.Message);
                    }
                    catch (InvalidDataException e)
                    {
                        Console.WriteLine();
                        throw new Exception("Failed processing " + sourceFile);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine();
                        throw new Exception("Failed processing " + sourceFile);
                    }
                }

                oggOut.Finish();
                Audio = outputData.ToArray();

                var prov = new SHA1CryptoServiceProvider();
                Header.Hash = prov.ComputeHash(Audio);
                Header.AudioChapters = chapters.ToArray();
                Header.AudioId = oggOut.LogicalStreamId;
                Header.AudioLength = Audio.Length;
                Header.Padding = new byte[0];
            }
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
    }
}
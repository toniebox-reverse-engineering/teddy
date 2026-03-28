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

using NAudio.Wave;
using System;
using System.IO;
using System.Runtime.InteropServices;
using FFMpegCore;

namespace TonieFile
{
    /// <summary>
    /// Cross-platform audio reader that supports MP3, FLAC, WAV, M4A, AAC, WMA, and more.
    /// Uses NAudio's Mp3FileReader on Windows for MP3 files (more efficient).
    /// Uses FFmpeg for all other formats and for all formats on Linux/macOS.
    /// </summary>
    public class CrossPlatformAudioReader : WaveStream
    {
        private readonly WaveStream sourceStream;
        private readonly string tempWavFile;

        public CrossPlatformAudioReader(string audioFilePath)
        {
            string extension = Path.GetExtension(audioFilePath).ToLower();

            // Use Mp3FileReader on Windows for MP3 files (more efficient)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && extension == ".mp3")
            {
                sourceStream = new Mp3FileReader(audioFilePath);
                tempWavFile = null;
            }
            else
            {
                // Use FFmpeg for all other formats on all platforms
                sourceStream = DecodeWithFFmpeg(audioFilePath, out tempWavFile);
            }
        }

        private WaveStream DecodeWithFFmpeg(string audioFilePath, out string tempFile)
        {
            // Create temporary file for decoded WAV
            tempFile = Path.GetTempFileName();

            try
            {
                // Use FFmpeg to decode audio to WAV
                FFMpegArguments
                    .FromFileInput(audioFilePath)
                    .OutputToFile(tempFile, true, options => options
                        .ForceFormat("wav"))
                    .ProcessSynchronously();

                // Read the decoded WAV file
                return new WaveFileReader(tempFile);
            }
            catch
            {
                // Clean up temp file if decode fails
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
                throw;
            }
        }

        public override WaveFormat WaveFormat => sourceStream.WaveFormat;

        public override long Length => sourceStream.Length;

        public override long Position
        {
            get => sourceStream.Position;
            set => sourceStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return sourceStream.Read(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                sourceStream?.Dispose();

                // Clean up temp file on Linux/macOS
                if (tempWavFile != null && File.Exists(tempWavFile))
                {
                    try
                    {
                        File.Delete(tempWavFile);
                    }
                    catch
                    {
                        // Ignore errors cleaning up temp file
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}

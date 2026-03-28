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
using FFMpegCore.Pipes;

namespace TonieFile
{
    /// <summary>
    /// Cross-platform audio resampler that uses MediaFoundation on Windows
    /// and FFmpeg on Linux/macOS
    /// </summary>
    public class CrossPlatformResampler : WaveStream
    {
        private readonly WaveStream sourceStream;
        private readonly WaveFormat targetFormat;
        private readonly MemoryStream resampledData;
        private long position;

        public CrossPlatformResampler(WaveStream sourceStream, WaveFormat targetFormat)
        {
            this.sourceStream = sourceStream;
            this.targetFormat = targetFormat;
            this.position = 0;

            // Use MediaFoundation on Windows, FFmpeg elsewhere
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                resampledData = ResampleWithMediaFoundation(sourceStream, targetFormat);
            }
            else
            {
                resampledData = ResampleWithFFmpeg(sourceStream, targetFormat);
            }
        }

        private MemoryStream ResampleWithMediaFoundation(WaveStream source, WaveFormat target)
        {
            using (var resampler = new MediaFoundationResampler(source, target))
            {
                var output = new MemoryStream();
                byte[] buffer = new byte[target.AverageBytesPerSecond];
                int bytesRead;

                while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, bytesRead);
                }

                output.Position = 0;
                return output;
            }
        }

        private MemoryStream ResampleWithFFmpeg(WaveStream source, WaveFormat target)
        {
            // Create temporary files for FFmpeg processing
            string tempInputFile = Path.GetTempFileName();
            string tempOutputFile = Path.GetTempFileName();

            try
            {
                // Write source stream to temp file as WAV
                using (var writer = new WaveFileWriter(tempInputFile, source.WaveFormat))
                {
                    byte[] buffer = new byte[source.WaveFormat.AverageBytesPerSecond];
                    int bytesRead;
                    while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        writer.Write(buffer, 0, bytesRead);
                    }
                }

                // Use FFmpeg to resample
                FFMpegArguments
                    .FromFileInput(tempInputFile)
                    .OutputToFile(tempOutputFile, true, options => options
                        .WithAudioSamplingRate(target.SampleRate)
                        .WithCustomArgument($"-ac {target.Channels}")
                        .ForceFormat("wav"))
                    .ProcessSynchronously();

                // Read resampled data
                using (var reader = new WaveFileReader(tempOutputFile))
                {
                    var output = new MemoryStream();
                    byte[] buffer = new byte[reader.WaveFormat.AverageBytesPerSecond];
                    int bytesRead;

                    // Skip the WAV header by reading raw data
                    while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, bytesRead);
                    }

                    output.Position = 0;
                    return output;
                }
            }
            finally
            {
                // Clean up temp files
                if (File.Exists(tempInputFile)) File.Delete(tempInputFile);
                if (File.Exists(tempOutputFile)) File.Delete(tempOutputFile);
            }
        }

        public override WaveFormat WaveFormat => targetFormat;

        public override long Length => resampledData.Length;

        public override long Position
        {
            get => position;
            set
            {
                position = value;
                resampledData.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = resampledData.Read(buffer, offset, count);
            position += bytesRead;
            return bytesRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                resampledData?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
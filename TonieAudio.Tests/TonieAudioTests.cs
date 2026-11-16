using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TonieFile;
using Xunit;

namespace TonieAudio.Tests
{
    public class TonieAudioTests
    {
        private const int Rate = 48000;
        private static readonly string AlbumDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "album");
        private static readonly char Sep = Path.DirectorySeparatorChar;

        [Fact]
        public void NormalizeSourcePath_KeepsAbsolutePathAndStripsTrailingSeparator()
        {
            string abs = AlbumDir + Sep;
            Assert.Equal(AlbumDir, TonieFile.TonieAudio.NormalizeSourcePath(abs));
            Assert.Equal(AlbumDir, TonieFile.TonieAudio.NormalizeSourcePath("\"" + abs + "\""));
            Assert.True(Path.IsPathRooted(TonieFile.TonieAudio.NormalizeSourcePath(abs)));
        }

        [Fact]
        public void NormalizeSourcePath_KeepsFilesystemRoot()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(@"C:\", TonieFile.TonieAudio.NormalizeSourcePath(@"C:\"));
            }
            else
            {
                Assert.Equal("/", TonieFile.TonieAudio.NormalizeSourcePath("/"));
            }
        }

        [Fact]
        public void GetAudioFiles_MatchesExtensionsCaseInsensitivelyAndSkipsOthers()
        {
            var names = TonieFile.TonieAudio.GetAudioFiles(AlbumDir).Select(Path.GetFileName).OrderBy(n => n).ToArray();

            Assert.Equal(new[] { "01 stereo44k.mp3", "02 MONO22K.MP3", "03 opus48k.ogg" }, names);
        }

        [Theory]
        [InlineData(22050, 1)]
        [InlineData(44100, 2)]
        [InlineData(48000, 2)]
        public void ToPcm16_ConvertsToStereo48k16BitAndKeepsPitch(int inRate, int inChannels)
        {
            const double seconds = 1.0;
            const int freq = 1000;
            var source = SineStream(inRate, inChannels, freq, seconds);

            var pcm = AudioInput.ToPcm16(source, Rate, 2);
            Assert.Equal(new WaveFormat(Rate, 16, 2), pcm.WaveFormat);

            short[] left = ReadLeftChannel(pcm);
            Assert.InRange(left.Length, Rate * seconds * 0.98, Rate * seconds * 1.02);
            Assert.InRange(EstimateFrequency(left, Rate / 4, Rate / 2), freq * 0.98, freq * 1.02);
        }

        [Fact]
        public void OpenMp3_DecodesWithManagedDecoder()
        {
            using (var mp3 = AudioInput.OpenMp3(Path.Combine(AlbumDir, "01 stereo44k.mp3")))
            {
                Assert.Equal(44100, mp3.WaveFormat.SampleRate);
                Assert.Equal(2, mp3.WaveFormat.Channels);
                Assert.InRange(mp3.TotalTime.TotalSeconds, 1.95, 2.15);
            }
        }

        [Fact]
        public void Encode_FolderOfMp3AndOgg_ProducesValidTonieFileWithCorrectAudio()
        {
            string outFile = Path.Combine(Path.GetTempPath(), "teddy-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                /* absolute folder path with trailing separator - used to break on Linux */
                var generated = new TonieFile.TonieAudio(new[] { AlbumDir + Sep }, 0x12345678, 96000, false, null);

                Assert.Equal(3, generated.FileList.Count);
                Assert.Equal(0x12345678u, generated.Header.AudioId);
                Assert.Equal(3, generated.Header.AudioChapters.Length);
                Assert.Equal(0u, generated.Header.AudioChapters[0]);
                Assert.True(generated.Header.AudioChapters.SequenceEqual(generated.Header.AudioChapters.OrderBy(c => c)));

                File.WriteAllBytes(outFile, generated.FileContent);

                var parsed = TonieFile.TonieAudio.FromFile(outFile);
                Assert.True(parsed.HashCorrect);
                Assert.Equal(SHA1.HashData(parsed.Audio), parsed.Header.Hash);
                Assert.Equal(generated.Header.AudioChapters, parsed.Header.AudioChapters);

                /* decode the embedded Opus stream and check every track's tone landed in order */
                short[] left;
                using (var decoded = new OpusWaveStream(new MemoryStream(parsed.Audio), Rate, 2))
                {
                    left = ReadLeftChannel(decoded);
                }

                Assert.InRange(left.Length / (double)Rate, 5.8, 6.6);
                Assert.InRange(EstimateFrequency(left, (int)(0.75 * Rate), Rate / 2), 430, 450);    /* track 1: 440 Hz MP3 44.1k stereo */
                Assert.InRange(EstimateFrequency(left, (int)(2.75 * Rate), Rate / 2), 980, 1020);   /* track 2: 1 kHz MP3 22.05k mono */
                Assert.InRange(EstimateFrequency(left, (int)(4.75 * Rate), Rate / 2), 645, 675);    /* track 3: 660 Hz Opus/Ogg */
            }
            finally
            {
                File.Delete(outFile);
            }
        }

        private static WaveStream SineStream(int rate, int channels, double freq, double seconds)
        {
            int frames = (int)(rate * seconds);
            var bytes = new byte[frames * channels * 2];
            for (int i = 0; i < frames; i++)
            {
                short v = (short)(Math.Sin(2 * Math.PI * freq * i / rate) * 16000);
                for (int c = 0; c < channels; c++)
                {
                    BitConverter.TryWriteBytes(new Span<byte>(bytes, (i * channels + c) * 2, 2), v);
                }
            }
            return new RawSourceWaveStream(new MemoryStream(bytes), new WaveFormat(rate, 16, channels));
        }

        private static short[] ReadLeftChannel(IWaveProvider pcm)
        {
            Assert.Equal(16, pcm.WaveFormat.BitsPerSample);
            int channels = pcm.WaveFormat.Channels;
            var samples = new List<short>();
            var buffer = new byte[16384];
            int read;
            while ((read = pcm.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int pos = 0; pos + 2 * channels <= read; pos += 2 * channels)
                {
                    samples.Add(BitConverter.ToInt16(buffer, pos));
                }
            }
            return samples.ToArray();
        }

        private static double EstimateFrequency(short[] samples, int start, int length)
        {
            Assert.True(start + length <= samples.Length, "window beyond end of audio");
            int crossings = 0;
            for (int i = start + 1; i < start + length; i++)
            {
                if ((samples[i - 1] < 0) != (samples[i] < 0))
                {
                    crossings++;
                }
            }
            return crossings / 2.0 / (length / (double)Rate);
        }
    }
}

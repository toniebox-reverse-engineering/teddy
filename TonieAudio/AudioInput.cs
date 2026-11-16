using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport;
using System;
using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TonieAudio.Tests")]

namespace TonieFile
{
    /// <summary>
    /// Cross-platform replacement for the Windows-only NAudio pieces previously used by the encoder
    /// (Mp3FileReader -> ACM codec, MediaFoundationResampler -> Media Foundation).
    /// Everything here is fully managed and runs on Linux, macOS and Windows.
    /// </summary>
    internal static class AudioInput
    {
        /// <summary>Opens an MP3 file using the managed NLayer decoder.</summary>
        public static WaveStream OpenMp3(string path)
        {
            return new Mp3FileReaderBase(path, wf => new Mp3FrameDecompressor(wf));
        }

        /// <summary>
        /// Converts any PCM/IEEE-float source into 16 bit little-endian PCM with the given sample rate
        /// and channel count - the same output format MediaFoundationResampler produced before.
        /// Mono is duplicated to both channels, other sample rates are resampled (WDL resampler).
        /// </summary>
        public static IWaveProvider ToPcm16(WaveStream source, int sampleRate, int channels)
        {
            ISampleProvider samples = source.ToSampleProvider();

            if (samples.WaveFormat.Channels != channels)
            {
                if (samples.WaveFormat.Channels == 1 && channels == 2)
                {
                    samples = new MonoToStereoSampleProvider(samples);
                }
                else if (samples.WaveFormat.Channels == 2 && channels == 1)
                {
                    samples = new StereoToMonoSampleProvider(samples);
                }
                else
                {
                    throw new InvalidDataException("Unsupported channel count " + samples.WaveFormat.Channels);
                }
            }

            if (samples.WaveFormat.SampleRate != sampleRate)
            {
                samples = new WdlResamplingSampleProvider(samples, sampleRate);
            }

            return new SampleToWaveProvider16(samples);
        }
    }
}

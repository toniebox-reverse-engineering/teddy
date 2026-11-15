using NAudio.Wave;
using System;
using System.IO;
using System.Threading;
using TonieFile;

namespace TeddyBench.Core
{
    /// <summary>
    /// Plays the audio of a tonie file through an <see cref="IWavePlayer"/> created by
    /// <see cref="PlatformServices.AudioOutputFactory"/> (or the factory passed in).
    /// Progress and end of playback are reported via events from the player thread.
    /// </summary>
    public class TonieAudioPlayer
    {
        private readonly Func<IWavePlayer> OutputFactory;
        private SafeThread PlayThread;
        private volatile bool StopRequested;
        private int SeekRequest = -1;

        public bool IsPlaying => PlayThread != null;

        /// <summary>(position, total) roughly every 100 ms.</summary>
        public event Action<TimeSpan, TimeSpan> Progress;

        /// <summary>Raised once when playback ended, was stopped or failed (exception or null).</summary>
        public event Action<Exception> Stopped;

        public TonieAudioPlayer(Func<IWavePlayer> outputFactory = null)
        {
            OutputFactory = outputFactory;
        }

        public void Play(string tonieFile)
        {
            if (PlayThread != null)
            {
                return;
            }

            Func<IWavePlayer> factory = OutputFactory ?? PlatformServices.AudioOutputFactory;
            if (factory == null)
            {
                throw new InvalidOperationException("No audio output available on this platform");
            }

            StopRequested = false;
            PlayThread = new SafeThread(() => Run(tonieFile, factory), "PlayThread");
            PlayThread.Start();
        }

        public void Stop()
        {
            StopRequested = true;
        }

        /// <summary>Seeks to the given second (applied by the player thread).</summary>
        public void Seek(int seconds)
        {
            Interlocked.Exchange(ref SeekRequest, seconds);
        }

        /// <summary>Requests stop and waits for the player thread.</summary>
        public void Shutdown(int timeoutMs = 1000)
        {
            var thread = PlayThread;
            StopRequested = true;
            if (thread != null && !thread.Join(timeoutMs))
            {
                thread.Abort();
            }
        }

        private void Run(string tonieFile, Func<IWavePlayer> factory)
        {
            Exception error = null;

            try
            {
                TonieAudio dump = TonieAudio.FromFile(tonieFile);

                using (var output = factory())
                using (var reader = new OpusWaveStream(new MemoryStream(dump.Audio), 48000, 2))
                {
                    output.PlaybackStopped += (s, e) => StopRequested = true;
                    output.Init(reader);
                    output.Play();
                    Logger.Log(Logger.eLogLevel.Debug, "Playback started");

                    while (!StopRequested)
                    {
                        Thread.Sleep(100);

                        int seek = Interlocked.Exchange(ref SeekRequest, -1);
                        if (seek >= 0)
                        {
                            reader.SeekTo(TimeSpan.FromSeconds(seek));
                        }

                        Progress?.Invoke(reader.OpusDecoder.CurrentTime, reader.OpusDecoder.TotalTime);
                    }
                    output.Stop();
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }

            Logger.Log(Logger.eLogLevel.Debug, "Playback stopped");
            PlayThread = null;
            Stopped?.Invoke(error);
        }
    }
}

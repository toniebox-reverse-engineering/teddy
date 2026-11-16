using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace TeddyBench.Core
{
    /// <summary>
    /// Audio output for Linux (and other Unix systems): streams 16 bit PCM to the standard input of the
    /// desktop's command line player - pw-play (PipeWire), paplay (PulseAudio) or aplay (ALSA).
    /// NAudio's own outputs are Windows only; this needs no native library.
    /// </summary>
    public class PipeAudioOutput : IWavePlayer
    {
        /// <summary>A player command and its arguments for (sample rate, channels).</summary>
        public class Player
        {
            public string Command;
            public Func<int, int, string> Arguments;
        }

        /// <summary>Supported players in order of preference.</summary>
        public static readonly Player[] KnownPlayers =
        {
            new Player { Command = "pw-play", Arguments = (rate, channels) => $"--raw --rate {rate} --channels {channels} --format s16 -" },
            new Player { Command = "paplay", Arguments = (rate, channels) => $"--raw --rate={rate} --channels={channels} --format=s16le" },
            new Player { Command = "aplay", Arguments = (rate, channels) => $"-q -t raw -f S16_LE -r {rate} -c {channels} -" },
        };

        private readonly Player PlayerCommand;
        private IWaveProvider Source;
        private Process Process;
        private Thread Writer;
        private volatile bool StopRequested;
        private readonly ManualResetEventSlim NotPaused = new ManualResetEventSlim(true);

        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
        /// <summary>Not supported by the external players; kept for the interface.</summary>
        public float Volume { get; set; } = 1.0f;
        public WaveFormat OutputWaveFormat => Source?.WaveFormat;
        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        public PipeAudioOutput(Player player)
        {
            PlayerCommand = player;
        }

        /// <summary>A factory for the first player found in PATH, or null if there is none.</summary>
        public static Func<IWavePlayer> TryCreateFactory(Func<string, bool> isAvailable = null)
        {
            isAvailable = isAvailable ?? IsInPath;
            Player player = KnownPlayers.FirstOrDefault(p => isAvailable(p.Command));
            if (player == null)
            {
                Logger.Log(Logger.eLogLevel.Warning, "[Audio] No audio player found (pw-play, paplay or aplay); playback is disabled.");
                return null;
            }
            Logger.Log(Logger.eLogLevel.Debug, "[Audio] Using " + player.Command + " for playback");
            return () => new PipeAudioOutput(player);
        }

        public static bool IsInPath(string command)
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            return path.Split(Path.PathSeparator).Where(d => d.Length > 0).Any(d => File.Exists(Path.Combine(d, command)));
        }

        public void Init(IWaveProvider waveProvider)
        {
            if (waveProvider.WaveFormat.Encoding != WaveFormatEncoding.Pcm || waveProvider.WaveFormat.BitsPerSample != 16)
            {
                throw new ArgumentException("Only 16 bit PCM is supported", nameof(waveProvider));
            }
            Source = waveProvider;
        }

        public void Play()
        {
            if (Source == null)
            {
                throw new InvalidOperationException("Init must be called first");
            }

            NotPaused.Set();
            if (PlaybackState == PlaybackState.Playing || PlaybackState == PlaybackState.Paused)
            {
                PlaybackState = PlaybackState.Playing;
                return;
            }

            var format = Source.WaveFormat;
            Process = Process.Start(new ProcessStartInfo(PlayerCommand.Command, PlayerCommand.Arguments(format.SampleRate, format.Channels))
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            /* drain the outputs so the player never blocks on them */
            Process.OutputDataReceived += (s, e) => { };
            Process.ErrorDataReceived += (s, e) => { if (e.Data != null) Logger.Log(Logger.eLogLevel.Debug, "[Audio] " + e.Data); };
            Process.BeginOutputReadLine();
            Process.BeginErrorReadLine();

            StopRequested = false;
            PlaybackState = PlaybackState.Playing;
            Writer = new Thread(WriteLoop) { IsBackground = true, Name = "PipeAudioOutput" };
            Writer.Start();
        }

        private void WriteLoop()
        {
            Exception error = null;
            Process process = Process;

            try
            {
                /* ~50 ms chunks keep the delay after Stop/seek small */
                var buffer = new byte[Math.Max(Source.WaveFormat.AverageBytesPerSecond / 20, Source.WaveFormat.BlockAlign)];
                Stream input = process.StandardInput.BaseStream;

                while (!StopRequested)
                {
                    NotPaused.Wait();
                    if (StopRequested)
                    {
                        break;
                    }

                    int read = Source.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        break;
                    }
                    input.Write(buffer, 0, read);
                }

                /* end of data: let the player finish what it buffered */
                process.StandardInput.Close();
                if (!StopRequested)
                {
                    process.WaitForExit();
                }
            }
            catch (IOException ex)
            {
                /* the player exited (or was stopped) */
                if (!StopRequested)
                {
                    error = new IOException(PlayerCommand.Command + " stopped unexpectedly", ex);
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                KillPlayer(process);
                PlaybackState = PlaybackState.Stopped;
            }

            PlaybackStopped?.Invoke(this, new StoppedEventArgs(error));
        }

        public void Pause()
        {
            if (PlaybackState == PlaybackState.Playing)
            {
                /* stops feeding the player; it plays out what is already buffered (~100 ms) */
                NotPaused.Reset();
                PlaybackState = PlaybackState.Paused;
            }
        }

        public void Stop()
        {
            StopRequested = true;
            NotPaused.Set();
            KillPlayer(Process);

            var writer = Writer;
            if (writer != null && writer != Thread.CurrentThread)
            {
                writer.Join(2000);
            }
            Writer = null;
        }

        private static void KillPlayer(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception)
            {
            }
        }

        public void Dispose()
        {
            Stop();
            Process?.Dispose();
            Process = null;
            NotPaused.Dispose();
        }
    }
}

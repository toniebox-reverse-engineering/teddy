using System;
using System.IO;
using System.Threading;

namespace TeddyBench.Core
{
    /// <summary>
    /// Polls for Toniebox SD cards (via <see cref="ICardDetector"/>) and watches the opened directory.
    /// Events are raised on the monitor thread.
    /// </summary>
    public class CardMonitor
    {
        private readonly ICardDetector Detector;
        private readonly int IntervalMs;
        private SafeThread Thread;
        private volatile bool StopRequested;
        private volatile string _CurrentDirectory;

        /// <summary>Raised on every poll for every card found (path of its CONTENT directory).</summary>
        public event Action<string> CardFound;

        /// <summary>Raised once when <see cref="CurrentDirectory"/> disappears (card removed); CurrentDirectory is reset to null.</summary>
        public event Action CurrentDirectoryLost;

        /// <summary>The directory opened in the UI, watched for removal. null if none.</summary>
        public string CurrentDirectory
        {
            get => _CurrentDirectory;
            set => _CurrentDirectory = value;
        }

        public CardMonitor(ICardDetector detector = null, int intervalMs = 500)
        {
            Detector = detector ?? PlatformServices.CardDetector;
            IntervalMs = intervalMs;
        }

        public void Start()
        {
            if (Thread != null)
            {
                return;
            }
            StopRequested = false;
            Thread = new SafeThread(Run, "ScanCardThread");
            Thread.Start();
        }

        public void Stop()
        {
            if (Thread == null)
            {
                return;
            }
            StopRequested = true;
            if (!Thread.Join(IntervalMs + 500))
            {
                Thread.Abort();
            }
            Thread = null;
        }

        /// <summary>One detection pass (public for tests).</summary>
        public void Poll()
        {
            try
            {
                foreach (var content in Detector.FindContentDirectories())
                {
                    CardFound?.Invoke(content);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.eLogLevel.Debug, "[CardMonitor] detection failed: " + ex.Message);
            }

            string current = _CurrentDirectory;
            if (current != null && !DirectoryExists(current))
            {
                _CurrentDirectory = null;
                CurrentDirectoryLost?.Invoke();
            }
        }

        private void Run()
        {
            while (!StopRequested)
            {
                Poll();
                System.Threading.Thread.Sleep(IntervalMs);
            }
        }

        private static bool DirectoryExists(string dir)
        {
            try
            {
                return new DirectoryInfo(dir).Exists;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}

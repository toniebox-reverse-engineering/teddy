using System;
using System.Threading;

namespace TeddyBench.Core
{
    /// <summary>
    /// Thread wrapper that reports unhandled exceptions through <see cref="UnhandledException"/>
    /// instead of tearing down the process.
    /// </summary>
    public class SafeThread
    {
        private ThreadStart ThreadStart;
        private Thread Thread;

        /// <summary>Raised (on the failing thread) with the thread name and the exception.</summary>
        public static event Action<string, Exception> UnhandledException;

        public string Name => Thread.Name;

        public SafeThread(ThreadStart start, string name)
        {
            ThreadStart = start;
            Thread = new Thread(ThreadMain);
            Thread.Name = name;
            /* never keep the process alive because of a worker that did not react to its stop flag */
            Thread.IsBackground = true;
        }

        private void ThreadMain()
        {
            try
            {
                ThreadStart.Invoke();
            }
            catch (ThreadAbortException ex)
            {
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.eLogLevel.Error, "[SafeThread] Unhandled exception in '" + Thread.Name + "': " + ex);
                UnhandledException?.Invoke(Thread.Name, ex);
            }
        }

        public void Start()
        {
            Thread.Start();
        }

        /// <summary>
        /// Last resort for a thread that did not stop cooperatively. Thread.Abort only exists on
        /// .NET Framework; on .NET (Core) it throws PlatformNotSupportedException. There the thread is
        /// left running in the background (it is a background thread, so it does not block exit).
        /// </summary>
        public void Abort()
        {
            if (!Thread.IsAlive)
            {
                return;
            }

            try
            {
                Thread.Abort();
            }
            catch (PlatformNotSupportedException)
            {
                Logger.Log(Logger.eLogLevel.Debug, "[SafeThread] '" + Thread.Name + "' did not stop in time, abandoning it (Thread.Abort not supported)");
            }
        }

        public bool Join(int timeoutMs)
        {
            return Thread.Join(timeoutMs);
        }
    }
}

using System;
using System.Text;

namespace TeddyBench.Core
{
    /// <summary>
    /// Application wide log, independent of any UI. A log view (e.g. the WinForms LogWindow)
    /// polls <see cref="LastUpdate"/>/<see cref="GetText"/> or subscribes to <see cref="MessageLogged"/>.
    /// </summary>
    public static class Logger
    {
        public enum eLogLevel
        {
            DebugVerbose = 0,
            Debug = 1,
            Information = 2,
            Warning = 3,
            Error = 4
        }

        private static readonly object Lock = new object();
        private static readonly StringBuilder Buffer = new StringBuilder();

        /// <summary>Messages below this level are dropped.</summary>
        public static eLogLevel LogLevel { get; set; } = eLogLevel.Warning;

        /// <summary>Time of the last accepted message (DateTime.MinValue if none).</summary>
        public static DateTime LastUpdate { get; private set; } = DateTime.MinValue;

        /// <summary>Raised for every accepted message, on the thread that logged it.</summary>
        public static event Action<eLogLevel, string> MessageLogged;

        public static void Log(eLogLevel level, string message)
        {
            if (level < LogLevel)
            {
                return;
            }

            lock (Lock)
            {
                Buffer.AppendLine(message);
                LastUpdate = DateTime.Now;
            }

            MessageLogged?.Invoke(level, message);
        }

        /// <summary>The complete log so far.</summary>
        public static string GetText()
        {
            lock (Lock)
            {
                return Buffer.ToString();
            }
        }

        public static void Clear()
        {
            lock (Lock)
            {
                Buffer.Clear();
                LastUpdate = DateTime.Now;
            }
        }
    }
}

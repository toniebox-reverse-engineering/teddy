using System;
using System.Threading;

namespace TeddyBench
{
    internal class SafeThread
    {
        private ThreadStart ThreadStart;
        private Thread Thread;

        public SafeThread(ThreadStart start, string name)
        {
            ThreadStart = start;
            Thread = new Thread(ThreadMain);
            Thread.Name = name;
        }

        private void ThreadMain()
        {
            try
            {
                ThreadStart.Invoke();
            }
            catch (Exception ex)
            {
                Program.MainClass.ReportException(Thread.Name, ex);
            }
        }

        internal void Start()
        {
            Thread.Start();
        }

        internal void Abort()
        {
            // Thread.Abort() is not supported in .NET Core/.NET 5+
            // Threads should stop gracefully via their stop flags
            // If thread doesn't stop within Join() timeout, it will be terminated when process exits
        }

        internal bool Join(int v)
        {
            return Thread.Join(v);
        }
    }
}

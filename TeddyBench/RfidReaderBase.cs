using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TeddyBench.Proxmark3;

namespace TeddyBench
{
    public abstract class RfidReaderBase
    {
        [Flags]
        public enum eDeviceInfo
        {
            None = 0,
            BootromPresent = 1,
            OsImagePresent = 2,
            ModeBootrom = 4,
            ModeOs = 8,
            UnderstandStartFlash = 16,
            UnderstandVersion = 32
        }

        internal SafeThread ScanThread = null;
        protected bool ExitScanThread = false;
        internal SafeThread ConsoleThread = null;
        protected bool ExitConsoleThread = false;
        protected object ReaderLock = new object();

        public string CurrentUidString = null;

        protected Dictionary<string, DateTime> PortsFailed = new Dictionary<string, DateTime>();
        protected Dictionary<string, DateTime> PortsAppeared = new Dictionary<string, DateTime>();

        public event EventHandler<string> UidFound;
        public event EventHandler<string> DeviceFound;
        public event EventHandler<FlashRequestContext> FlashRequest;
        public event EventHandler<bool> FlashResult;

        //The event-invoking method that derived classes can override.
        protected virtual void OnUidFound(string e)
        {
            // Safely raise the event for all subscribers
            UidFound?.Invoke(this, e);
        }
        protected virtual void OnDeviceFound(string e)
        {
            // Safely raise the event for all subscribers
            DeviceFound?.Invoke(this, e);
        }
        protected virtual void OnFlashRequest(FlashRequestContext e)
        {
            // Safely raise the event for all subscribers
            FlashRequest?.Invoke(this, e);
        }
        protected virtual void OnFlashResult(bool e)
        {
            // Safely raise the event for all subscribers
            FlashResult?.Invoke(this, e);
        }

        public SerialPort Port { get; protected set; }
        public string CurrentPort { get; protected set; }
        public eDeviceInfo DeviceInfo = eDeviceInfo.None;

        public string HardwareType = "";
        public bool UnlockSupported = false;
        public float AntennaVoltage = 0.0f;
        public bool Connected = false;

        public void Start()
        {
            StartThread();
        }

        protected void StartThread()
        {
            if (ScanThread == null)
            {
                ExitScanThread = false;
                ScanThread = new SafeThread(ScanThreadFunc, "Pn5180Esp Scan Thread");
                ScanThread.Start();
            }
        }

        public void Stop()
        {
            StopThread();

            Close();

            OnDeviceFound(null);
        }

        public void StopThread()
        {
            if (ScanThread != null)
            {
                LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] Trying to stop thread");
                ExitScanThread = true;

                if (!ScanThread.Join(1000))
                {
                    LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] Trying to abort thread");
                    ScanThread.Abort();
                }
                ScanThread = null;
            }
        }

        protected void Close()
        {
            lock (this)
            {
                if (Port != null)
                {
                    try
                    {
                        Flush(Port);
                    }
                    catch (Exception ex)
                    {
                    }

                    try
                    {
                        Port.Close();
                    }
                    catch (Exception ex)
                    {
                    }
                    Port.Dispose();
                    Port = null;
                    CurrentPort = null;
                }
                Connected = false;
                OnDeviceFound(CurrentPort);
            }
        }

        protected void Flush(SerialPort p)
        {
            if (p.BytesToRead > 0)
            {
                LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] Flush: " + p.BytesToRead + " bytes to flush");
            }
            while (p.BytesToRead > 0)
            {
                p.ReadByte();
            }
        }

        internal abstract void ScanThreadFunc();

        internal virtual void EnterConsole() { }
        internal virtual void ExitConsole() { }

        internal virtual byte[] ReadMemory()
        {
            return null;
        }

        internal virtual void EmulateTag(byte[] data) { }

        public virtual void EnterBootloader(string fileName) { }
        internal virtual MeasurementResult MeasureAntenna(eMeasurementType type = eMeasurementType.Both)
        {
            return null;
        }
    }
}

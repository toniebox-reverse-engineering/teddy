using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TeddyBench
{
    public class Pn5180Esp : RfidReaderBase
    {
        public Pn5180Esp()
        {
            LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] new instance");
        }

        internal override void ScanThreadFunc()
        {
            lock (ReaderLock)
            {
                while (true)
                {
                    if (Port == null)
                    {
                        if (ExitScanThread)
                        {
                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: Exiting");
                            return;
                        }
                        try
                        {
                            if (!Detect())
                            {
                                Thread.Sleep(250);
                            }
                        }
                        catch (Exception ex)
                        {
                            Close();
                            Thread.Sleep(250);
                        }
                    }
                    else
                    {
                        try
                        {
                            if (ExitScanThread)
                            {
                                LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: Exiting");
                                return;
                            }
                            Flush(Port);

                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: Tag detected, identifying");

                            String uid = GetUID();

                            /* no UID detected? might be locked */
                            if (uid == "")
                            {
                                    LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: Tag in privacy mode, try to unlock");

                                    /* try to unlock using the common passwords */
                                    if (UnlockTag(0x0F0F0F0F) || UnlockTag(0x7FFD6E5B))
                                    {
                                        LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: Unlocked tag");
                                        uid = GetUID();
                                        if (uid == null)
                                        {
                                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: But it did still not respond to an INVENTORY command?!");
                                            Thread.Sleep(100);
                                            continue;
                                        }
                                    }
                            }

                            /* exit as quickly as possible if requested */
                            if (ExitScanThread)
                            {
                                LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: Exiting");
                                Flush(Port);
                                return;
                            }

                            string uidString = uid; //UIDToString(uid);

                            /* found a new one? print a log message */
                            if (uidString != null)
                            {
                                LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] Report tag UID: " + uidString);
                            }
                            else
                            {
                                LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] Report removed tag");
                            }
                            CurrentUidString = uidString;

                            /* notify listeners about currently detected UID */
                            if (uidString != "")
                            {
                                OnUidFound(uidString);
                            }

                            while (uidString != "" && GetUID() != "")
                            {
                                /* exit as quickly as possible if requested */
                                if (ExitScanThread)
                                {
                                    LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: Exiting");
                                    Flush(Port);
                                    return;
                                }
                                Thread.Sleep(100);
                            }
                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: Tag disappeared");

                        }
                        catch (InvalidOperationException ex2)
                        {
                            if (ex2.TargetSite.DeclaringType.Name == "SerialPort")
                            {
                                LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: Serial port lost. Closing device.");
                            }
                            else
                            {
                                LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: Device failed. Closing. (" + ex2.ToString() + ")");
                            }
                            Close();
                        }
                        catch (Exception ex3)
                        {
                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] MainFunc: Device failed. Closing. (" + ex3.ToString() + ")");
                            Close();
                        }
                    }
                    Thread.Sleep(50);
                }
            }
        }

        private bool UnlockTag(uint pass)
        {
            int reason = 0;
            return UnlockTag(pass, ref reason);
        }

        private bool UnlockTag(uint pass, ref int reason)
        {
            if (Port == null)
            {
                return false;
            }

            Port.WriteLine("u");
            String response = Port.ReadLine();
            response.Replace("\r", "");

            if (response == "ok")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #region Send ISO15693 commands

        private String GetUID(int tries = 1)
        {
            for (int tried = 0; tried < tries; tried++)
            {

                Port.WriteLine("i");
                String response = Port.ReadLine();
                response = response.Replace("\r", "");

                if (response == "" || response.Length != 16)
                {
                    LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] GetUID: Failed (" + ((response == "") ? "no resp" : (response.Length + " bytes")) + ")");
                    continue;
                }

                return response;
            }
            return "";
        }

        #endregion

        public bool Detect()
        {
            string[] ports = SerialPort.GetPortNames();
            List<string> reallyAvailablePorts = new List<string>();

            /* SerialPort.GetPortNames() also returns stale ports from registry. do a check if they are really available. */
            foreach (string listedPort in ports)
            {
                try
                {
                    SerialPort p = new SerialPort(listedPort, 115200);

                    p.Open();
                    p.Close();
                    reallyAvailablePorts.Add(listedPort);
                }
                catch (Exception e0)
                {
                }
            }

            /* update seen list */
            foreach (string newPort in reallyAvailablePorts)
            {
                if (!PortsAppeared.ContainsKey(newPort))
                {
                    PortsAppeared.Add(newPort, DateTime.Now);
                    LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] New port: " + newPort);
                }
            }

            /* remove failed ports when they disappear */
            foreach (string del in PortsFailed.Keys.Where(p => !reallyAvailablePorts.Contains(p)).ToArray())
            {
                PortsFailed.Remove(del);
                PortsAppeared.Remove(del);
                LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] Port disappeared: " + del);
            }

            /* only try to connect to ports that have been seen more than a second ago. Ensure Pn5180Esp has booted properly. */
            foreach (string port in reallyAvailablePorts.Where(p => (DateTime.Now - PortsAppeared[p]).TotalMilliseconds > 1000))
            {
                /* retry failed ports only every 60 seconds */
                if (PortsFailed.ContainsKey(port) && (DateTime.Now - PortsFailed[port]).TotalSeconds < 60)
                {
                    continue;
                }

                LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] Try port " + port);
                if (TryOpen(port))
                {
                    LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] Success");
                    return true;
                }

                LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] Failed");
                if (!PortsFailed.ContainsKey(port))
                {
                    PortsFailed.Add(port, DateTime.Now);
                }
                PortsFailed[port] = DateTime.Now;
            }

            return false;
        }

        private bool TryOpen(string port)
        {
            SerialPort p = null;

            try
            {
                p = new SerialPort(port, 115200);
                p.ReadTimeout = 500;

                p.Open();
                //Thread.Sleep(500);
                Flush(p);

                /* read version */
                p.WriteLine("v");
                String response = p.ReadLine();
                response.Replace("\r", "");

                if(!response.Contains("Pn5180Esp"))
                {
                    p.Close();
                    return false;
                }

                CurrentPort = port;
                Port = p;

                OnDeviceFound(CurrentPort);
                LogWindow.Log(LogWindow.eLogLevel.Debug, "[Pn5180Esp] TryOpen: " + port + " successfully opened");

                Connected = true;
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    if (p != null && p.IsOpen)
                    {
                        p.Close();
                    }
                }
                catch (Exception e)
                {
                }
            }

            return false;
        }
    }
}
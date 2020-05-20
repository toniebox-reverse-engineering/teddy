﻿using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TeddyBench
{
    public class Proxmark3
    {
        public class Pm3UsbCommand
        {
            public Pm3UsbCommandStruct data = new Pm3UsbCommandStruct();

            public Pm3UsbCommand(ulong cmd = 0, ulong arg0 = 0, ulong arg1 = 0, ulong arg2 = 0, byte[] payload = null)
            {
                data = new Pm3UsbCommandStruct();
                data.arg = new ulong[3];
                data.d = new byte[512];

                data.cmd = cmd;
                data.arg[0] = arg0;
                data.arg[1] = arg1;
                data.arg[2] = arg2;
                SetData(payload);
            }

            public byte[] ToByteArray()
            {
                return StructureToByteArray(data);
            }

            internal void SetData(byte[] payload)
            {
                if (payload != null && payload.Length > 0)
                {
                    Array.Copy(payload, data.d, payload.Length);
                }
            }

            internal void Write(SerialPort p)
            {
                byte[] buf = ToByteArray();
                p.Write(buf, 0, buf.Length);
            }
        }

        public class Pm3UsbResponse
        {
            public enum eResponseType
            {
                NACK = 0xFE,
                ACK = 0xFF,
                DebugString = 0x100,
                DebugInteger = 0x101,
                DebugBytes = 0x102,
                NoData = 0x7FFE,
                Timeout = 0x7FFF
            }

            public Pm3UsbResponseStruct data;
            private SerialPort p;

            public bool VarSize => (data.cmd & 0x8000) != 0;
            public eResponseType Cmd => (eResponseType)(data.cmd & ~0x8000);
            public int Length => (int)(2 + 2 + 3 * 4 + data.dataLen);

            public Pm3UsbResponse(byte[] buf, int offset, int length)
            {
                ByteArrayToStructure(buf, offset, ref data);
            }

            public Pm3UsbResponse(SerialPort p, int waitTime = 0)
            {
                try
                {
                    int loops = 0;
                    while ((p.BytesToRead < 16) && ((loops++ * 10) < waitTime))
                    {
                        Thread.Sleep(10);
                    }
                    if (p.BytesToRead < 16)
                    {
                        data.cmd = 0x7FFE;
                        return;
                    }

                    byte[] buf = new byte[512 + 16];

                    if (p.Read(buf, 0, 16) != 16)
                    {
                        return;
                    }
                    ushort dataLen = 0;

                    ByteArrayToStructure(buf, 0, ref data);

                    if (VarSize)
                    {
                        dataLen = data.dataLen;
                    }
                    else
                    {
                        dataLen = 512;
                    }

                    if (dataLen > 0)
                    {
                        if (p.Read(buf, 16, dataLen) != dataLen)
                        {
                            return;
                        }
                    }
                    ByteArrayToStructure(buf, 0, ref data);
                }
                catch (TimeoutException ex)
                {
                    data.cmd = 0x7FFF;
                    return;
                }
            }
        }

        public struct Pm3UsbCommandStruct
        {
            public ulong cmd;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public ulong[] arg;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] d;
        };

        public struct Pm3UsbResponseStruct
        {
            public ushort cmd;
            public ushort dataLen;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public uint[] arg;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] d;
        };

        private static byte[] StructureToByteArray(object obj)
        {
            int len = Marshal.SizeOf(obj);
            byte[] arr = new byte[len];
            IntPtr ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, arr, 0, len);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }

        private static void ByteArrayToStructure<T>(byte[] bytearray, int offset, ref T obj)
        {
            int len = Marshal.SizeOf(obj);
            IntPtr i = Marshal.AllocHGlobal(len);
            Marshal.Copy(bytearray, offset, i, len);
            obj = (T)Marshal.PtrToStructure(i, typeof(T));
            Marshal.FreeHGlobal(i);
        }
        public string CurrentPort { get; private set; }
        public SerialPort Port { get; private set; }
        public event EventHandler<string> UidFound;
        public event EventHandler<string> DeviceFound;
        public string CurrentUid = null;
        private Thread Pm3Thread = null;
        private bool ExitThread = false;

        private Dictionary<string, DateTime> PortsFailed = new Dictionary<string, DateTime>();
        private Dictionary<string, DateTime> PortsAppeared = new Dictionary<string, DateTime>();
        private bool UnlockSupported = false;

        public void StartThreads()
        {
            Pm3Thread = new Thread(MainFunc);
            Pm3Thread.Start();
        }

        public void StopThreads()
        {
            ExitThread = true;
            if (!Pm3Thread.Join(2000))
            {
                Pm3Thread.Abort();
            }
            Pm3Thread = null;

            Close();
        }

        private void MainFunc()
        {
            while (true)
            {
                if (Port == null)
                {
                    if (ExitThread)
                    {
                        LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] MainFunc: Exiting");
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
                        string uid = GetUID();

                        if (ExitThread)
                        {
                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] MainFunc: Exiting");
                            Flush(Port);
                            return;
                        }

                        if (uid == null)
                        {
                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] MainFunc: No tag detected, try to unlock");
                            if (UnlockTag(0x0F0F0F0F) || UnlockTag(0x7FFD6E5B))
                            {
                                LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] MainFunc: Unlocked tag");
                                uid = GetUID();
                            }
                        }

                        if (ExitThread)
                        {
                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] MainFunc: Exiting");
                            Flush(Port);
                            return;
                        }

                        UidFound?.Invoke(this, uid);

                        if (uid != null && CurrentUid != uid)
                        {
                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] Detected tag UID: " + uid);
                        }

                        CurrentUid = uid;
                    }
                    catch (Exception ex2)
                    {
                        LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] MainFunc: Device failed. Closing");
                        Close();
                    }

                    Thread.Sleep(250);
                }
            }
        }

        private void Close()
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
            DeviceFound?.Invoke(this, CurrentPort);
        }

        private bool UnlockTag(uint pass)
        {
            int reason = 0;
            return UnlockTag(pass, ref reason);
        }

        private bool UnlockTag(uint pass, ref int reason)
        {
            if(!UnlockSupported)
            {
                return false;
            }
            Pm3UsbCommand cmd = new Pm3UsbCommand(0x319, pass);

            LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] UnlockTag: Send " + BitConverter.ToString(cmd.ToByteArray()).Replace("-", ""));
            Flush(Port);
            cmd.Write(Port);

            while (true)
            {
                Pm3UsbResponse response = new Pm3UsbResponse(Port, 1000);

                switch (response.Cmd)
                {
                    case Pm3UsbResponse.eResponseType.DebugString:
                        {
                            string debugStr = Encoding.UTF8.GetString(response.data.d, 0, response.data.dataLen);
                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] UnlockTag:   String '" + debugStr + "'");
                            break;
                        }

                    case Pm3UsbResponse.eResponseType.ACK:
                        reason = 0;
                        LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] UnlockTag: ACK");
                        return true;

                    case Pm3UsbResponse.eResponseType.NACK:
                        reason = 1 + (int)response.data.arg[0];
                        LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] UnlockTag: NACK (reason: " + reason + ")");
                        return false;
                
                    case Pm3UsbResponse.eResponseType.NoData:
                    case Pm3UsbResponse.eResponseType.Timeout:
                        reason = -1;
                        LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] UnlockTag: timeout, returning");
                        return false;

                    default:
                        LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] UnlockTag: Unhandled: " + response.Cmd);
                        break;
                }
            }
        }

        private string GetUID()
        {
            byte[] cmdIdentify = new byte[] { 0x26, 0x01, 0x00, 0xF6, 0x0A };
            Pm3UsbCommand cmd = new Pm3UsbCommand(0x313, (byte)cmdIdentify.Length, 1, 1, cmdIdentify);

            LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] GetUID: Send " + BitConverter.ToString(cmd.ToByteArray()).Replace("-", ""));
            Flush(Port);
            cmd.Write(Port);

            while (true)
            {
                Pm3UsbResponse response = new Pm3UsbResponse(Port, 500);

                switch (response.Cmd)
                {
                    case Pm3UsbResponse.eResponseType.DebugString:
                        {
                            string debugStr = Encoding.UTF8.GetString(response.data.d, 0, response.data.dataLen);
                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] GetUID: DebugMessage '" + debugStr + "'");
                            break;
                        }

                    case Pm3UsbResponse.eResponseType.NoData:
                    case Pm3UsbResponse.eResponseType.Timeout:
                        LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] GetUID: timeout, returning");
                        return null;

                    case Pm3UsbResponse.eResponseType.ACK:
                        if (response.data.arg[0] == 12)
                        {
                            byte[] uidBytes = new byte[8];
                            Array.Copy(response.data.d, 2, uidBytes, 0, 8);

                            string uid = BitConverter.ToString(uidBytes.Reverse().ToArray()).Replace("-", "");
                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] GetUID: ACK, returning " + uid + "");
                            return uid;
                        }
                        else
                        {
                            LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] GetUID: no tag found, returning");
                            return null;
                        }

                    default:
                        LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] GetUID: Unhandled: " + response.Cmd);
                        break;
                }
            }
        }

        private void Flush(SerialPort p)
        {
            while (p.BytesToRead > 0)
            {
                p.ReadByte();
            }
        }

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
                catch(Exception e0)
                {
                }
            }

            /* update seen list */
            foreach(string newPort in reallyAvailablePorts)
            {
                if(!PortsAppeared.ContainsKey(newPort))
                {
                    PortsAppeared.Add(newPort, DateTime.Now);
                    LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] New port: " + newPort);
                }
            }

            /* remove failed ports when they disappear */
            foreach (string del in PortsFailed.Keys.Where(p => !reallyAvailablePorts.Contains(p)).ToArray())
            {
                PortsFailed.Remove(del);
                PortsAppeared.Remove(del);
                LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] Port disappeared: " + del);
            }

            /* only try to connect to ports that have been seen more than a second ago. Ensure PM3 has booted properly. */
            foreach(string port in reallyAvailablePorts.Where(p=> (DateTime.Now - PortsAppeared[p]).TotalMilliseconds > 1000))
            {
                /* retry failed ports only every 60 seconds */
                if(PortsFailed.ContainsKey(port) && (DateTime.Now - PortsFailed[port]).TotalSeconds < 60)
                {
                    continue;
                }

                LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] Try port " + port);
                if (TryOpen(port))
                {
                    LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] Success");
                    return true;
                }

                LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] Failed");
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
            SerialPort p = new SerialPort(port, 115200);
            try
            {
                p = new SerialPort(port, 115200);
                p.ReadTimeout = 500;

                p.Open();
                Flush(p);

                Pm3UsbCommand cmdPing = new Pm3UsbCommand(0x109);
                cmdPing.Write(p);
                Pm3UsbResponse resPing = new Pm3UsbResponse(p, 100);

                if (resPing.Cmd != Pm3UsbResponse.eResponseType.ACK)
                {
                    LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] TryOpen: "+ port + " did not reply to a ping");
                    p.Close();
                    return false;
                }

                /* read version */
                Pm3UsbCommand cmdVers = new Pm3UsbCommand(0x107);
                cmdVers.Write(p);
                Pm3UsbResponse resVers = new Pm3UsbResponse(p, 300);
                if (resVers.Cmd != Pm3UsbResponse.eResponseType.ACK)
                {
                    LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] TryOpen: " + port + " did not reply to a version query");
                    p.Close();
                    return false;
                }

                LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] Chip ID: 0x" + resVers.data.arg[0].ToString("X8"));
                LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] Flash:   0x" + resVers.data.arg[1].ToString("X8"));
                LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] Caps:    0x" + resVers.data.arg[2].ToString("X8"));
                string versionString = Encoding.UTF8.GetString(resVers.data.d, 0, resVers.data.dataLen - 1);
                LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] Version:");
                foreach (string line in versionString.Split('\n'))
                {
                    LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3]     " + line);
                }

                CurrentPort = port;
                Port = p;

                /* does it support the unlock command? */
                int reason = 0;
                UnlockSupported = true;
                UnlockTag(0xDEADBEEF, ref reason);

                /* if not supported, the command will simply time out */
                UnlockSupported = reason > 0;

                DeviceFound?.Invoke(this, CurrentPort);
                LogWindow.Log(LogWindow.eLogLevel.Debug, "[PM3] TryOpen: " + port + " successfully opened");
                return true;
            }
            catch(Exception ex)
            {
                try
                {
                    if (p != null && p.IsOpen)
                    {
                        p.Close();
                    }
                }
                catch(Exception e)
                {
                }
            }

            return false;
        }
    }
}
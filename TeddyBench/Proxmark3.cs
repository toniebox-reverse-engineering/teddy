using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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

            public Pm3UsbCommand()
            {
                data = new Pm3UsbCommandStruct();
                data.arg = new ulong[3];
                data.d = new byte[512];
            }

            public byte[] ToByteArray()
            {
                return StructureToByteArray(data);
            }

            internal void SetData(byte[] payload)
            {
                Array.Copy(payload, data.d, payload.Length);
            }
        }

        public class Pm3UsbResponse
        {
            public enum eResponseType
            {
                NoData = 0x7FFE,
                Timeout = 0x7FFF,
                ACK = 0xFF,
                DebugString = 0x100,
                DebugInteger = 0x101,
                DebugBytes = 0x102
            }

            public Pm3UsbResponseStruct data;
            private SerialPort p;

            public bool VarSize => (data.cmd & 0x8000) != 0;
            public eResponseType Cmd => (eResponseType)(data.cmd & ~0x8000);
            public int Length => (int)(2 + 2 + 3*4 + data.dataLen);

            public Pm3UsbResponse(byte[] buf, int offset, int length)
            {
                ByteArrayToStructure(buf, offset, ref data);
            }

            public Pm3UsbResponse(SerialPort p)
            {
                try
                {
                    if(p.BytesToRead < 16)
                    {
                        data.cmd = 0x7FFE;
                        return;
                    }

                    byte[] buf = new byte[512+16];

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
            while (!ExitThread)
            {
                if (Port == null)
                {
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
                            return;
                        }

                        if (uid == null)
                        {
                            if (UnlockTag(0x0F0F0F0F) || UnlockTag(0x7FFD6E5B))
                            {
                                Console.WriteLine("Unlocked tag");
                                uid = GetUID();
                            }
                        }

                        if (ExitThread)
                        {
                            return;
                        }

                        UidFound?.Invoke(this, uid);

                        if (uid != null && CurrentUid != uid)
                        {
                            CurrentUid = uid;
                            Console.WriteLine("Detected tag UID: " + uid);
                        }

                        CurrentUid = uid;
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine("Device failed. Closing");
                        Close();
                    }

                    Thread.Sleep(250);
                }
            }
        }

        private void Close()
        {
            if(Port != null)
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
            Pm3UsbCommand cmd = new Pm3UsbCommand();

            cmd.data.cmd = 0x319;
            cmd.data.arg[0] = pass;
            cmd.data.arg[1] = 0;
            cmd.data.arg[2] = 0;

            Console.WriteLine("UnlockTag: sending");
            Flush(Port);
            byte[] data = cmd.ToByteArray();
            Port.Write(data, 0, data.Length);

            while (true)
            {
                Pm3UsbResponse response = new Pm3UsbResponse(Port);

                switch (response.Cmd)
                {
                    case Pm3UsbResponse.eResponseType.DebugString:
                        {
                            string debugStr = ASCIIEncoding.UTF8.GetString(response.data.d, 0, response.data.dataLen);
                            Console.WriteLine("Debug: " + debugStr);

                            if (debugStr.Contains("No tag found"))
                            {
                                Console.WriteLine("UnlockTag: returning");
                                return false;
                            }
                            if (debugStr.Contains("Password was not accepted"))
                            {
                                Console.WriteLine("UnlockTag: returning");
                                return false;
                            }
                            if (debugStr.Contains("Success"))
                            {
                                Console.WriteLine("UnlockTag: found tag");
                            }
                            break;
                        }

                    case Pm3UsbResponse.eResponseType.NoData:
                        break;

                    case Pm3UsbResponse.eResponseType.ACK:
                        Console.WriteLine("UnlockTag: ACK, returning");
                        return true;

                    case Pm3UsbResponse.eResponseType.Timeout:
                        Console.WriteLine("UnlockTag: Timeout, returning");
                        return false;

                    default:
                        Console.WriteLine("Unhandled: " + response.Cmd);
                        break;
                }
            }
        }

        private string GetUID()
        {
            Pm3UsbCommand cmd = new Pm3UsbCommand();
            byte[] cmdIdentify = new byte[] { 0x26, 0x01, 0x00, 0xF6, 0x0A  };

            cmd.data.cmd = 0x313;
            cmd.data.arg[0] = (byte)cmdIdentify.Length;
            cmd.data.arg[1] = 1;
            cmd.data.arg[2] = 1;
            cmd.SetData(cmdIdentify);

            Console.WriteLine("GetUID: sending");

            Flush(Port);
            byte[] data = cmd.ToByteArray();
            Port.Write(data, 0, data.Length);

            while (true)
            {
                Pm3UsbResponse response = new Pm3UsbResponse(Port);

                switch (response.Cmd)
                {
                    case Pm3UsbResponse.eResponseType.DebugString:
                        {
                            string debugStr = ASCIIEncoding.UTF8.GetString(response.data.d, 0, response.data.dataLen);
                            Console.WriteLine("Debug: " + debugStr);
                            break;
                        }

                    case Pm3UsbResponse.eResponseType.NoData:
                        break;

                    case Pm3UsbResponse.eResponseType.Timeout:
                        Console.WriteLine("GetUID: timeout, returning");
                        return null;

                    case Pm3UsbResponse.eResponseType.ACK:
                        if (response.data.arg[0] == 12)
                        {
                            byte[] uidBytes = new byte[8];
                            Array.Copy(response.data.d, 2, uidBytes, 0, 8);

                            string uid = BitConverter.ToString(uidBytes.Reverse().ToArray()).Replace("-", "");
                            Console.WriteLine("GetUID: ACK, returning " + uid + "");
                            return uid;
                        }
                        else
                        {
                            Console.WriteLine("GetUID: no response, returning");
                            return null;
                        }

                    default:
                        Console.WriteLine("Unhandled: " + response.Cmd);
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

        private Dictionary<string, DateTime> PortsFailed = new Dictionary<string, DateTime>();
        private Dictionary<string, DateTime> PortsAppeared = new Dictionary<string, DateTime>();

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
                }
            }

            /* remove failed ports when they disappear */
            foreach (string del in PortsFailed.Keys.Where(p => !reallyAvailablePorts.Contains(p)).ToArray())
            {
                PortsFailed.Remove(del);
                PortsAppeared.Remove(del);
            }

            /* only try to connect to ports that have been seen more than a second ago. Ensure PM3 has boted properly. */
            foreach(string port in reallyAvailablePorts.Where(p=> (DateTime.Now - PortsAppeared[p]).TotalMilliseconds > 1000))
            {
                /* retry failed ports only every 60 seconds */
                if(PortsFailed.ContainsKey(port) && (DateTime.Now - PortsFailed[port]).TotalSeconds < 60)
                {
                    continue;
                }

                if(TryOpen(port))
                {
                    return true;
                }

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

                Pm3UsbCommand cmd = new Pm3UsbCommand();

                cmd.data.cmd = 0x109;
                cmd.data.arg[0] = 0;
                cmd.data.arg[1] = 0;
                cmd.data.arg[2] = 0;

                Flush(p);
                byte[] data = cmd.ToByteArray();
                p.Write(data, 0, data.Length);

                Pm3UsbResponse response = new Pm3UsbResponse(p);

                if (response.Cmd == Pm3UsbResponse.eResponseType.ACK)
                {
                    CurrentPort = port;
                    Port = p;
                    DeviceFound?.Invoke(this, CurrentPort);
                    return true;
                }

                p.Close();
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

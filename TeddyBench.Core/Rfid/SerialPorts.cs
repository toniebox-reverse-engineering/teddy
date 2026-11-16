using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;

namespace TeddyBench.Core
{
    /// <summary>Finds serial ports an NFC reader could be connected to.</summary>
    public static class SerialPorts
    {
        private static readonly object Lock = new object();
        private static readonly HashSet<string> Denied = new HashSet<string>();

        /// <summary>Lists port names (default: <see cref="SerialPort.GetPortNames"/>); replaceable for tests.</summary>
        public static Func<string[]> ListPorts { get; set; } = SerialPort.GetPortNames;

        /// <summary>Opens and closes a port to check it is usable; replaceable for tests.</summary>
        public static Action<string> Probe { get; set; } = port =>
        {
            using (var p = new SerialPort(port, 115200))
            {
                p.Open();
                p.Close();
            }
        };

        /// <summary>Ports that exist but could not be opened for lack of permission (e.g. not in the "dialout" group).</summary>
        public static string[] PermissionDenied
        {
            get { lock (Lock) { return Denied.OrderBy(p => p).ToArray(); } }
        }

        /// <summary>
        /// Candidate ports that can actually be opened.
        /// On Linux only USB serial devices are considered (ttyACM*: Proxmark3, ttyUSB*: ESP32 boards);
        /// the onboard /dev/ttyS* ports are always listed but never host a reader, and probing them is slow.
        /// </summary>
        public static List<string> GetAvailablePorts()
        {
            var available = new List<string>();
            var denied = new List<string>();

            foreach (string port in Candidates(ListPorts(), RuntimeInformation.IsOSPlatform(OSPlatform.Windows)))
            {
                try
                {
                    /* the list may contain stale entries (Windows registry), so check the port really works */
                    Probe(port);
                    available.Add(port);
                }
                catch (UnauthorizedAccessException)
                {
                    denied.Add(port);
                }
                catch (Exception)
                {
                }
            }

            lock (Lock)
            {
                foreach (string port in denied.Where(p => !Denied.Contains(p)))
                {
                    Logger.Log(Logger.eLogLevel.Warning, "[Serial] No permission to open " + port + ". On Linux add your user to the 'dialout' group (sudo usermod -aG dialout $USER) and log in again.");
                }
                Denied.Clear();
                Denied.UnionWith(denied);
            }
            return available;
        }

        /// <summary>The ports worth probing (public for tests).</summary>
        public static IEnumerable<string> Candidates(IEnumerable<string> ports, bool windows)
        {
            if (windows)
            {
                return ports;
            }
            return ports.Where(p =>
            {
                string name = p.Substring(p.LastIndexOf('/') + 1);
                /* Linux: ttyACM/ttyUSB, macOS: cu./tty.usbmodem and usbserial */
                return name.StartsWith("ttyACM") || name.StartsWith("ttyUSB") || name.Contains("usbmodem") || name.Contains("usbserial");
            });
        }
    }
}

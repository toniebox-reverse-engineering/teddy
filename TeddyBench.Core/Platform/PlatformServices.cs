using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace TeddyBench.Core
{
    /// <summary>Finds mounted Toniebox SD cards.</summary>
    public interface ICardDetector
    {
        /// <summary>Full paths of the CONTENT directories of all currently available cards.</summary>
        IEnumerable<string> FindContentDirectories();
    }

    /// <summary>Desktop integration: browser, file manager.</summary>
    public interface IShell
    {
        void OpenUrl(string url);
        void OpenFolder(string path);
        /// <summary>Opens the file manager with the given file selected (or at least its folder).</summary>
        void RevealFile(string file);
    }

    /// <summary>
    /// The "live" flag of a tonie file. The Toniebox uses the FAT hidden attribute for it.
    /// </summary>
    public interface ILiveFlag
    {
        bool IsLive(string file);
        void SetLive(string file, bool live);
    }

    /// <summary>
    /// Platform specific implementations used by the UI independent code.
    /// The GUI may replace any of them at startup.
    /// </summary>
    public static class PlatformServices
    {
        private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static ICardDetector CardDetector { get; set; } = IsLinux ? (ICardDetector)new MountCardDetector() : new DriveInfoCardDetector();
        public static IShell Shell { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? (IShell)new WindowsShell() : new XdgShell();
        /* Windows: FileAttributes.Hidden; Linux: vfat ioctl; macOS: .NET maps Hidden to chflags */
        public static ILiveFlag LiveFlag { get; set; } = IsLinux ? (ILiveFlag)new FatIoctlLiveFlag() : new FileAttributesLiveFlag();

        /// <summary>Creates the audio output used for previewing tonies. Must be set by the GUI (audio output is not portable in NAudio).</summary>
        public static Func<IWavePlayer> AudioOutputFactory { get; set; }
    }

    /// <summary>Removable drives with a CONTENT directory in their root (works on Windows).</summary>
    public class DriveInfoCardDetector : ICardDetector
    {
        public IEnumerable<string> FindContentDirectories()
        {
            var result = new List<string>();

            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType == DriveType.Removable && drive.IsReady)
                    {
                        var content = drive.RootDirectory.GetDirectories().FirstOrDefault(d => d.Name == "CONTENT");
                        if (content != null)
                        {
                            result.Add(content.FullName);
                        }
                    }
                }
                catch (Exception ex)
                {
                }
            }
            return result;
        }
    }

    /// <summary>The FAT hidden attribute through System.IO (works on Windows).</summary>
    public class FileAttributesLiveFlag : ILiveFlag
    {
        public bool IsLive(string file)
        {
            return new FileInfo(file).Attributes.HasFlag(FileAttributes.Hidden);
        }

        public void SetLive(string file, bool live)
        {
            var info = new FileInfo(file);
            if (live)
            {
                info.Attributes |= FileAttributes.Hidden;
            }
            else
            {
                info.Attributes &= ~FileAttributes.Hidden;
            }
        }
    }

    public class WindowsShell : IShell
    {
        public void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        public void OpenFolder(string path)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\""));
        }

        public void RevealFile(string file)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select, \"" + file + "\""));
        }
    }

    /// <summary>
    /// freedesktop integration: xdg-open (macOS: open), and the FileManager1 D-Bus interface to show a
    /// file selected in the file manager (Nautilus, Dolphin, Nemo, Thunar, ...).
    /// </summary>
    public class XdgShell : IShell
    {
        private static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        private static string Opener => IsMac ? "open" : "xdg-open";

        public void OpenUrl(string url)
        {
            Run(Opener, Quote(url));
        }

        public void OpenFolder(string path)
        {
            Run(Opener, Quote(path));
        }

        public void RevealFile(string file)
        {
            if (IsMac)
            {
                Run("open", "-R " + Quote(file));
                return;
            }

            /* the D-Bus call may take a moment (file manager start): do not block the caller */
            System.Threading.Tasks.Task.Run(() =>
            {
                if (!ShowItemsViaDBus(file))
                {
                    Run(Opener, Quote(Path.GetDirectoryName(file)));
                }
            });
        }

        /// <summary>org.freedesktop.FileManager1.ShowItems(["file://..."], "") via dbus-send.</summary>
        public static bool ShowItemsViaDBus(string file)
        {
            try
            {
                string uri = new Uri(Path.GetFullPath(file)).AbsoluteUri;
                var psi = new ProcessStartInfo("dbus-send",
                    "--session --print-reply --dest=org.freedesktop.FileManager1 --type=method_call " +
                    "/org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems " +
                    Quote("array:string:" + uri) + " " + Quote("string:"))
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var process = Process.Start(psi))
                {
                    if (!process.WaitForExit(5000))
                    {
                        return false;
                    }
                    return process.ExitCode == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Run(string command, string arguments)
        {
            Process.Start(new ProcessStartInfo(command, arguments) { UseShellExecute = false });
        }

        /* netstandard2.0 has no ArgumentList: quote for the .NET argument parser */
        public static string Quote(string argument)
        {
            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }
    }
}

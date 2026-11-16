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
        public static ICardDetector CardDetector { get; set; } = new DriveInfoCardDetector();
        public static IShell Shell { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? (IShell)new WindowsShell() : new XdgShell();
        public static ILiveFlag LiveFlag { get; set; } = new FileAttributesLiveFlag();

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

    /// <summary>Basic freedesktop integration via xdg-open (macOS: open).</summary>
    public class XdgShell : IShell
    {
        private static string Opener => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open";

        public void OpenUrl(string url)
        {
            Start(url);
        }

        public void OpenFolder(string path)
        {
            Start(path);
        }

        public void RevealFile(string file)
        {
            Start(Path.GetDirectoryName(file));
        }

        private static void Start(string target)
        {
            /* netstandard2.0 has no ArgumentList: quote for the .NET argument parser */
            string quoted = "\"" + target.Replace("\"", "\\\"") + "\"";
            Process.Start(new ProcessStartInfo(Opener, quoted) { UseShellExecute = false });
        }
    }
}

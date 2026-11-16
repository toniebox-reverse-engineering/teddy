using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TeddyBench.Core
{
    /// <summary>
    /// Linux: Toniebox SD cards are FAT formatted and mounted by the desktop (udisks) below
    /// /run/media/$USER or /media/$USER. Looks at all FAT mounts for a CONTENT directory.
    /// </summary>
    public class MountCardDetector : ICardDetector
    {
        /* file systems a Toniebox card can have; fuseblk covers FUSE exfat/vfat drivers */
        private static readonly HashSet<string> CardFileSystems = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "vfat", "msdos", "exfat", "fuseblk" };

        private readonly Func<string> ReadMounts;

        /// <param name="readMounts">Content of the mount table; default /proc/self/mounts.</param>
        public MountCardDetector(Func<string> readMounts = null)
        {
            ReadMounts = readMounts ?? (() => File.ReadAllText("/proc/self/mounts"));
        }

        public IEnumerable<string> FindContentDirectories()
        {
            var result = new List<string>();

            foreach (var mount in ParseMounts(ReadMounts()))
            {
                if (!CardFileSystems.Contains(mount.Type))
                {
                    continue;
                }

                try
                {
                    /* vfat may show "CONTENT" in lower case depending on the mount options */
                    string content = Directory.EnumerateDirectories(mount.Point)
                        .FirstOrDefault(d => string.Equals(Path.GetFileName(d), "CONTENT", StringComparison.OrdinalIgnoreCase));
                    if (content != null)
                    {
                        result.Add(content);
                    }
                }
                catch (Exception)
                {
                    /* not readable (e.g. another user's mount) */
                }
            }
            return result;
        }

        public struct Mount
        {
            public string Device;
            public string Point;
            public string Type;
        }

        /// <summary>Parses /proc/mounts lines ("device mountpoint type options 0 0"), decoding \040-style escapes.</summary>
        public static IEnumerable<Mount> ParseMounts(string table)
        {
            foreach (string line in table.Split('\n'))
            {
                string[] fields = line.Split(' ');
                if (fields.Length < 3)
                {
                    continue;
                }
                yield return new Mount { Device = Unescape(fields[0]), Point = Unescape(fields[1]), Type = fields[2] };
            }
        }

        /// <summary>The kernel writes space, tab, newline and backslash as octal escapes (\040, \011, \012, \134).</summary>
        public static string Unescape(string field)
        {
            if (field.IndexOf('\\') < 0)
            {
                return field;
            }

            var bytes = new List<byte>();
            byte[] raw = Encoding.UTF8.GetBytes(field);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == '\\' && i + 3 < raw.Length &&IsOctal(raw[i + 1]) && IsOctal(raw[i + 2]) && IsOctal(raw[i + 3]))
                {
                    bytes.Add((byte)(((raw[i + 1] - '0') << 6) | ((raw[i + 2] - '0') << 3) | (raw[i + 3] - '0')));
                    i += 3;
                }
                else
                {
                    bytes.Add(raw[i]);
                }
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static bool IsOctal(byte b) => b >= '0' && b <= '7';
    }

    /// <summary>
    /// Linux: the "live" flag is the FAT hidden attribute, which .NET cannot access on Linux.
    /// Uses the vfat driver's FAT_IOCTL_GET_ATTRIBUTES / FAT_IOCTL_SET_ATTRIBUTES (what fatattr does).
    /// </summary>
    public class FatIoctlLiveFlag : ILiveFlag
    {
        /* _IOR('r', 0x10, __u32) / _IOW('r', 0x11, __u32) with the generic ioctl encoding (x86, ARM, RISC-V) */
        public const uint FAT_IOCTL_GET_ATTRIBUTES = (2u << 30) | (4u << 16) | ('r' << 8) | 0x10;
        public const uint FAT_IOCTL_SET_ATTRIBUTES = (1u << 30) | (4u << 16) | ('r' << 8) | 0x11;
        public const uint ATTR_HIDDEN = 0x02;

        private const int O_RDONLY = 0;
        private const int EPERM = 1;
        private const int EACCES = 13;
        private const int ENOTTY = 25;

        public bool IsLive(string file)
        {
            uint attributes;
            int error = Ioctl(file, FAT_IOCTL_GET_ATTRIBUTES, out attributes, 0);
            if (error != 0)
            {
                /* not on a FAT file system (e.g. an exported copy): never live */
                return false;
            }
            return (attributes & ATTR_HIDDEN) != 0;
        }

        public void SetLive(string file, bool live)
        {
            uint attributes;
            int error = Ioctl(file, FAT_IOCTL_GET_ATTRIBUTES, out attributes, 0);
            if (error == 0)
            {
                uint wanted = live ? (attributes | ATTR_HIDDEN) : (attributes & ~ATTR_HIDDEN);
                if (wanted == attributes)
                {
                    return;
                }
                error = Ioctl(file, FAT_IOCTL_SET_ATTRIBUTES, out _, wanted);
            }

            switch (error)
            {
                case 0:
                    return;
                case ENOTTY:
                    throw new NotSupportedException("The live flag can only be set on the FAT formatted SD card ('" + file + "' is on another file system).");
                case EPERM:
                case EACCES:
                    throw new UnauthorizedAccessException("No permission to change the attributes of '" + file + "'.");
                default:
                    throw new IOException("Changing the attributes of '" + file + "' failed (errno " + error + ").");
            }
        }

        /// <summary>Runs the ioctl; returns 0 or the errno.</summary>
        private static int Ioctl(string file, uint request, out uint value, uint input)
        {
            value = input;
            int fd = LibC.Open(file, O_RDONLY);
            if (fd < 0)
            {
                return Marshal.GetLastWin32Error();
            }
            try
            {
                return LibC.Ioctl(fd, new UIntPtr(request), ref value) < 0 ? Marshal.GetLastWin32Error() : 0;
            }
            finally
            {
                LibC.Close(fd);
            }
        }

        /// <summary>
        /// libc entry points. "libc.so.6" is glibc; the plain "libc" name is the fallback for other C libraries
        /// (dlopen of "libc.so" fails on glibc systems without the development package).
        /// </summary>
        private static class LibC
        {
            private static bool UseFallback;

            public static int Open(string path, int flags)
            {
                /* file names are bytes on Linux; .NET uses UTF-8 (netstandard2.0 has no LPUTF8Str marshalling) */
                byte[] utf8Path = Encoding.UTF8.GetBytes(path + "\0");
                if (!UseFallback)
                {
                    try { return GlibcOpen(utf8Path, flags); }
                    catch (DllNotFoundException) { UseFallback = true; }
                }
                return FallbackOpen(utf8Path, flags);
            }

            public static int Ioctl(int fd, UIntPtr request, ref uint value)
            {
                return UseFallback ? FallbackIoctl(fd, request, ref value) : GlibcIoctl(fd, request, ref value);
            }

            public static int Close(int fd)
            {
                return UseFallback ? FallbackClose(fd) : GlibcClose(fd);
            }

            [DllImport("libc.so.6", EntryPoint = "open", SetLastError = true)]
            private static extern int GlibcOpen(byte[] path, int flags);
            [DllImport("libc.so.6", EntryPoint = "ioctl", SetLastError = true)]
            private static extern int GlibcIoctl(int fd, UIntPtr request, ref uint value);
            [DllImport("libc.so.6", EntryPoint = "close", SetLastError = true)]
            private static extern int GlibcClose(int fd);

            [DllImport("libc", EntryPoint = "open", SetLastError = true)]
            private static extern int FallbackOpen(byte[] path, int flags);
            [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
            private static extern int FallbackIoctl(int fd, UIntPtr request, ref uint value);
            [DllImport("libc", EntryPoint = "close", SetLastError = true)]
            private static extern int FallbackClose(int fd);
        }
    }
}

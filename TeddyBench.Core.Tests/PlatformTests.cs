using NAudio.Wave;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace TeddyBench.Core.Tests
{
    public class LinuxPlatformTests
    {
        private static string Escape(string path) => path.Replace("\\", "\\134").Replace(" ", "\\040");

        [Fact]
        public void MountCardDetector_FindsContentOnFatMountsOnly()
        {
            using (var dir = new TempDir())
            {
                Directory.CreateDirectory(dir.Combine("card", "CONTENT"));
                Directory.CreateDirectory(dir.Combine("my card", "content"));     /* lower case, space in path */
                Directory.CreateDirectory(dir.Combine("ext4disk", "CONTENT"));    /* not FAT: ignored */
                Directory.CreateDirectory(dir.Combine("efi", "EFI"));             /* FAT without CONTENT */

                string mounts = string.Join("\n",
                    "/dev/nvme0n1p3 / btrfs rw,relatime 0 0",
                    "/dev/sdb1 " + Escape(dir.Combine("card")) + " vfat rw,nosuid,uid=1000,shortname=mixed 0 0",
                    "/dev/sdc1 " + Escape(dir.Combine("my card")) + " exfat rw,nosuid 0 0",
                    "/dev/sdd1 " + Escape(dir.Combine("ext4disk")) + " ext4 rw 0 0",
                    "/dev/nvme0n1p1 " + Escape(dir.Combine("efi")) + " vfat rw 0 0",
                    "/dev/sde1 /does/not/exist vfat rw 0 0",
                    "");

                var found = new MountCardDetector(() => mounts).FindContentDirectories().OrderBy(p => p).ToArray();

                Assert.Equal(new[] { dir.Combine("card", "CONTENT"), dir.Combine("my card", "content") }, found);
            }
        }

        [Fact]
        public void MountCardDetector_DecodesKernelEscapes()
        {
            Assert.Equal("/run/media/dane/TONIE BOX", MountCardDetector.Unescape("/run/media/dane/TONIE\\040BOX"));
            Assert.Equal("a\tb\\c", MountCardDetector.Unescape("a\\011b\\134c"));
            Assert.Equal("/run/media/dane/Spielä", MountCardDetector.Unescape("/run/media/dane/Spielä"));
            Assert.Equal("ends\\", MountCardDetector.Unescape("ends\\"));
        }

        [Fact]
        public void FatIoctl_RequestNumbersMatchLinuxHeaders()
        {
            /* linux/msdos_fs.h: FAT_IOCTL_GET_ATTRIBUTES _IOR('r', 0x10, __u32), FAT_IOCTL_SET_ATTRIBUTES _IOW('r', 0x11, __u32) */
            Assert.Equal(0x80047210u, FatIoctlLiveFlag.FAT_IOCTL_GET_ATTRIBUTES);
            Assert.Equal(0x40047211u, FatIoctlLiveFlag.FAT_IOCTL_SET_ATTRIBUTES);
        }

        [Fact]
        public void FatIoctlLiveFlag_OutsideFat_IsNeverLiveAndCannotBeSet()
        {
            if (!OperatingSystem.IsLinux())
            {
                return;
            }
            using (var dir = new TempDir())
            {
                string file = dir.Combine("500304E0");
                File.WriteAllBytes(file, new byte[] { 1 });
                var flag = new FatIoctlLiveFlag();

                Assert.False(flag.IsLive(file));
                var ex = Assert.Throws<NotSupportedException>(() => flag.SetLive(file, true));
                Assert.Contains("FAT", ex.Message);
                Assert.False(flag.IsLive(dir.Combine("missing")));
            }
        }

        [Fact]
        public void PipeAudioOutput_StreamsPcmToPlayerStdin()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }
            using (var dir = new TempDir())
            {
                string target = dir.Combine("out.raw");
                var player = new PipeAudioOutput.Player { Command = "sh", Arguments = (rate, ch) => "-c \"cat > '" + target + "'\"" };
                byte[] pcm = Enumerable.Range(0, 48000 * 4 / 2).Select(i => (byte)(i * 7)).ToArray(); /* 0.5 s stereo s16 */

                var stopped = new ManualResetEventSlim();
                Exception error = new Exception("not stopped");
                using (var output = new PipeAudioOutput(player))
                {
                    output.PlaybackStopped += (s, e) => { error = e.Exception; stopped.Set(); };
                    output.Init(new RawSourceWaveStream(new MemoryStream(pcm), new WaveFormat(48000, 16, 2)));
                    output.Play();
                    Assert.Equal(PlaybackState.Playing, output.PlaybackState);

                    Assert.True(stopped.Wait(10000));
                    Assert.Null(error);
                    Assert.Equal(PlaybackState.Stopped, output.PlaybackState);
                }
                Assert.Equal(pcm, File.ReadAllBytes(target));
            }
        }

        [Fact]
        public void PipeAudioOutput_StopEndsBlockedPlaybackQuickly()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }
            /* a "player" that never reads: the pipe fills up and the writer blocks */
            var player = new PipeAudioOutput.Player { Command = "sh", Arguments = (rate, ch) => "-c \"sleep 30\"" };
            var stopped = new ManualResetEventSlim();
            Exception error = new Exception("not stopped");
            var output = new PipeAudioOutput(player);
            output.PlaybackStopped += (s, e) => { error = e.Exception; stopped.Set(); };
            output.Init(new RawSourceWaveStream(new MemoryStream(new byte[48000 * 4 * 10]), new WaveFormat(48000, 16, 2)));
            output.Play();
            Thread.Sleep(300);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            output.Stop();
            Assert.True(stopped.Wait(5000));
            Assert.True(watch.ElapsedMilliseconds < 3000, "Stop took " + watch.ElapsedMilliseconds + " ms");
            Assert.Null(error);
            output.Dispose();
        }

        [Fact]
        public void PipeAudioOutput_PicksFirstAvailablePlayer()
        {
            Assert.NotNull(PipeAudioOutput.TryCreateFactory(cmd => cmd == "paplay" || cmd == "aplay"));
            Assert.Null(PipeAudioOutput.TryCreateFactory(cmd => false));
            Assert.Equal(new[] { "pw-play", "paplay", "aplay" }, PipeAudioOutput.KnownPlayers.Select(p => p.Command));
            /* pw-play needs --raw, otherwise it tries to parse a sound file from stdin */
            Assert.Equal("--raw --rate 48000 --channels 2 --format s16 -", PipeAudioOutput.KnownPlayers[0].Arguments(48000, 2));
            Assert.True(PipeAudioOutput.IsInPath("sh") || OperatingSystem.IsWindows());
        }

        [Fact]
        public void TonieAudioPlayer_ThroughPipeOutput_DeliversWholeTonie()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }
            using (var dir = new TempDir())
            {
                string target = dir.Combine("out.raw");
                string tonie = new CardContent(dir.Path).WriteTonieFile("E00403500102AB12", Fixtures.TonieFile(0x01234567));
                var player = new TonieAudioPlayer(() => new PipeAudioOutput(new PipeAudioOutput.Player { Command = "sh", Arguments = (r, c) => "-c \"cat > '" + target + "'\"" }));
                var stopped = new ManualResetEventSlim();
                Exception error = new Exception("not stopped");
                player.Stopped += e => { error = e; stopped.Set(); };

                player.Play(tonie);
                Assert.True(stopped.Wait(15000));
                Assert.Null(error);

                /* the 2 s fixture as 48 kHz stereo s16 */
                Assert.InRange(new FileInfo(target).Length / 192000.0, 1.9, 2.5);
            }
        }

        [Fact]
        public void SerialPorts_OnUnixOnlyProbesUsbSerialDevices()
        {
            string[] listed = { "/dev/ttyS0", "/dev/ttyS1", "/dev/ttyACM0", "/dev/ttyUSB0", "/dev/cu.usbmodem1101", "/dev/ttyAMA0" };
            Assert.Equal(new[] { "/dev/ttyACM0", "/dev/ttyUSB0", "/dev/cu.usbmodem1101" }, SerialPorts.Candidates(listed, windows: false).ToArray());
            Assert.Equal(new[] { "COM1", "COM7" }, SerialPorts.Candidates(new[] { "COM1", "COM7" }, windows: true).ToArray());
        }

        [Fact]
        public void SerialPorts_ReportsPortsWithoutPermission()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }
            var list = SerialPorts.ListPorts;
            var probe = SerialPorts.Probe;
            try
            {
                SerialPorts.ListPorts = () => new[] { "/dev/ttyS0", "/dev/ttyACM0", "/dev/ttyUSB0" };
                SerialPorts.Probe = port =>
                {
                    if (port == "/dev/ttyACM0") throw new UnauthorizedAccessException("denied");
                    if (port == "/dev/ttyS0") throw new InvalidOperationException("must not be probed");
                };

                Assert.Equal(new[] { "/dev/ttyUSB0" }, SerialPorts.GetAvailablePorts());
                Assert.Equal(new[] { "/dev/ttyACM0" }, SerialPorts.PermissionDenied);

                SerialPorts.Probe = port => { };
                SerialPorts.GetAvailablePorts();
                Assert.Empty(SerialPorts.PermissionDenied);
            }
            finally
            {
                SerialPorts.ListPorts = list;
                SerialPorts.Probe = probe;
            }
        }

        [Fact]
        public void XdgShell_QuotesArguments()
        {
            Assert.Equal("\"/run/media/x/My \\\"Card\\\"\"", XdgShell.Quote("/run/media/x/My \"Card\""));
        }
    }
}

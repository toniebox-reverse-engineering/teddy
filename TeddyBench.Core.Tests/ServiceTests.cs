using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TonieFile;
using Xunit;

namespace TeddyBench.Core.Tests
{
    public class TonieDatabaseTests
    {
        private const string TonieJson = "[{\"no\":\"1\",\"model\":\"10000123\",\"audio_id\":[\"1\"],\"hash\":[\"AABB\"],\"title\":\"Sine\",\"series\":\"Tones\",\"language\":\"de-de\",\"pic\":\"https://example.invalid/p.png\"}]";

        [Fact]
        public void Load_Download_CachesFileAndFindsByHash()
        {
            using (var dir = new TempDir())
            {
                string requested = null;
                var db = new TonieDatabase(dir.Combine("tonies.json"), dir.Combine("customTonies.json"), url => { requested = url; return TonieJson; });

                db.Load(true, TonieDatabase.DefaultUrl + "v1");

                Assert.Equal(TonieDatabase.DefaultUrl + "v1", requested);
                Assert.Equal(1, db.Count);
                Assert.Equal("", db.Status);
                Assert.Equal("Sine", db.Find("AABB").Title);
                Assert.Null(db.Find("CCDD"));
                Assert.Equal(TonieJson, File.ReadAllText(dir.Combine("tonies.json")));
            }
        }

        [Fact]
        public void Load_Offline_ReadsCachedToniesJsonNotCustomFile()
        {
            using (var dir = new TempDir())
            {
                File.WriteAllText(dir.Combine("tonies.json"), TonieJson);
                File.WriteAllText(dir.Combine("customTonies.json"), "{\"EEFF\":\"My tonie\"}");
                var db = new TonieDatabase(dir.Combine("tonies.json"), dir.Combine("customTonies.json"), url => throw new InvalidOperationException("must not download"));

                db.Load(false, null);

                /* the WinForms version parsed customTonies.json as tonies.json here and ended up empty */
                Assert.Equal(1, db.Count);
                Assert.Equal("", db.Status);
                Assert.Equal("My tonie", db.GetCustomName("EEFF"));
            }
        }

        [Fact]
        public void Load_DownloadFailure_SetsStatusAndRaisesError()
        {
            using (var dir = new TempDir())
            {
                var db = new TonieDatabase(dir.Combine("tonies.json"), dir.Combine("customTonies.json"), url => throw new IOException("offline"));
                string errorContext = null;
                db.Error += (ctx, ex) => errorContext = ctx;

                db.Load(true, "https://example.invalid");

                Assert.Equal("| Download Failed", db.Status);
                Assert.Equal("Downloader", errorContext);
                Assert.Equal(0, db.Count);
            }
        }

        [Fact]
        public void SetCustomName_IsPersisted()
        {
            using (var dir = new TempDir())
            {
                var db = new TonieDatabase(dir.Combine("tonies.json"), dir.Combine("customTonies.json"));
                Assert.True(db.SetCustomName("AABB", "Renamed"));
                Assert.Null(db.GetCustomName(null));

                var reloaded = new TonieDatabase(dir.Combine("tonies.json"), dir.Combine("customTonies.json"));
                reloaded.Load(false, null);
                Assert.Equal("Renamed", reloaded.GetCustomName("AABB"));
                Assert.Equal("| Reading tonies.json Failed", reloaded.Status);
            }
        }
    }

    public class OnlineServiceTests
    {
        private static string Release(string tag, string asset) =>
            "{\"tag_name\":\"" + tag + "\",\"name\":\"Release " + tag + "\",\"assets\":[{\"name\":\"" + asset + "\",\"browser_download_url\":\"https://example.invalid/" + asset + "\"}]}";

        [Fact]
        public async Task UpdateChecker_ReportsOnlyNewerReleaseWithExpectedAsset()
        {
            string requested = null;
            var checker = new UpdateChecker(url => { requested = url; return Task.FromResult(Release("v2.0", "TeddyBench.zip")); });

            UpdateInfo info = await checker.CheckAsync("v1.0");
            Assert.Equal("https://api.github.com/repos/toniebox-reverse-engineering/teddy/releases/latest", requested);
            Assert.Equal("v2.0", info.LatestVersion);
            Assert.Equal("Release v2.0", info.ReleaseName);
            Assert.Equal("https://example.invalid/TeddyBench.zip", info.AssetUrl);

            Assert.Null(await checker.CheckAsync("v2.0"));
            Assert.Null(await checker.CheckAsync(""));
            Assert.Null(await checker.CheckAsync("v1.0", "TeddyBench-linux-x64.tar.gz"));

            /* without asset filter (Linux: no installable asset yet) the release page is offered */
            var linkOnly = new UpdateChecker(url => Task.FromResult("{\"tag_name\":\"v3\",\"name\":\"R3\",\"html_url\":\"https://example.invalid/r3\",\"assets\":[]}"));
            UpdateInfo page = await linkOnly.CheckAsync("v1.0", null);
            Assert.Equal("https://example.invalid/r3", page.ReleaseUrl);
            Assert.Null(page.AssetUrl);

            /* the wanted asset does not have to be the first one */
            var several = new UpdateChecker(url => Task.FromResult("{\"tag_name\":\"v4\",\"name\":\"R4\",\"assets\":[" +
                "{\"name\":\"TeddyBench-x86_64.AppImage\",\"browser_download_url\":\"https://example.invalid/a\"}," +
                "{\"name\":\"TeddyBench.zip\",\"browser_download_url\":\"https://example.invalid/z\"}]}"));
            Assert.Equal("https://example.invalid/z", (await several.CheckAsync("v1.0")).AssetUrl);
        }

        [Theory]
        [InlineData("v2.0.1", "v2.0.0", true)]
        [InlineData("v2.1.0", "v2.0.9", true)]
        [InlineData("v10.0.0", "v9.9.9", true)]
        [InlineData("v2.0.0", "v2.0.0", false)]
        [InlineData("v1.7.0", "v2.0.0", false)]   /* upstream behind the fork: no downgrade offer */
        [InlineData("v2.0.0", "v2.0.0-rc.1", true)]
        [InlineData("v2.0.0-rc.1", "v2.0.0", false)]
        [InlineData("v2.0.0-rc.2", "v2.0.0-rc.1", true)]
        [InlineData("2.0", "v1.9.9", true)]
        [InlineData("nightly", "v2.0.0", true)]   /* not a version: old "differs" rule */
        [InlineData("v2.0.0", "", true)]
        [InlineData("", "v2.0.0", false)]
        public void UpdateChecker_IsNewer(string latest, string current, bool expected)
        {
            Assert.Equal(expected, UpdateChecker.IsNewer(latest, current));
        }

        [Fact]
        public async Task UpdateChecker_UsesConfiguredRepositoryAndNeverOffersOlderReleases()
        {
            string requested = null;
            var fork = new UpdateChecker(url => { requested = url; return Task.FromResult(Release("v1.7.0", "TeddyBench.zip")); }, "leubedane/teddy-linux");

            Assert.Equal("leubedane/teddy-linux", fork.Repository);
            Assert.Null(await fork.CheckAsync("v2.0.0"));
            Assert.Equal("https://api.github.com/repos/leubedane/teddy-linux/releases/latest", requested);

            Assert.Equal(UpdateChecker.DefaultRepository, new UpdateChecker(null, "  ").Repository);
        }

        [Fact]
        public void ImageCache_DownloadsOnceThenServesFromDisk()
        {
            using (var dir = new TempDir())
            {
                int downloads = 0;
                var cache = new ImageCache(dir.Path, url => { downloads++; return new byte[] { 1, 2, 3 }; });

                Assert.Equal(new byte[] { 1, 2, 3 }, cache.GetImage("https://example.invalid/a.png", "HASH"));
                Assert.Equal(new byte[] { 1, 2, 3 }, cache.GetImage("https://example.invalid/a.png", "HASH"));
                Assert.Equal(1, downloads);
                Assert.True(File.Exists(Path.Combine(dir.Path, "HASH.png")));

                var failing = new ImageCache(dir.Path, url => throw new IOException("offline"));
                Assert.Null(failing.GetImage("https://example.invalid/b.png", "OTHER"));
            }
        }

        [Fact]
        public void DiagnosticReports_ContainTheRelevantData()
        {
            string tag = DiagnosticReports.NfcTag(new byte[] { 0xE0, 0x04, 0x03, 0x50, 0x01, 0x02, 0xAB, 0x12, 0xDE, 0xAD });
            Assert.Contains("UUID:        E00403500102AB12", tag);
            Assert.Contains("Memory:      DEAD", tag);

            string crash;
            try
            {
                throw new InvalidOperationException("boom");
            }
            catch (Exception ex)
            {
                crash = DiagnosticReports.Exception("Worker", ex);
            }
            Assert.Contains("Thread: 'Worker'", crash);
            Assert.Contains("boom", crash);
            Assert.Contains(nameof(DiagnosticReports_ContainTheRelevantData), crash);
        }
    }

    public class InfrastructureTests
    {
        [Fact]
        public void SafeThread_AbortDoesNotThrowOnModernDotNet_AndReportsExceptions()
        {
            string failedThread = null;
            Exception failure = null;
            var done = new ManualResetEventSlim();
            Action<string, Exception> handler = (name, ex) => { failedThread = name; failure = ex; done.Set(); };
            SafeThread.UnhandledException += handler;
            try
            {
                var failing = new SafeThread(() => throw new InvalidOperationException("worker failed"), "FailingThread");
                failing.Start();
                Assert.True(done.Wait(5000));
                Assert.Equal("FailingThread", failedThread);
                Assert.Equal("worker failed", failure.Message);

                var stop = new ManualResetEventSlim();
                var hanging = new SafeThread(() => stop.Wait(), "HangingThread");
                hanging.Start();
                Assert.False(hanging.Join(50));
                hanging.Abort(); /* Thread.Abort throws PlatformNotSupportedException on .NET; must be swallowed */
                stop.Set();
                Assert.True(hanging.Join(5000));
            }
            finally
            {
                SafeThread.UnhandledException -= handler;
            }
        }

        [Fact]
        public void Logger_FiltersByLevelAndKeepsText()
        {
            var previous = Logger.LogLevel;
            try
            {
                Logger.LogLevel = Logger.eLogLevel.Warning;
                string marker = Guid.NewGuid().ToString("N");
                var seen = new List<string>();
                Action<Logger.eLogLevel, string> handler = (level, msg) => { if (msg.Contains(marker)) seen.Add(msg); };
                Logger.MessageLogged += handler;

                Logger.Log(Logger.eLogLevel.Debug, "debug " + marker);
                Logger.Log(Logger.eLogLevel.Error, "error " + marker);
                Logger.MessageLogged -= handler;

                Assert.Equal(new[] { "error " + marker }, seen);
                Assert.Contains("error " + marker, Logger.GetText());
                Assert.DoesNotContain("debug " + marker, Logger.GetText());
            }
            finally
            {
                Logger.LogLevel = previous;
            }
        }

        [Fact]
        public void AppPaths_UsesXdgDirectoriesOutsideWindows()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(Environment.CurrentDirectory, AppPaths.DefaultDataDirectory());
                return;
            }

            string oldConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            string oldCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            try
            {
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "/xdg/config");
                Environment.SetEnvironmentVariable("XDG_CACHE_HOME", "/xdg/cache");
                Assert.Equal("/xdg/config/TeddyBench", AppPaths.DefaultDataDirectory());
                Assert.Equal("/xdg/cache/TeddyBench", AppPaths.DefaultCacheDirectory());

                /* relative values are invalid per the XDG spec and must be ignored */
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "relative/dir");
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                Assert.Equal(Path.Combine(home, ".config", "TeddyBench"), AppPaths.DefaultDataDirectory());
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", oldConfig);
                Environment.SetEnvironmentVariable("XDG_CACHE_HOME", oldCache);
            }
        }

        [Fact]
        public void Settings_RoundTrip()
        {
            using (var dir = new TempDir())
            {
                string file = dir.Combine("teddyBench.cfg");
                var settings = new Settings { Username = "tester", NfcEnabled = true, NfcType = "Pn5180Esp" };
                Assert.True(settings.Save(file));

                var loaded = Settings.FromFile(file);
                Assert.Equal("tester", loaded.Username);
                Assert.True(loaded.NfcEnabled);
                Assert.Equal("Pn5180Esp", loaded.NfcType);
                Assert.NotNull(Settings.FromFile(dir.Combine("missing.cfg")));
            }
        }

        [Fact]
        public void NamingHelpers_ArePortable()
        {
            Assert.Equal("12AB0201500304E0", TonieTools.ReverseUid("E00403500102AB12"));
            Assert.Equal("Was ist was Pferde", TonieTools.RemoveInvalidChars("Was ist was: Pferde?"));
            Assert.Equal("ab", TonieTools.RemoveInvalidChars("a/\\<>|\"*b"));
        }
    }

    public class TonieAudioPlayerTests
    {
        [Fact]
        public void Play_FeedsDecodedAudioToOutput_AndStops()
        {
            using (var dir = new TempDir())
            {
                string file = new CardContent(dir.Path).WriteTonieFile("E00403500102AB12", Fixtures.TonieFile(0x01234567));
                var output = new FakeWavePlayer();
                var player = new TonieAudioPlayer(() => output);
                var progressed = new ManualResetEventSlim();
                var stopped = new ManualResetEventSlim();
                Exception stopError = new Exception("not stopped");
                TimeSpan total = TimeSpan.Zero;
                player.Progress += (pos, tot) => { total = tot; progressed.Set(); };
                player.Stopped += err => { stopError = err; stopped.Set(); };

                player.Play(file);
                Assert.True(progressed.Wait(5000));
                Assert.True(player.IsPlaying);
                Assert.Equal(new WaveFormat(48000, 16, 2), output.Source.WaveFormat);
                Assert.InRange(total.TotalSeconds, 1.9, 2.5);

                player.Stop();
                Assert.True(stopped.Wait(5000));
                Assert.Null(stopError);
                Assert.True(output.Disposed);
                Assert.False(player.IsPlaying);
            }
        }

        [Fact]
        public void Play_WithoutAudioOutput_Throws()
        {
            var previous = PlatformServices.AudioOutputFactory;
            PlatformServices.AudioOutputFactory = null;
            try
            {
                Assert.Throws<InvalidOperationException>(() => new TonieAudioPlayer().Play("x"));
            }
            finally
            {
                PlatformServices.AudioOutputFactory = previous;
            }
        }

        private class FakeWavePlayer : IWavePlayer
        {
            public IWaveProvider Source;
            public bool Disposed;
            public float Volume { get; set; } = 1;
            public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;
            public WaveFormat OutputWaveFormat => Source?.WaveFormat;
            public event EventHandler<StoppedEventArgs> PlaybackStopped;

            public void Init(IWaveProvider waveProvider) { Source = waveProvider; }
            public void Play() { PlaybackState = PlaybackState.Playing; }
            public void Pause() { PlaybackState = PlaybackState.Paused; }
            public void Stop() { PlaybackState = PlaybackState.Stopped; }
            public void Dispose() { Disposed = true; }
        }
    }
}

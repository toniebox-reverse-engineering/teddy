using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeddyBench.Avalonia.Services;
using TeddyBench.Avalonia.ViewModels;
using TeddyBench.Core;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(TeddyBench.Avalonia.Tests.TestAppBuilder))]

namespace TeddyBench.Avalonia.Tests
{
    public class TestAppBuilder
    {
        /* Skia rendering (not headless drawing) so frames can be captured as screenshots */
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    public sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "teddybench-ui-test-" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public string Combine(params string[] parts) => System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (Exception)
            {
            }
        }
    }

    internal static class Fixtures
    {
        public static readonly string Album = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "album");
        public static readonly string Mp3 = System.IO.Path.Combine(Album, "01 stereo44k.mp3");
        public static readonly string Ogg = System.IO.Path.Combine(Album, "03 opus48k.ogg");

        private static readonly ConcurrentDictionary<uint, byte[]> Encoded = new ConcurrentDictionary<uint, byte[]>();

        public static byte[] TonieFile(uint audioId) => Encoded.GetOrAdd(audioId, id => CardContent.Encode(new[] { Mp3 }, id).FileContent);
    }

    /// <summary>Runs UI work immediately on the calling thread.</summary>
    public class ImmediateUi : IUiDispatcher
    {
        public void Post(Action action) => action();
        public Task<T> InvokeAsync<T>(Func<Task<T>> action) => action();
    }

    public class NullCovers : ICoverImages
    {
        public IImage Unknown => null;
        public IImage Custom => null;
        public IImage FromBytes(byte[] data) => null;
    }

    /// <summary>
    /// Scripted dialogs: every call is recorded in <see cref="Calls"/>; answers are taken from the
    /// queues (or a default). Unexpected prompts therefore show up in Calls.
    /// </summary>
    public class FakeDialogs : IDialogService
    {
        public readonly List<string> Calls = new List<string>();
        public readonly List<string> Messages = new List<string>();
        public readonly Queue<bool> YesNo = new Queue<bool>();
        public readonly Queue<bool?> YesNoCancel = new Queue<bool?>();
        public readonly Queue<string> Uids = new Queue<string>();
        public readonly Queue<string> Texts = new Queue<string>();
        public readonly Queue<string> Folders = new Queue<string>();
        public readonly Queue<string[]> Files = new Queue<string[]>();
        public ReportResult Report;
        public Func<string[], string[]> Sort = files => files;
        public string TagDump;
        public bool LogVisible;
        public FakeOperation LastOperation;

        private void Call(string what) { lock (Calls) Calls.Add(what); }

        public Task ShowMessage(string text, string title = "TeddyBench", bool error = false)
        {
            Call("message:" + title);
            Messages.Add(text);
            return Task.CompletedTask;
        }

        public Task<bool> AskYesNo(string text, string title)
        {
            Call("yesno:" + title);
            return Task.FromResult(YesNo.Count > 0 ? YesNo.Dequeue() : false);
        }

        public Task<bool?> AskYesNoCancel(string text, string title)
        {
            Call("yesnocancel:" + title);
            return Task.FromResult(YesNoCancel.Count > 0 ? YesNoCancel.Dequeue() : null);
        }

        public Task<string> AskUid(RfidReaderBase reader, string initialUid = null)
        {
            Call("uid:" + initialUid);
            return Task.FromResult(Uids.Count > 0 ? Uids.Dequeue() : null);
        }

        public Task<uint?> AskAudioId(string initialHex)
        {
            Call("audioid");
            return Task.FromResult<uint?>(null);
        }

        public Task<string> AskText(string title, string prompt, string initial)
        {
            Call("text:" + initial);
            return Task.FromResult(Texts.Count > 0 ? Texts.Dequeue() : null);
        }

        public Task<string[]> PickFiles(string title, bool allowMultiple, string filterName = null, params string[] patterns)
        {
            Call("files:" + title);
            return Task.FromResult(Files.Count > 0 ? Files.Dequeue() : new string[0]);
        }

        public Task<string> PickFolder(string title)
        {
            Call("folder:" + title);
            return Task.FromResult(Folders.Count > 0 ? Folders.Dequeue() : null);
        }

        public Task<ReportResult> ShowReport(string content, string defaultUsername)
        {
            Call("report:" + defaultUsername);
            return Task.FromResult(Report);
        }

        public Task<string[]> SortTracks(string[] files)
        {
            Call("sort");
            return Task.FromResult(Sort(files));
        }

        public Task<string> ShowTagDump(bool display, string initial)
        {
            Call("tagdump:" + initial);
            return Task.FromResult(display ? initial : TagDump);
        }

        public Task<bool> AskUpdate(string version, string releaseName, bool canInstall)
        {
            Call("update:" + version + ":" + canInstall);
            return Task.FromResult(YesNo.Count > 0 ? YesNo.Dequeue() : false);
        }

        public Task ShowAbout(string version)
        {
            Call("about:" + version);
            return Task.CompletedTask;
        }

        public Task ShowAntennaPlot(Proxmark3.MeasurementResult result)
        {
            Call("antenna");
            return Task.CompletedTask;
        }

        public IOperationHandle ShowOperation(string title, bool cancellable)
        {
            Call("operation:" + cancellable);
            LastOperation = new FakeOperation();
            return LastOperation;
        }

        public void SetLogWindowVisible(bool visible)
        {
            Call("log:" + visible);
            LogVisible = visible;
        }
    }

    public class FakeOperation : IOperationHandle
    {
        public readonly CancellationTokenSource Cts = new CancellationTokenSource();
        public bool Closed;
        public CancellationToken Cancelled => Cts.Token;
        public void Close() => Closed = true;
    }

    /// <summary>NFC reader that does nothing on its own; tests trigger its events.</summary>
    public class FakeReader : RfidReaderBase
    {
        public byte[] Memory;
        public ManualResetEventSlim ReadGate;

        public FakeReader(bool unlockSupported = true)
        {
            UnlockSupported = unlockSupported;
            HardwareType = "FakeReader";
            DeviceInfo = eDeviceInfo.BootromPresent;
        }

        protected override void ScanThreadFunc() { }

        public override byte[] ReadMemory()
        {
            ReadGate?.Wait(5000);
            return Memory;
        }

        public void RaiseUid(string uid) => OnUidFound(uid);
        public void RaiseDevice(string port) { Connected = port != null; CurrentPort = port; OnDeviceFound(port); }
        public void RaiseFlashRequest(Proxmark3.FlashRequestContext ctx) => OnFlashRequest(ctx);
    }

    public class FakeShell : IShell
    {
        public readonly List<string> Opened = new List<string>();
        public void OpenUrl(string url) => Opened.Add("url:" + url);
        public void OpenFolder(string path) => Opened.Add("folder:" + path);
        public void RevealFile(string file) => Opened.Add("reveal:" + file);
    }

    /// <summary>A main window view model working on temporary directories, without background work.</summary>
    public sealed class Harness : IDisposable
    {
        public readonly TempDir Dir = new TempDir();
        public readonly FakeDialogs Dialogs = new FakeDialogs();
        public readonly MainWindowViewModel Vm;
        public readonly MainWindowServices Services;
        public readonly List<string[]> SentReports = new List<string[]>();
        public string Card => Dir.Combine("CONTENT");

        public Harness(Func<string, RfidReaderBase> reader = null, string toniesJson = null, Func<string, Task<string>> github = null, string baseTag = "", ICoverImages covers = null, IDialogService dialogs = null)
        {
            Directory.CreateDirectory(Card);
            Directory.CreateDirectory(Dir.Combine("data"));
            if (toniesJson != null)
            {
                File.WriteAllText(Dir.Combine("data", "tonies.json"), toniesJson);
            }

            Services = new MainWindowServices
            {
                Database = new TonieDatabase(Dir.Combine("data", "tonies.json"), Dir.Combine("data", "customTonies.json"), url => throw new IOException("no network in tests")),
                Images = new ImageCache(Dir.Combine("cache"), url => throw new IOException("no network in tests")),
                CardMonitor = new CardMonitor(new NoCards()),
                SettingsFile = Dir.Combine("data", "teddyBench.cfg"),
                SendDiagnostics = (payload, sender, message, filename) =>
                {
                    SentReports.Add(new[] { payload, sender, message, filename });
                    return Task.FromResult(new DiagStatus { Success = true, Message = "thanks" });
                },
                CreateReader = reader ?? (type => null),
                UpdateChecker = new UpdateChecker(github ?? (url => throw new IOException("no network in tests"))),
                Version = "9.9.9-test",
                BaseTag = baseTag,
                StartBackgroundWork = false,
                CanPlayAudio = () => false
            };
            Services.Database.Load(false, null);
            Vm = new MainWindowViewModel(dialogs ?? Dialogs, new ImmediateUi(), covers ?? new NullCovers(), Services);
        }

        /// <summary>Writes a tonie file for the UID onto the fake card.</summary>
        public string AddTonie(string uid, uint audioId)
        {
            return new CardContent(Card).WriteTonieFile(uid, Fixtures.TonieFile(audioId));
        }

        public void OpenCard()
        {
            Vm.OpenPath(Card);
            Vm.AnalyzeOnce();
        }

        public void Dispose()
        {
            Vm.Shutdown();
            Dir.Dispose();
        }

        private class NoCards : ICardDetector
        {
            public IEnumerable<string> FindContentDirectories() => new string[0];
        }
    }
}

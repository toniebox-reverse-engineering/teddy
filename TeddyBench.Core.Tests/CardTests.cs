using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using TonieFile;
using Xunit;

namespace TeddyBench.Core.Tests
{
    public class CardTests
    {
        /* tag UID as read from the NFC tag, and the reversed form used for the path on the SD card */
        private const string Uid = "E00403500102AB12";
        private const string ReversedUid = "12AB0201500304E0";

        private const uint CustomAudioId = 0x01234567;   /* below 0x50000000: custom */
        private const uint OfficialAudioId = 0x5E6F7A8B; /* above: looks official */

        [Fact]
        public void GetTonieFilePath_UsesReversedUidAsDirectoryAndFileName()
        {
            var card = new CardContent("/card/CONTENT");

            Assert.Equal(Path.Combine("/card/CONTENT", "12AB0201", "500304E0"), card.GetTonieFilePath(Uid));
            Assert.Throws<ArgumentException>(() => card.GetTonieFilePath("E004"));
        }

        [Fact]
        public void WriteTonieFile_ThenScan_FindsEntryWithOriginalUid()
        {
            using (var dir = new TempDir())
            {
                var card = new CardContent(dir.Path);
                string written = card.WriteTonieFile(Uid, Fixtures.TonieFile(CustomAudioId));

                var entry = Assert.Single(card.Scan());
                Assert.Equal(written, entry.FileName);
                Assert.Equal(Uid, entry.Uid);
                Assert.Equal(Uid, entry.InitialText);
            }
        }

        [Fact]
        public void Scan_MatchesLowerCaseFileNames_AndExportedFiles()
        {
            using (var dir = new TempDir())
            {
                /* vfat mounted with shortname=lower shows "500304e0" */
                Directory.CreateDirectory(dir.Combine("12ab0201"));
                File.WriteAllBytes(dir.Combine("12ab0201", "500304e0"), new byte[] { 1 });
                File.WriteAllBytes(dir.Combine("10000123 - 1A2B3C4D - Some Title"), new byte[] { 1 });
                File.WriteAllBytes(dir.Combine("unrelated.txt"), new byte[] { 1 });

                var entries = new CardContent(dir.Path).Scan();

                Assert.Equal(2, entries.Count);
                Assert.Contains(entries, e => e.FileName.EndsWith("500304e0"));
                Assert.Contains(entries, e => e.InitialText == "10000123 - Some Title");
            }
        }

        [Fact]
        public void Reassign_MovesFileAndRemovesEmptyDirectory()
        {
            using (var dir = new TempDir())
            {
                var card = new CardContent(dir.Path);
                card.WriteTonieFile(Uid, new byte[] { 42 });
                var entry = card.Scan().Single();

                Assert.Equal(ReassignResult.Unchanged, card.Reassign(entry, Uid));

                const string newUid = "E00403500102CD34";
                Assert.Equal(ReassignResult.Moved, card.Reassign(entry, newUid));

                Assert.False(Directory.Exists(dir.Combine("12AB0201")));
                Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(card.GetTonieFilePath(newUid)));
            }
        }

        [Fact]
        public void Reassign_KeepsNonEmptyDirectory_AndRefusesToOverwrite()
        {
            using (var dir = new TempDir())
            {
                var card = new CardContent(dir.Path);
                card.WriteTonieFile(Uid, new byte[] { 1 });
                File.WriteAllText(dir.Combine("12AB0201", "other"), "keep me");
                const string otherUid = "E00403500102CD34";
                card.WriteTonieFile(otherUid, new byte[] { 2 });

                var entry = card.Scan().Single(e => e.Uid == Uid);

                var ex = Assert.Throws<CardOperationException>(() => card.Reassign(entry, otherUid));
                Assert.Contains("already exists", ex.Message);

                Assert.Equal(ReassignResult.MovedOldDirectoryKept, card.Reassign(entry, "E00403500102EF56"));
                Assert.True(File.Exists(dir.Combine("12AB0201", "other")));
            }
        }

        [Fact]
        public void Delete_RemovesFileAndDirectory()
        {
            using (var dir = new TempDir())
            {
                var card = new CardContent(dir.Path);
                card.WriteTonieFile(Uid, new byte[] { 1 });

                CardContent.Delete(card.Scan().Single());

                Assert.Empty(Directory.GetFileSystemEntries(dir.Path));
            }
        }

        [Fact]
        public void Export_UsesPortableFileName()
        {
            using (var dir = new TempDir())
            {
                var card = new CardContent(dir.Path);
                card.WriteTonieFile(Uid, new byte[] { 7 });
                var entry = card.Scan().Single();
                entry.AudioId = 0x1A2B3C4D;
                entry.Info = new TonieTools.TonieData { Model = "10000123", Title = "Was ist was: Pferde?" };

                Directory.CreateDirectory(dir.Combine("out"));
                string exported = CardContent.Export(entry, dir.Combine("out"));

                Assert.Equal("10000123 - 1A2B3C4D - Was ist was Pferde", Path.GetFileName(exported));
                Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(exported));
                /* the exported file is picked up again when that folder is opened */
                Assert.Equal("10000123 - Was ist was Pferde", new CardContent(dir.Combine("out")).Scan().Single().InitialText);
            }
        }

        [Fact]
        public void Resolver_UnknownAndUnnamedCustomTonies()
        {
            using (var dir = new TempDir())
            {
                var db = EmptyDatabase(dir);
                var card = new CardContent(dir.Path);

                card.WriteTonieFile(Uid, Fixtures.TonieFile(CustomAudioId));
                var custom = card.Scan().Single();
                var res = TonieResolver.Resolve(custom, new TonieFileCache().GetHeader(custom.FileName), db, live: false);

                Assert.Equal(TonieKind.Custom, res.Kind);
                Assert.Equal(TonieResolver.ImageCustom, res.ImageKey);
                Assert.StartsWith("Unnamed Teddy - [" + Uid + "]", res.DisplayText);
                Assert.Equal(CustomAudioId, custom.AudioId);
                Assert.Equal(40, custom.Hash.Length);

                CardContent.Delete(custom);
                card.WriteTonieFile(Uid, Fixtures.TonieFile(OfficialAudioId));
                var unknown = card.Scan().Single();
                res = TonieResolver.Resolve(unknown, TonieAudio.FromFile(unknown.FileName, false), db, live: true);

                Assert.Equal(TonieKind.Unknown, res.Kind);
                Assert.Equal(TonieResolver.ImageUnknown, res.ImageKey);
                Assert.False(res.Recognized);
                Assert.StartsWith("[live] [" + Uid + "]", res.DisplayText);
                Assert.Contains("AudioID:  0x5E6F7A8B", res.ToolTip);
            }
        }

        [Fact]
        public void Resolver_KnownTonie_AndCustomNameOverride()
        {
            using (var dir = new TempDir())
            {
                var card = new CardContent(dir.Path);
                card.WriteTonieFile(Uid, Fixtures.TonieFile(OfficialAudioId));
                var entry = card.Scan().Single();
                TonieAudio header = TonieAudio.FromFile(entry.FileName, false);
                string hash = BitConverter.ToString(header.Header.Hash).Replace("-", "");

                var db = DatabaseWith(dir, new TonieTools.TonieData { Model = "10000123", Title = "Sine", Pic = "https://example.invalid/sine.png", Hash = new[] { hash } });

                var res = TonieResolver.Resolve(entry, header, db, live: false);
                Assert.Equal(TonieKind.Known, res.Kind);
                Assert.Equal(hash, res.ImageKey);
                Assert.Equal("https://example.invalid/sine.png", res.PicUrl);
                Assert.Equal("10000123 - Sine", res.DisplayText);
                Assert.Equal("Sine", entry.Info.Title);

                db.SetCustomName(hash, "My name");
                Assert.Equal("My name", TonieResolver.Resolve(entry, header, db, live: false).DisplayText);
            }
        }

        [Fact]
        public void SaveAudio_WritesOneOggPerChapterIntoPerTonieDirectory()
        {
            using (var dir = new TempDir())
            {
                var card = new CardContent(dir.Combine("card"));
                Directory.CreateDirectory(card.RootDirectory);
                card.WriteTonieFile(Uid, Fixtures.TonieFile(OfficialAudioId));
                var entry = card.Scan().Single();
                string hash = BitConverter.ToString(TonieAudio.FromFile(entry.FileName, false).Header.Hash).Replace("-", "");
                var db = DatabaseWith(dir, new TonieTools.TonieData { Model = "10000123", Title = "Sine: 440?", Hash = new[] { hash }, Tracks = new[] { "Tone" } });

                Directory.CreateDirectory(dir.Combine("out"));
                CardContent.SaveAudio(entry, dir.Combine("out"), true, db, "test");

                string outDir = dir.Combine("out", "10000123 - 5E6F7A8B - Sine 440");
                Assert.True(Directory.Exists(outDir), "per-tonie directory with portable name");
                var oggs = Directory.GetFiles(outDir).Select(Path.GetFileName).ToArray();
                Assert.Equal(new[] { "500304E0-5E6F7A8B - Track #01.ogg" }, oggs);
            }
        }

        [Fact]
        public void HeaderCache_ReloadsChangedFiles()
        {
            using (var dir = new TempDir())
            {
                var card = new CardContent(dir.Path);
                string file = card.WriteTonieFile(Uid, Fixtures.TonieFile(CustomAudioId));
                var cache = new TonieFileCache();

                var first = cache.GetHeader(file);
                Assert.Same(first, cache.GetHeader(file));

                File.WriteAllBytes(file, Fixtures.TonieFile(OfficialAudioId));
                File.SetLastWriteTime(file, DateTime.Now.AddMinutes(1));
                Assert.Equal(OfficialAudioId, cache.GetHeader(file).Header.AudioId);
            }
        }

        [Fact]
        public void CardMonitor_ReportsCardsAndLostDirectoryOnce()
        {
            using (var dir = new TempDir())
            {
                string content = dir.Combine("CONTENT");
                Directory.CreateDirectory(content);
                var monitor = new CardMonitor(new FakeDetector(content));
                string found = null;
                int lost = 0;
                monitor.CardFound += c => found = c;
                monitor.CurrentDirectoryLost += () => lost++;

                monitor.CurrentDirectory = content;
                monitor.Poll();
                Assert.Equal(content, found);
                Assert.Equal(0, lost);

                Directory.Delete(content);
                monitor.Poll();
                monitor.Poll();
                Assert.Equal(1, lost);
                Assert.Null(monitor.CurrentDirectory);
            }
        }

        private class FakeDetector : ICardDetector
        {
            private readonly string[] Dirs;
            public FakeDetector(params string[] dirs) { Dirs = dirs; }
            public System.Collections.Generic.IEnumerable<string> FindContentDirectories() => Dirs.Where(Directory.Exists);
        }

        private static TonieDatabase EmptyDatabase(TempDir dir)
        {
            return new TonieDatabase(dir.Combine("tonies.json"), dir.Combine("customTonies.json"));
        }

        private static TonieDatabase DatabaseWith(TempDir dir, params TonieTools.TonieData[] tonies)
        {
            var db = EmptyDatabase(dir);
            File.WriteAllText(dir.Combine("tonies.json"), JsonConvert.SerializeObject(tonies));
            db.Load(false, null);
            return db;
        }
    }
}

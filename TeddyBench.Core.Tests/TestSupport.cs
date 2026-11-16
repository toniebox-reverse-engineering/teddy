using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using TeddyBench.Core;

namespace TeddyBench.Core.Tests
{
    /// <summary>Temporary directory, deleted on dispose.</summary>
    public sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "teddybench-test-" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public string Combine(params string[] parts)
        {
            return System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());
        }

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
        public static readonly string Mp3 = Path.Combine(AppContext.BaseDirectory, "Fixtures", "album", "01 stereo44k.mp3");

        private static readonly ConcurrentDictionary<uint, byte[]> Encoded = new ConcurrentDictionary<uint, byte[]>();

        /// <summary>A real tonie file (header + Opus audio) with the given audio ID, encoded once per ID.</summary>
        public static byte[] TonieFile(uint audioId)
        {
            return Encoded.GetOrAdd(audioId, id => CardContent.Encode(new[] { Mp3 }, id).FileContent);
        }
    }
}

using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TeddyBench.Avalonia.Services
{
    public class AvaloniaUiDispatcher : IUiDispatcher
    {
        public void Post(Action action)
        {
            Dispatcher.UIThread.Post(action);
        }

        public Task<T> InvokeAsync<T>(Func<Task<T>> action)
        {
            return Dispatcher.UIThread.InvokeAsync(action);
        }
    }

    /// <summary>Tonie cover images: placeholders for unknown/custom tonies and downloaded pictures.</summary>
    public interface ICoverImages
    {
        IImage Unknown { get; }
        IImage Custom { get; }
        /// <summary>Decodes a downloaded picture (scaled to the tile size); null if it is not an image.</summary>
        IImage FromBytes(byte[] data);
    }

    public class CoverImages : ICoverImages
    {
        public const int Size = 128;

        public IImage Unknown { get; } = Load("unknown.png");
        public IImage Custom { get; } = Load("custom.png");

        public IImage FromBytes(byte[] data)
        {
            try
            {
                using (var stream = new MemoryStream(data))
                {
                    return Bitmap.DecodeToWidth(stream, Size, BitmapInterpolationMode.HighQuality);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IImage Load(string name)
        {
            using (var stream = AssetLoader.Open(new Uri("avares://TeddyBench/Assets/" + name)))
            {
                return Bitmap.DecodeToWidth(stream, Size, BitmapInterpolationMode.HighQuality);
            }
        }
    }
}

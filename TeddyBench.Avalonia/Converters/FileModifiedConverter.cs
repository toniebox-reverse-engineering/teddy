using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.IO;

namespace TeddyBench.Avalonia.Converters
{
    public class FileModifiedConverter : IValueConverter
    {
        public static readonly FileModifiedConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    var fileInfo = new FileInfo(path);
                    return fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch
                {
                    return "N/A";
                }
            }
            return "N/A";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

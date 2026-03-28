using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.IO;

namespace TeddyBench.Avalonia.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public static readonly FileSizeConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    var fileInfo = new FileInfo(path);
                    return fileInfo.Length / 1024; // Return size in KB
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

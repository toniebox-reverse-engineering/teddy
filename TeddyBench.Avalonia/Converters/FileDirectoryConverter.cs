using Avalonia.Data.Converters;
using System;
using System.Globalization;
using TeddyBench.Avalonia.Models;

namespace TeddyBench.Avalonia.Converters
{
    public class FileDirectoryConverter : IValueConverter
    {
        public static readonly FileDirectoryConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TonieFileItem item && !string.IsNullOrEmpty(item.DirectoryName))
            {
                return item.DirectoryName;
            }

            return "N/A";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

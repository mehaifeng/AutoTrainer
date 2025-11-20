using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.IO;

namespace AutoTrainer.Converters
{
    public class FileNameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                return Path.GetFileName(path);
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

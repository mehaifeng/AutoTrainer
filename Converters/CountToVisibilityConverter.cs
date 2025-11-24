using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AutoTrainer.Converters
{
    /// <summary>
    /// 将Count值转换为可见性
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

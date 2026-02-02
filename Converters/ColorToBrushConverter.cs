using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;
using System;

namespace AutoTrainer.Converters
{
    /// <summary>
    /// 颜色到画刷转换器
    /// </summary>
    public class ColorToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return new SolidColorBrush(color);
            }
            return Brushes.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

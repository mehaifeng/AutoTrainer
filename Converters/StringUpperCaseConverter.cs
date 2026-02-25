using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AutoTrainer.Converters
{
    public class StringUpperCaseConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str.ToUpper();
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

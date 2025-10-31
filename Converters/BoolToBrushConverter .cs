using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTrainer.Converters
{
    public class BoolToBrushConverter:IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isTrue)
            {
                return isTrue ? new SolidColorBrush(Colors.DarkSlateGray) : new SolidColorBrush(Colors.Transparent);
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return 1;
        }
    }
}

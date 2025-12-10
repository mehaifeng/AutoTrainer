using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AutoTrainer.Converters
{
    public class BoolToValidationSplitTipConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isEnabled)
            {
                return isEnabled 
                    ? "未指定验证集时，从训练集按此比例自动划分" 
                    : "已指定验证集目录，此配置项将被忽略";
            }
            return "未指定验证集时，从训练集按此比例自动划分";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return null;
        }
    }
}

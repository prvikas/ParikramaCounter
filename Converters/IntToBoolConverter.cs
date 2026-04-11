using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ParikramaCounter.Converters
{
    // Returns true when the bound integer equals the ConverterParameter.
    // Used to show "no items" labels when a collection is empty:
    // IsVisible="{Binding Count, Converter={StaticResource IntToBoolConverter}, ConverterParameter=0}"
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count && parameter is string param && int.TryParse(param, out int target))
                return count == target;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

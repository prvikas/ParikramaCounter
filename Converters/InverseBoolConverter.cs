using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ParikramaCounter.Converters
{
    // Fix #18: InverseBoolConverter was referenced in SettingsPage.xaml via
    // {StaticResource InverseBoolConverter} but was never defined anywhere in
    // the project — causing a runtime crash on both Android and iOS when the
    // Settings page loaded.
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }
}

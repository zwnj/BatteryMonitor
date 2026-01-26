using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BatteryMonitor3.Helpers
{
    public class PinColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPinned && isPinned)
            {
                return Brushes.LightGreen; // Pinned color
            }
            return Brushes.Gray; // Unpinned color
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

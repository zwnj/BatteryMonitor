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
                return Brushes.LightGreen; // 固定時の色
            }
            return Brushes.Gray; // 未固定時の色
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

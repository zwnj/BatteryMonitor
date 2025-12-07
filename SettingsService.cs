using System;
using System.IO;
using System.Windows;
using System.Globalization;

namespace BatteryMonitor3
{
    public static class SettingsService
    {
        private static readonly string FilePath = Path.Combine(
            AppContext.BaseDirectory, 
            "last_position.txt");

        public static void SaveWindowPosition(System.Windows.Point position)
        {
            try
            {
                var text = $"{position.X.ToString(CultureInfo.InvariantCulture)},{position.Y.ToString(CultureInfo.InvariantCulture)}";
                File.WriteAllText(FilePath, text);
            }
            catch (Exception)
            {
                // Log error if needed, but fail silently for the user.
            }
        }

        public static System.Windows.Point? LoadWindowPosition()
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            try
            {
                var text = File.ReadAllText(FilePath);
                var parts = text.Split(',');
                if (parts.Length == 2 && 
                    double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double x) && 
                    double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double y))
                {
                    return new System.Windows.Point(x, y);
                }
            }
            catch (Exception)
            {
                 // Log error if needed, but fail silently.
            }

            return null;
        }
    }
}

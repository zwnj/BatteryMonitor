using BatteryMonitor3.Models;

namespace BatteryMonitor3.Services
{
    public static class AppSettingsStore
    {
        public static AppSettings Load()
        {
            return AppSettings.Load();
        }

        public static void Save(AppSettings settings)
        {
            AppSettings.Save(settings.WindowLeft, settings.WindowTop, settings.ChargeLimit);
        }

        public static void SaveChargeLimit(int chargeLimit)
        {
            var current = Load();
            AppSettings.Save(current.WindowLeft, current.WindowTop, chargeLimit);
        }

        public static void SaveWindowPosition(double left, double top)
        {
            var current = Load();
            AppSettings.Save(left, top, current.ChargeLimit);
        }
    }
}

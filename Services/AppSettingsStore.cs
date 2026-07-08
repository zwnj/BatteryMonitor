using BatteryMonitor.Models;

namespace BatteryMonitor.Services
{
    public static class AppSettingsStore
    {
        private static readonly object SyncRoot = new();
        private static AppSettings? _cachedSettings;

        private static AppSettings Clone(AppSettings settings)
        {
            return new AppSettings
            {
                WindowLeft = settings.WindowLeft,
                WindowTop = settings.WindowTop,
                ChargeLimit = settings.ChargeLimit,
            };
        }

        public static AppSettings Load()
        {
            lock (SyncRoot)
            {
                _cachedSettings ??= AppSettings.Load();
                return Clone(_cachedSettings);
            }
        }

        public static void Save(AppSettings settings)
        {
            lock (SyncRoot)
            {
                _cachedSettings = Clone(settings);
            }

            AppSettings.Save(settings.WindowLeft, settings.WindowTop, settings.ChargeLimit);
        }

        public static void SaveChargeLimit(int chargeLimit)
        {
            lock (SyncRoot)
            {
                _cachedSettings ??= AppSettings.Load();
                _cachedSettings.ChargeLimit = chargeLimit;
                AppSettings.Save(_cachedSettings.WindowLeft, _cachedSettings.WindowTop, chargeLimit);
            }
        }

        public static void SaveWindowPosition(double left, double top)
        {
            lock (SyncRoot)
            {
                _cachedSettings ??= AppSettings.Load();
                _cachedSettings.WindowLeft = left;
                _cachedSettings.WindowTop = top;
                AppSettings.Save(left, top, _cachedSettings.ChargeLimit);
            }
        }
    }
}

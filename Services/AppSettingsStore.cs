using BatteryMonitor.Models;

namespace BatteryMonitor.Services
{
    public static class AppSettingsStore
    {
        private static readonly object SyncRoot = new();
        private static AppSettings? _cachedSettings;

        private static AppSettings GetSettings()
        {
            return _cachedSettings ??= AppSettings.Load();
        }

        private static AppSettings Clone(AppSettings settings)
        {
            return new AppSettings
            {
                WindowLeft = settings.WindowLeft,
                WindowTop = settings.WindowTop,
                ChargeLimit = settings.ChargeLimit
            };
        }

        public static AppSettings Load()
        {
            lock (SyncRoot)
            {
                return Clone(GetSettings());
            }
        }

        public static int LoadChargeLimit()
        {
            lock (SyncRoot)
            {
                return GetSettings().ChargeLimit;
            }
        }

        public static (double Left, double Top, bool HasValue) LoadWindowPosition()
        {
            lock (SyncRoot)
            {
                var settings = GetSettings();
                bool hasValue = double.IsFinite(settings.WindowLeft) && double.IsFinite(settings.WindowTop);
                return (settings.WindowLeft, settings.WindowTop, hasValue);
            }
        }

        public static void Save(AppSettings settings)
        {
            lock (SyncRoot)
            {
                _cachedSettings = Clone(settings);
                AppSettings.Save(settings.WindowLeft, settings.WindowTop, settings.ChargeLimit);
            }
        }

        public static void SaveChargeLimit(int chargeLimit)
        {
            lock (SyncRoot)
            {
                var settings = GetSettings();
                settings.ChargeLimit = Math.Clamp(chargeLimit, 1, 100);
                AppSettings.Save(settings.WindowLeft, settings.WindowTop, settings.ChargeLimit);
            }
        }

        public static void SaveWindowPosition(double left, double top)
        {
            lock (SyncRoot)
            {
                var settings = GetSettings();
                settings.WindowLeft = double.IsFinite(left) ? left : double.NaN;
                settings.WindowTop = double.IsFinite(top) ? top : double.NaN;
                AppSettings.Save(settings.WindowLeft, settings.WindowTop, settings.ChargeLimit);
            }
        }
    }
}

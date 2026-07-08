using BatteryMonitor.Models;

namespace BatteryMonitor.Services
{
    public static class AppSettingsStore
    {
        private static readonly object SyncRoot = new();
        private static bool _hasCachedWindowPosition;
        private static double _cachedWindowLeft = double.NaN;
        private static double _cachedWindowTop = double.NaN;

        private static void CacheWindowPosition(AppSettings settings)
        {
            _cachedWindowLeft = settings.WindowLeft;
            _cachedWindowTop = settings.WindowTop;
            _hasCachedWindowPosition = true;
        }

        public static AppSettings Load()
        {
            return AppSettings.Load();
        }

        public static int LoadChargeLimit()
        {
            return AppSettings.Load().ChargeLimit;
        }

        public static (double Left, double Top, bool HasValue) LoadWindowPosition()
        {
            lock (SyncRoot)
            {
                if (!_hasCachedWindowPosition)
                {
                    var settings = AppSettings.Load();
                    CacheWindowPosition(settings);
                }

                bool hasValue = !double.IsNaN(_cachedWindowLeft) && !double.IsNaN(_cachedWindowTop);
                return (_cachedWindowLeft, _cachedWindowTop, hasValue);
            }
        }

        public static void Save(AppSettings settings)
        {
            lock (SyncRoot)
            {
                CacheWindowPosition(settings);
            }

            AppSettings.Save(settings.WindowLeft, settings.WindowTop, settings.ChargeLimit);
        }

        public static void SaveChargeLimit(int chargeLimit)
        {
            var (left, top, hasValue) = LoadWindowPosition();
            if (!hasValue)
            {
                var settings = AppSettings.Load();
                left = settings.WindowLeft;
                top = settings.WindowTop;
            }

            AppSettings.Save(left, top, chargeLimit);
        }

        public static void SaveWindowPosition(double left, double top)
        {
            var chargeLimit = LoadChargeLimit();

            lock (SyncRoot)
            {
                _cachedWindowLeft = left;
                _cachedWindowTop = top;
                _hasCachedWindowPosition = true;
            }

            AppSettings.Save(left, top, chargeLimit);
        }
    }
}

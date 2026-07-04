using System;
using BatteryMonitor.Models;

namespace BatteryMonitor.Services
{
    public static class AppSettingsStore
    {
        private static readonly object SyncRoot = new();
        private static AppSettings? _current;

        public static AppSettings Current
        {
            get
            {
                lock (SyncRoot)
                {
                    return _current ??= AppSettings.Load();
                }
            }
        }

        public static AppSettings Load()
        {
            return Current;
        }

        public static void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            lock (SyncRoot)
            {
                _current = new AppSettings
                {
                    WindowLeft = settings.WindowLeft,
                    WindowTop = settings.WindowTop,
                    ChargeLimit = settings.ChargeLimit
                };
                PersistLocked();
            }
        }

        public static void SaveChargeLimit(int chargeLimit)
        {
            lock (SyncRoot)
            {
                var current = EnsureCurrentLocked();
                current.ChargeLimit = chargeLimit;
                PersistLocked();
            }
        }

        public static void SaveWindowPosition(double left, double top)
        {
            lock (SyncRoot)
            {
                var current = EnsureCurrentLocked();
                current.WindowLeft = left;
                current.WindowTop = top;
                PersistLocked();
            }
        }

        private static AppSettings EnsureCurrentLocked()
        {
            return _current ??= AppSettings.Load();
        }

        private static void PersistLocked()
        {
            if (_current == null)
            {
                _current = new AppSettings();
            }

            AppSettings.Save(_current.WindowLeft, _current.WindowTop, _current.ChargeLimit);
        }
    }
}

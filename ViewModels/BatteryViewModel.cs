using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Input;

using BatteryMonitor.Services;
using BatteryMonitor.Helpers;
using BatteryMonitor.Models;

namespace BatteryMonitor.ViewModels
{
    public class BatteryViewModel : INotifyPropertyChanged
    {
        private static readonly TimeSpan FullChargedCapacityRefreshInterval = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan VisibleTemperatureRefreshInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan HiddenTemperatureRefreshInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CycleCountRefreshInterval = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan VisibleSecondaryRefreshInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan HiddenSecondaryRefreshInterval = TimeSpan.FromMinutes(5);

        private readonly BatteryService _service;
        private bool _isUpdating = false;
        private DateTime _lastFullChargedCapacityRefresh = DateTime.MinValue;
        private DateTime _lastTemperatureRefresh = DateTime.MinValue;
        private DateTime _lastCycleCountRefresh = DateTime.MinValue;
        private DateTime _lastSecondaryRefresh = DateTime.MinValue;
        private bool _hasLoadedVisibleDetails = false;
        private int _lastIconBucket = -1;
        private bool? _lastIconChargingState;

        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly SvgIconGenerator _iconGenerator;

        private string _versionText = "v?";

        public BatteryViewModel()
        {
            _service = new BatteryService();
            string iconDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "TrayIconsIco");
            _iconGenerator = new SvgIconGenerator(iconDirectory);

            var settings = AppSettingsStore.Load();
            _chargeLimit = settings.ChargeLimit;
            TogglePinCommand = new RelayCommand(_ => IsPinned = !IsPinned);
            ToggleSettingsCommand = new RelayCommand(_ => IsSettingsOpen = !IsSettingsOpen);
        }

        public ICommand TogglePinCommand { get; }
        public ICommand ToggleSettingsCommand { get; }

        public string VersionText
        {
            get => _versionText;
            set { SetProperty(ref _versionText, value); }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(name);
            return true;
        }

        public async void UpdateData(bool isPopupVisible = false, bool forceSecondaryRefresh = false)
        {
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                var now = DateTime.Now;
                var secondaryInterval = isPopupVisible ? VisibleSecondaryRefreshInterval : HiddenSecondaryRefreshInterval;
                bool refreshFullChargedCapacity = now - _lastFullChargedCapacityRefresh >= FullChargedCapacityRefreshInterval;
                var temperatureInterval = isPopupVisible ? VisibleTemperatureRefreshInterval : HiddenTemperatureRefreshInterval;
                bool refreshTemperature = now - _lastTemperatureRefresh >= temperatureInterval;
                bool refreshCycleCount = now - _lastCycleCountRefresh >= CycleCountRefreshInterval;
                bool refreshSecondary = now - _lastSecondaryRefresh >= secondaryInterval;

                if (isPopupVisible && !_hasLoadedVisibleDetails)
                {
                    refreshSecondary = true;
                }

                if (forceSecondaryRefresh)
                {
                    refreshSecondary = true;
                }

                if (refreshFullChargedCapacity)
                {
                    _lastFullChargedCapacityRefresh = now;
                }

                if (refreshTemperature)
                {
                    _lastTemperatureRefresh = now;
                }

                if (refreshCycleCount)
                {
                    _lastCycleCountRefresh = now;
                }

                if (refreshSecondary && isPopupVisible)
                {
                    _lastSecondaryRefresh = now;
                }

                // バックグラウンドスレッドでデータを取得・生成
                var data = await System.Threading.Tasks.Task.Run(() => 
                {
                    return _service.GetBatteryStatus(refreshFullChargedCapacity, refreshCycleCount, refreshTemperature);
                });

                // --- UIスレッドでの更新処理 ---

                double powerW = (data.IsCharging ? data.ChargeRate : data.DischargeRate) / 1000.0;
                double voltageV = data.Voltage / 1000.0;
                double currentA = (voltageV > 0) ? (powerW / voltageV) : 0;

                BatteryLevel = BatteryDisplayFormatter.FormatBatteryLevel(data);
                IsCharging = data.IsCharging;
                MainStatusText = BatteryDisplayFormatter.FormatMainStatus(data.IsCharging);
                PowerRate = BatteryDisplayFormatter.FormatPowerRate(data, powerW);
                SubStatusText = BatteryDisplayFormatter.FormatSubStatus(data, powerW, voltageV, currentA);
                UpdateTrayIconIfNeeded(data);

                if (!isPopupVisible || !refreshSecondary)
                {
                    return;
                }

                _hasLoadedVisibleDetails = true;

                Health = BatteryDisplayFormatter.FormatHealth(data);
                CycleCount = BatteryDisplayFormatter.FormatCycleCount(data);
                Voltage = BatteryDisplayFormatter.FormatVoltage(voltageV);
                RemainingTime = BatteryDisplayFormatter.FormatRemainingTime(data, ChargeLimit);
                Temperature = BatteryDisplayFormatter.FormatTemperature(data.Temperature);
                CapacityDetail = BatteryDisplayFormatter.FormatCapacityDetail(data);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in UpdateData", ex);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        // --- プロパティ群 ---
        private string _batteryLevel = "--";
        public string BatteryLevel
        {
            get => _batteryLevel;
            set { SetProperty(ref _batteryLevel, value); }
        }



        private string _mainStatusText = "---";
        public string MainStatusText
        {
            get => _mainStatusText;
            set { SetProperty(ref _mainStatusText, value); }
        }

        private string _subStatusText = "---";
        public string SubStatusText
        {
            get => _subStatusText;
            set { SetProperty(ref _subStatusText, value); }
        }

        private string _powerRate = "---";
        public string PowerRate
        {
            get => _powerRate;
            set { SetProperty(ref _powerRate, value); }
        }

        private string _voltage = "---";
        public string Voltage
        {
            get => _voltage;
            set { SetProperty(ref _voltage, value); }
        }

        private string _cycleCount = "-- 回";
        public string CycleCount
        {
            get => _cycleCount;
            set { SetProperty(ref _cycleCount, value); }
        }

        private string _health = "---";
        public string Health
        {
            get => _health;
            set { SetProperty(ref _health, value); }
        }

        // --- フェーズ2 新規プロパティ ---
        private string _remainingTime = "--";
        public string RemainingTime
        {
            get => _remainingTime;
            set { SetProperty(ref _remainingTime, value); }
        }

        private string _capacityDetail = "-- / -- Wh";
        public string CapacityDetail
        {
            get => _capacityDetail;
            set { SetProperty(ref _capacityDetail, value); }
        }

        private string _temperature = "-- °C";
        public string Temperature
        {
            get => _temperature;
            set { SetProperty(ref _temperature, value); }
        }

        private int _chargeLimit = 100;
        public int ChargeLimit
        {
            get => _chargeLimit;
            set 
            { 
                if (_chargeLimit != value)
                {
                    _chargeLimit = value; 
                    OnPropertyChanged();
                    AppSettingsStore.SaveChargeLimit(_chargeLimit);
                }
            }
        }

        private bool _isCharging;
        public bool IsCharging
        {
            get => _isCharging;
            set 
            {
                if (_isCharging != value)
                {
                    _isCharging = value;
                    Logger.Info($"IsCharging changed to: {_isCharging}");
                    OnPropertyChanged();
                }
            }
        }

        private bool _isPinned = false;
        public bool IsPinned
        {
            get => _isPinned;
            set { SetProperty(ref _isPinned, value); }
        }

        private bool _isSettingsOpen = false;
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set { SetProperty(ref _isSettingsOpen, value); }
        }

        public bool IsStartupEnabled
        {
            get => StartupManager.IsStartupEnabled();
            set
            {
                StartupManager.SetStartup(value);
                OnPropertyChanged();
            }
        }

        private ImageSource? _trayIconSource;
        public ImageSource? TrayIconSource
        {
            get => _trayIconSource;
            set { SetProperty(ref _trayIconSource, value); }
        }

        private void UpdateTrayIconIfNeeded(BatteryInfo data)
        {
            int iconBucket = GetIconBucket((int)data.Percent);
            bool chargingState = data.IsCharging;

            if (TrayIconSource != null && iconBucket == _lastIconBucket && chargingState == _lastIconChargingState)
            {
                return;
            }

            TrayIconSource = _iconGenerator.GenerateIcon(iconBucket, chargingState);
            _lastIconBucket = iconBucket;
            _lastIconChargingState = chargingState;
        }

        private static int GetIconBucket(int percent)
        {
            if (percent >= 100) return 100;
            if (percent <= 0) return 0;

            return (percent / 10) * 10;
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

using BatteryMonitor.Helpers;
using BatteryMonitor.Models;
using BatteryMonitor.Services;

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
        private readonly SemaphoreSlim _updateGate = new(1, 1);
        private readonly SvgIconGenerator _iconGenerator;

        private DateTime _lastFullChargedCapacityRefresh = DateTime.MinValue;
        private DateTime _lastTemperatureRefresh = DateTime.MinValue;
        private DateTime _lastCycleCountRefresh = DateTime.MinValue;
        private DateTime _lastSecondaryRefresh = DateTime.MinValue;
        private bool _hasLoadedVisibleDetails = false;
        private int _lastIconBucket = -1;
        private bool? _lastIconChargingState;

        private string _versionText = "v?";
        private string _batteryLevel = "--";
        private string _mainStatusText = "---";
        private string _subStatusText = "---";
        private string _powerRate = "---";
        private string _voltage = "---";
        private string _cycleCount = "-- 回";
        private string _health = "---";
        private string _remainingTime = "--";
        private string _capacityDetail = "-- / -- Wh";
        private string _temperature = "-- °C";
        private int _chargeLimit = 100;
        private bool _isCharging;
        private bool _isPinned = false;
        private bool _isSettingsOpen = false;
        private ImageSource? _trayIconSource;

        public event PropertyChangedEventHandler? PropertyChanged;

        public BatteryViewModel()
        {
            _service = new BatteryService();
            string iconDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "TrayIconsIco");
            _iconGenerator = new SvgIconGenerator(iconDirectory);

            _chargeLimit = ClampChargeLimit(AppSettingsStore.LoadChargeLimit());
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

        public Task UpdateData(bool isPopupVisible = false, bool forceSecondaryRefresh = false)
        {
            return UpdateDataCoreAsync(isPopupVisible, forceSecondaryRefresh);
        }

        private async Task UpdateDataCoreAsync(bool isPopupVisible, bool forceSecondaryRefresh)
        {
            // 更新を捨てずに順番待ちにして、開いた直後の取りこぼしを防ぐ
            await _updateGate.WaitAsync();

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

                int chargeLimit = ChargeLimit;
                var snapshot = await Task.Run(() =>
                {
                    var data = _service.GetBatteryStatus(refreshFullChargedCapacity, refreshCycleCount, refreshTemperature);

                    double powerW = (data.IsCharging ? data.ChargeRate : data.DischargeRate) / 1000.0;
                    double voltageV = data.Voltage / 1000.0;
                    double currentA = (voltageV > 0) ? (powerW / voltageV) : 0;

                    return new BatterySnapshot(
                        BatteryDisplayFormatter.FormatBatteryLevel(data),
                        data.IsCharging,
                        BatteryDisplayFormatter.FormatMainStatus(data),
                        BatteryDisplayFormatter.FormatPowerRate(data, powerW),
                        BatteryDisplayFormatter.FormatSubStatus(data, powerW, voltageV, currentA),
                        BatteryDisplayFormatter.FormatHealth(data),
                        BatteryDisplayFormatter.FormatCycleCount(data),
                        BatteryDisplayFormatter.FormatVoltage(voltageV),
                        BatteryDisplayFormatter.FormatRemainingTime(data, chargeLimit),
                        BatteryDisplayFormatter.FormatCapacityDetail(data),
                        data.IsAvailable ? BatteryDisplayFormatter.FormatTemperature(data.Temperature) : "-- °C",
                        GetIconBucket((int)data.Percent),
                        data.IsCharging,
                        data.Availability);
                });

                if (snapshot.Availability == BatteryAvailability.Available)
                {
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
                }

                BatteryLevel = snapshot.BatteryLevel;
                IsCharging = snapshot.IsCharging;
                MainStatusText = snapshot.MainStatusText;
                PowerRate = snapshot.PowerRate;
                SubStatusText = snapshot.SubStatusText;
                if (snapshot.Availability == BatteryAvailability.Available)
                {
                    UpdateTrayIconIfNeeded(snapshot.TrayIconBucket, snapshot.TrayIconChargingState);
                }
                else if (TrayIconSource == null)
                {
                    TrayIconSource = _iconGenerator.GenerateFallbackIcon();
                }

                if (!isPopupVisible || !refreshSecondary)
                {
                    return;
                }

                _hasLoadedVisibleDetails = true;

                Health = snapshot.Health;
                CycleCount = snapshot.CycleCount;
                Voltage = snapshot.Voltage;
                RemainingTime = snapshot.RemainingTime;
                Temperature = snapshot.Temperature;
                CapacityDetail = snapshot.CapacityDetail;
            }
            catch (Exception ex)
            {
                Logger.Error("Error in UpdateData", ex);
            }
            finally
            {
                _updateGate.Release();
            }
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

        public string BatteryLevel
        {
            get => _batteryLevel;
            set { SetProperty(ref _batteryLevel, value); }
        }

        public string MainStatusText
        {
            get => _mainStatusText;
            set { SetProperty(ref _mainStatusText, value); }
        }

        public string SubStatusText
        {
            get => _subStatusText;
            set { SetProperty(ref _subStatusText, value); }
        }

        public string PowerRate
        {
            get => _powerRate;
            set { SetProperty(ref _powerRate, value); }
        }

        public string Voltage
        {
            get => _voltage;
            set { SetProperty(ref _voltage, value); }
        }

        public string CycleCount
        {
            get => _cycleCount;
            set { SetProperty(ref _cycleCount, value); }
        }

        public string Health
        {
            get => _health;
            set { SetProperty(ref _health, value); }
        }

        public string RemainingTime
        {
            get => _remainingTime;
            set { SetProperty(ref _remainingTime, value); }
        }

        public string CapacityDetail
        {
            get => _capacityDetail;
            set { SetProperty(ref _capacityDetail, value); }
        }

        public string Temperature
        {
            get => _temperature;
            set { SetProperty(ref _temperature, value); }
        }

        public int ChargeLimit
        {
            get => _chargeLimit;
            set
            {
                int validatedValue = ClampChargeLimit(value);
                if (_chargeLimit != validatedValue)
                {
                    _chargeLimit = validatedValue;
                    OnPropertyChanged();
                    AppSettingsStore.SaveChargeLimit(_chargeLimit);
                }
                else if (value != validatedValue)
                {
                    OnPropertyChanged();
                }
            }
        }

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

        public bool IsPinned
        {
            get => _isPinned;
            set { SetProperty(ref _isPinned, value); }
        }

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
                if (!StartupManager.TrySetStartup(value))
                {
                    OnPropertyChanged();
                    return;
                }

                OnPropertyChanged();
            }
        }

        public ImageSource? TrayIconSource
        {
            get => _trayIconSource;
            set { SetProperty(ref _trayIconSource, value); }
        }

        private void UpdateTrayIconIfNeeded(int iconBucket, bool chargingState)
        {
            if (TrayIconSource != null &&
                iconBucket == _lastIconBucket &&
                chargingState == _lastIconChargingState)
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

        private static int ClampChargeLimit(int value)
        {
            return Math.Clamp(value, 1, 100);
        }

        private sealed record BatterySnapshot(
            string BatteryLevel,
            bool IsCharging,
            string MainStatusText,
            string PowerRate,
            string SubStatusText,
            string Health,
            string CycleCount,
            string Voltage,
            string RemainingTime,
            string CapacityDetail,
            string Temperature,
            int TrayIconBucket,
            bool TrayIconChargingState,
            BatteryAvailability Availability);
    }
}

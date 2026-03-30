using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Input;

using BatteryMonitor3.Services;
using BatteryMonitor3.Helpers;
using BatteryMonitor3.Models;

namespace BatteryMonitor3.ViewModels
{
    public class BatteryViewModel : INotifyPropertyChanged
    {
        private static readonly TimeSpan VisibleTemperatureRefreshInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan HiddenTemperatureRefreshInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CycleCountRefreshInterval = TimeSpan.FromMinutes(30);

        private readonly BatteryService _service;
        private bool _isUpdating = false;
        private DateTime _lastTemperatureRefresh = DateTime.MinValue;
        private DateTime _lastCycleCountRefresh = DateTime.MinValue;
        private int _lastIconBucket = -1;
        private bool? _lastIconChargingState;

        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly SvgIconGenerator _iconGenerator;

        public BatteryViewModel()
        {
            _service = new BatteryService();
            string iconDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "TrayIconsIco");
            _iconGenerator = new SvgIconGenerator(iconDirectory);

            var settings = AppSettings.Load();
            _chargeLimit = settings.ChargeLimit;
            TogglePinCommand = new RelayCommand(_ => IsPinned = !IsPinned);
            ToggleSettingsCommand = new RelayCommand(_ => IsSettingsOpen = !IsSettingsOpen);
        }

        public ICommand TogglePinCommand { get; }
        public ICommand ToggleSettingsCommand { get; }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public async void UpdateData(bool isPopupVisible = false)
        {
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                var now = DateTime.Now;
                var temperatureInterval = isPopupVisible ? VisibleTemperatureRefreshInterval : HiddenTemperatureRefreshInterval;
                bool refreshTemperature = now - _lastTemperatureRefresh >= temperatureInterval;
                bool refreshCycleCount = now - _lastCycleCountRefresh >= CycleCountRefreshInterval;

                if (refreshTemperature)
                {
                    _lastTemperatureRefresh = now;
                }

                if (refreshCycleCount)
                {
                    _lastCycleCountRefresh = now;
                }

                // バックグラウンドスレッドでデータを取得・生成
                var data = await System.Threading.Tasks.Task.Run(() => 
                {
                    return _service.GetBatteryStatus(refreshCycleCount, refreshTemperature);
                });

                // --- UIスレッドでの更新処理 ---

                // 1. 電力 (W)
                double powerW = (data.IsCharging ? data.ChargeRate : data.DischargeRate) / 1000.0;

                // 2. 電圧 (V) - WMIからの正確な値
                double voltageV = data.Voltage / 1000.0;

                // 3. 電流 (A)
                double currentA = (voltageV > 0) ? (powerW / voltageV) : 0;

                // 4. 残量 (%)
                BatteryLevel = $"{data.Percent}";

                // トレイ維持に必要な最低限だけ先に更新し、非表示中の文字列整形は避ける。
                IsCharging = data.IsCharging;
                UpdateTrayIconIfNeeded(data);

                if (!isPopupVisible)
                {
                    return;
                }

                // 5. 健康度 (%) - WMIからの正確な値
                double health = 0;
                if (data.DesignCapacity > 0 && data.FullChargedCapacity > 0)
                {
                    health = Math.Min(100.0, (double)data.FullChargedCapacity * 100 / data.DesignCapacity);
                }
                Health = (health > 0) ? $"{health:F0} %" : "-- %";

                // 6. サイクルカウント
                CycleCount = (data.CycleCount > 0) ? $"{data.CycleCount} 回" : "-- 回"; // 0の場合は "-- 回"

                // --- UIプロパティの更新 ---
                Voltage = (voltageV > 0) ? $"{voltageV:F1} V" : "-- V";
                PowerRate = (powerW > 0) ? ((data.IsCharging ? "+" : "-") + $"{powerW:F1} W") : "-- W";

                if (data.IsCharging)
                {
                    MainStatusText = "充電中";
                    
                    // 充電完了までの時間 (設定された上限まで)
                    if (data.ChargeRate > 0)
                    {
                        double targetCapacity = data.FullChargedCapacity * (ChargeLimit / 100.0);
                        double neededCapacity = targetCapacity - data.RemainingCapacity;

                        if (neededCapacity <= 0)
                        {
                            RemainingTime = $"充電制限({ChargeLimit}%)に到達";
                        }
                        else
                        {
                            double hoursLeft = neededCapacity / data.ChargeRate;
                            TimeSpan ts = TimeSpan.FromHours(hoursLeft);
                            RemainingTime = $"あと {ts.Hours}時間 {ts.Minutes}分 ({ChargeLimit}%まで)";
                        }
                    }
                    else
                    {
                        RemainingTime = "計算中...";
                    }

                    SubStatusText = (voltageV > 0 && currentA > 0)
                        ? $"{powerW:F1}W ({voltageV:F1}V / {currentA:F1}A)"
                        : $"{powerW:F1}W";
                }
                else
                {
                    MainStatusText = "バッテリー使用中";
                    
                    // 残り駆動時間
                    if (data.DischargeRate > 0)
                    {
                        double hoursLeft = (double)data.RemainingCapacity / data.DischargeRate;
                        TimeSpan ts = TimeSpan.FromHours(hoursLeft);
                        RemainingTime = $"あと {ts.Hours}時間 {ts.Minutes}分";
                    }
                    else
                    {
                        RemainingTime = "-- 時間 -- 分";
                    }

                    SubStatusText = (powerW > 0) ? $"消費: {powerW:F1}W" : "待機中";
                }

                // 新しいデータ項目の更新
                Temperature = (data.Temperature > -270) ? $"{data.Temperature:F1} °C" : "-- °C";
                
                // mWh -> Wh
                double remWh = data.RemainingCapacity / 1000.0;
                double fullWh = data.FullChargedCapacity / 1000.0;
                CapacityDetail = $"{remWh:F1} / {fullWh:F1} Wh";
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
            set { _batteryLevel = value; OnPropertyChanged(); }
        }



        private string _mainStatusText = "---";
        public string MainStatusText
        {
            get => _mainStatusText;
            set { _mainStatusText = value; OnPropertyChanged(); }
        }

        private string _subStatusText = "---";
        public string SubStatusText
        {
            get => _subStatusText;
            set { _subStatusText = value; OnPropertyChanged(); }
        }

        private string _powerRate = "---";
        public string PowerRate
        {
            get => _powerRate;
            set { _powerRate = value; OnPropertyChanged(); }
        }

        private string _voltage = "---";
        public string Voltage
        {
            get => _voltage;
            set { _voltage = value; OnPropertyChanged(); }
        }

        private string _cycleCount = "-- 回";
        public string CycleCount
        {
            get => _cycleCount;
            set { _cycleCount = value; OnPropertyChanged(); }
        }

        private string _health = "---";
        public string Health
        {
            get => _health;
            set { _health = value; OnPropertyChanged(); }
        }

        // --- フェーズ2 新規プロパティ ---
        private string _remainingTime = "--";
        public string RemainingTime
        {
            get => _remainingTime;
            set { _remainingTime = value; OnPropertyChanged(); }
        }

        private string _capacityDetail = "-- / -- Wh";
        public string CapacityDetail
        {
            get => _capacityDetail;
            set { _capacityDetail = value; OnPropertyChanged(); }
        }

        private string _temperature = "-- °C";
        public string Temperature
        {
            get => _temperature;
            set { _temperature = value; OnPropertyChanged(); }
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
                    // 変更があった場合、設定を即座に保存
                    // 注意: 本来はスロットリングを行うか、終了時に保存すべきだが、簡略化のためここで保存
                    AppSettings.Save(double.NaN, double.NaN, _chargeLimit); // Window位置を維持または無視するためにNaNを使用

                    // AppSettings.Saveは上書きするため、現在の設定を読み込んでから保存し直す
                    var current = AppSettings.Load();
                    AppSettings.Save(current.WindowLeft, current.WindowTop, _chargeLimit);
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
            set { _isPinned = value; OnPropertyChanged(); }
        }

        private bool _isSettingsOpen = false;
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set { _isSettingsOpen = value; OnPropertyChanged(); }
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
            set { _trayIconSource = value; OnPropertyChanged(); }
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

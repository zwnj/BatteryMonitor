using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Input;

namespace BatteryMonitor3
{
    public class BatteryViewModel : INotifyPropertyChanged
    {
        private readonly BatteryService _service;

        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly SvgIconGenerator _iconGenerator;

        public BatteryViewModel()
        {
            _service = new BatteryService();
            // Initialize SVG generator
            // Assuming test.svg is in the BaseDirectory
            string svgPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test.svg");
            _iconGenerator = new SvgIconGenerator(svgPath);

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

        public void UpdateData()
        {
            try
            {
                var data = _service.GetBatteryStatus();

                // 1. 電力 (W)
                double powerW = (data.IsCharging ? data.ChargeRate : data.DischargeRate) / 1000.0;

                // 2. 電圧 (V) - WMIからの正確な値
                double voltageV = data.Voltage / 1000.0;

                // 3. 電流 (A)
                double currentA = (voltageV > 0) ? (powerW / voltageV) : 0;

                // 4. 残量 (%)
                BatteryLevel = $"{data.Percent}";

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
                    IsCharging = true;
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
                    IsCharging = false;
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

                // Update Icon
                TrayIconSource = _iconGenerator.GenerateIcon((int)data.Percent, data.IsCharging);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in UpdateData", ex);
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

        // --- Phase 2 New Properties ---
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
                    // Save settings immediately when changed
                    // Note: Ideally we should throttle this or save on exit, but for simplicity:
                    AppSettings.Save(double.NaN, double.NaN, _chargeLimit); // Use NaN to preserve/ignore window pos if logic supports, OR we need to fetch current window pos.
                    // Actually, AppSettings.Save overwrites everything. We need a better way or just load-modify-save.
                    // Let's reload to get current window pos, update limit, then save.
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
    }
}

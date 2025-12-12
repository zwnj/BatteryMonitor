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

        public BatteryViewModel()
        {
            _service = new BatteryService();
            TogglePinCommand = new RelayCommand(_ => IsPinned = !IsPinned);
        }

        public ICommand TogglePinCommand { get; }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void UpdateData()
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
                StatusColor = Brushes.LightGreen;
                MainStatusText = "充電中";
                SubStatusText = (voltageV > 0 && currentA > 0)
                    ? $"{powerW:F1}W ({voltageV:F1}V / {currentA:F1}A)"
                    : $"{powerW:F1}W";
            }
            else
            {
                StatusColor = Brushes.White;
                MainStatusText = "バッテリー使用中";
                SubStatusText = (powerW > 0) ? $"消費: {powerW:F1}W" : "待機中";
            }
        }

        // --- プロパティ群 ---
        private string _batteryLevel = "--";
        public string BatteryLevel
        {
            get => _batteryLevel;
            set { _batteryLevel = value; OnPropertyChanged(); }
        }

        private Brush _statusColor = Brushes.White;
        public Brush StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
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

        private string _cycleCount = "-- 回"; // 変更: Temperature -> CycleCount
        public string CycleCount // 変更: Temperature -> CycleCount
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

        private bool _isPinned = false;
        public bool IsPinned
        {
            get => _isPinned;
            set { _isPinned = value; OnPropertyChanged(); }
        }
    }
}

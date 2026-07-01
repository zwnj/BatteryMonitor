namespace BatteryMonitor3.Models
{
    // アプリ内データ受け渡し用
    public struct BatteryInfo
    {
        public bool IsCharging { get; set; }
        public uint ChargeRate { get; set; }
        public uint DischargeRate { get; set; }
        public uint Voltage { get; set; } // mV
        public uint RemainingCapacity { get; set; } // mWh
        public uint FullChargedCapacity { get; set; } // mWh
        public uint DesignCapacity { get; set; } // mWh
        public uint Percent { get; set; }
        public uint CycleCount { get; set; } // Count
        public double Temperature { get; set; } // Celsius
    }
}

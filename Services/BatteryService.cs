using System;
using System.Linq;
using System.Management;
using BatteryMonitor.Models;
using BatteryMonitor.Helpers;

namespace BatteryMonitor.Services
{
    public class BatteryService
    {
        private bool _supportsTemperature = true;
        private uint? _cachedDesignCapacity;
        private uint _cachedFullChargedCapacity;
        private uint _cachedCycleCount;
        private double _cachedTemperature = 0;

        public BatteryInfo GetBatteryStatus(
            bool refreshFullChargedCapacity = false,
            bool refreshCycleCount = false,
            bool refreshTemperature = false)
        {
            var info = new BatteryInfo();

            try
            {
                // --- 必須のバッテリー情報 ---
                var status = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM BatteryStatus")
                    .Get().Cast<ManagementObject>().FirstOrDefault();

                if (status != null)
                {
                    info.IsCharging = (bool)status["Charging"];
                    info.Voltage = Convert.ToUInt32(status["Voltage"]);
                    info.RemainingCapacity = Convert.ToUInt32(status["RemainingCapacity"]);
                    info.ChargeRate = Convert.ToUInt32(status["ChargeRate"]);
                    info.DischargeRate = Convert.ToUInt32(status["DischargeRate"]);
                }

                if (!_cachedDesignCapacity.HasValue)
                {
                    var staticData = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM BatteryStaticData")
                        .Get().Cast<ManagementObject>().FirstOrDefault();
                    if (staticData != null)
                    {
                        _cachedDesignCapacity = Convert.ToUInt32(staticData["DesignedCapacity"]);
                    }
                }
                info.DesignCapacity = _cachedDesignCapacity ?? 0;

                if (_cachedFullChargedCapacity == 0 || refreshFullChargedCapacity)
                {
                    var fullCharge = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM BatteryFullChargedCapacity")
                        .Get().Cast<ManagementObject>().FirstOrDefault();
                    if (fullCharge != null)
                    {
                        _cachedFullChargedCapacity = Convert.ToUInt32(fullCharge["FullChargedCapacity"]);
                    }
                }
                info.FullChargedCapacity = _cachedFullChargedCapacity;

                if (info.FullChargedCapacity > 0)
                {
                    info.Percent = (uint)Math.Min(100, (100.0 * info.RemainingCapacity / info.FullChargedCapacity));
                }
            }
            catch (ManagementException ex)
            {
                // 主要なWMI情報が取得できなければ、ここで処理を中断
                Logger.Error("Failed to get primary battery info", ex);
                return info;
            }

            // --- 補助情報: サイクルカウント ---
            if (refreshCycleCount)
            {
                try
                {
                    var cycleCountData = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM BatteryCycleCount")
                        .Get().Cast<ManagementObject>().FirstOrDefault();
                    if (cycleCountData != null)
                    {
                        _cachedCycleCount = Convert.ToUInt32(cycleCountData["CycleCount"]);
                    }
                }
                catch (ManagementException ex)
                {
                    Logger.Error("Failed to get cycle count", ex);
                }
            }
            info.CycleCount = _cachedCycleCount;

            // --- 補助情報: 温度 (管理者権限が必要) ---
            if (_supportsTemperature && refreshTemperature)
            {
                try
                {
                    var thermalData = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature")
                        .Get().Cast<ManagementObject>().FirstOrDefault();
                    if (thermalData != null)
                    {
                        // 単位: 1/10 Kelvin
                        // Celsius = (K - 273.15)
                        uint rawTemp = Convert.ToUInt32(thermalData["CurrentTemperature"]);
                        _cachedTemperature = (rawTemp / 10.0) - 273.15;
                    }
                }
                catch (ManagementException ex)
                {
                    Logger.Error("Failed to get temperature (Disabling temperature check)", ex);
                    _supportsTemperature = false;
                    _cachedTemperature = 0;
                }
            }
            info.Temperature = _cachedTemperature;

            return info;
        }
    }
}

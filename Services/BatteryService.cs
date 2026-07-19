using System;
using System.Linq;
using System.Management;

using BatteryMonitor.Helpers;
using BatteryMonitor.Models;

namespace BatteryMonitor.Services
{
    public class BatteryService
    {
        private const string Scope = @"root\WMI";
        private const string BatteryStatusQuery = "SELECT Charging, PowerOnline, Discharging, Voltage, RemainingCapacity, ChargeRate, DischargeRate FROM BatteryStatus";
        private const string BatteryStaticDataQuery = "SELECT DesignedCapacity FROM BatteryStaticData";
        private const string BatteryFullChargedCapacityQuery = "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity";
        private const string BatteryCycleCountQuery = "SELECT CycleCount FROM BatteryCycleCount";
        private const string ThermalZoneTemperatureQuery = "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature";

        private bool _supportsTemperature = true;
        private uint? _cachedDesignCapacity;
        private uint _cachedFullChargedCapacity;
        private uint _cachedCycleCount;
        private double _cachedTemperature = double.NaN;

        public BatteryInfo GetBatteryStatus(
            bool refreshFullChargedCapacity = false,
            bool refreshCycleCount = false,
            bool refreshTemperature = false)
        {
            var info = new BatteryInfo
            {
                Availability = BatteryAvailability.Error,
                Temperature = double.NaN
            };

            try
            {
                using var status = QuerySingle(Scope, BatteryStatusQuery);

                if (status == null)
                {
                    info.Availability = BatteryAvailability.NotPresent;
                    return info;
                }

                info.Availability = BatteryAvailability.Available;
                info.IsCharging = ReadBoolean(status, "Charging");
                info.PowerOnline = ReadBoolean(status, "PowerOnline");
                info.IsDischarging = ReadBoolean(status, "Discharging");
                info.Voltage = ReadUInt32(status, "Voltage");
                info.RemainingCapacity = ReadUInt32(status, "RemainingCapacity");
                info.ChargeRate = ReadUInt32(status, "ChargeRate");
                info.DischargeRate = ReadUInt32(status, "DischargeRate");

                if (!_cachedDesignCapacity.HasValue)
                {
                    using var staticData = QuerySingle(Scope, BatteryStaticDataQuery);
                    if (staticData != null)
                    {
                        _cachedDesignCapacity = ReadUInt32(staticData, "DesignedCapacity");
                    }
                }
                info.DesignCapacity = _cachedDesignCapacity ?? 0;

                if (_cachedFullChargedCapacity == 0 || refreshFullChargedCapacity)
                {
                    using var fullCharge = QuerySingle(Scope, BatteryFullChargedCapacityQuery);
                    if (fullCharge != null)
                    {
                        _cachedFullChargedCapacity = ReadUInt32(fullCharge, "FullChargedCapacity");
                    }
                }
                info.FullChargedCapacity = _cachedFullChargedCapacity;

                if (info.FullChargedCapacity > 0)
                {
                    info.Percent = (uint)Math.Min(100, (100.0 * info.RemainingCapacity / info.FullChargedCapacity));
                }
            }
            catch (Exception ex) when (ex is ManagementException or InvalidCastException or FormatException or OverflowException)
            {
                Logger.Error("Failed to get primary battery info", ex);
                info.Availability = BatteryAvailability.Error;
                return info;
            }

            if (refreshCycleCount)
            {
                try
                {
                    using var cycleCountData = QuerySingle(Scope, BatteryCycleCountQuery);
                    if (cycleCountData != null)
                    {
                        _cachedCycleCount = ReadUInt32(cycleCountData, "CycleCount");
                    }
                }
                catch (ManagementException ex)
                {
                    Logger.Error("Failed to get cycle count", ex);
                }
            }
            info.CycleCount = _cachedCycleCount;

            if (_supportsTemperature && refreshTemperature)
            {
                try
                {
                    using var thermalData = QuerySingle(Scope, ThermalZoneTemperatureQuery);
                    if (thermalData != null)
                    {
                        uint rawTemp = ReadUInt32(thermalData, "CurrentTemperature");
                        _cachedTemperature = (rawTemp / 10.0) - 273.15;
                    }
                }
                catch (ManagementException ex)
                {
                    Logger.Error("Failed to get temperature (Disabling temperature check)", ex);
                    _supportsTemperature = false;
                    _cachedTemperature = double.NaN;
                }
            }
            info.Temperature = _cachedTemperature;

            return info;
        }

        private static ManagementObject? QuerySingle(string scope, string query)
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            using var results = searcher.Get();
            return results.Cast<ManagementObject>().FirstOrDefault();
        }

        private static bool ReadBoolean(ManagementBaseObject source, string propertyName)
        {
            object? value = source[propertyName];
            if (value == null || value == DBNull.Value)
            {
                return false;
            }

            try
            {
                return Convert.ToBoolean(value);
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException)
            {
                Logger.Error($"Invalid WMI boolean value: {propertyName}", ex);
                return false;
            }
        }

        private static uint ReadUInt32(ManagementBaseObject source, string propertyName)
        {
            object? value = source[propertyName];
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            try
            {
                return Convert.ToUInt32(value);
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                Logger.Error($"Invalid WMI numeric value: {propertyName}", ex);
                return 0;
            }
        }
    }
}

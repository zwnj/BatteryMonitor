using BatteryMonitor.Models;

namespace BatteryMonitor.Helpers
{
    public static class BatteryDisplayFormatter
    {
        public static string FormatBatteryLevel(BatteryInfo data)
        {
            return data.IsAvailable ? $"{data.Percent}" : "--";
        }

        public static string FormatMainStatus(bool isCharging)
        {
            return isCharging ? "充電中" : "バッテリー使用中";
        }

        public static string FormatMainStatus(BatteryInfo data)
        {
            if (!data.IsAvailable)
            {
                return data.Availability == BatteryAvailability.NotPresent
                    ? "バッテリーなし"
                    : "取得できません";
            }

            if (data.IsCharging)
            {
                return "充電中";
            }

            if (data.PowerOnline)
            {
                return "AC接続中";
            }

            return "バッテリー使用中";
        }

        public static string FormatPowerRate(BatteryInfo data, double powerW)
        {
            if (!data.IsAvailable)
            {
                return "-- W";
            }

            return (powerW > 0)
                ? ((data.IsCharging ? "+" : "-") + $"{powerW:F1} W")
                : (data.PowerOnline ? "0.0 W" : "-- W");
        }

        public static string FormatSubStatus(BatteryInfo data, double powerW, double voltageV, double currentA)
        {
            if (!data.IsAvailable)
            {
                return "---";
            }

            if (data.IsCharging)
            {
                return (voltageV > 0 && currentA > 0)
                    ? $"{powerW:F1}W ({voltageV:F1}V / {currentA:F1}A)"
                    : $"{powerW:F1}W";
            }

            if (data.PowerOnline)
            {
                return "AC接続中";
            }

            return (powerW > 0) ? $"消費: {powerW:F1}W" : "待機中";
        }

        public static string FormatHealth(BatteryInfo data)
        {
            if (!data.IsAvailable)
            {
                return "-- %";
            }

            if (data.DesignCapacity > 0 && data.FullChargedCapacity > 0)
            {
                var health = System.Math.Min(100.0, (double)data.FullChargedCapacity * 100 / data.DesignCapacity);
                return (health > 0) ? $"{health:F0} %" : "-- %";
            }

            return "-- %";
        }

        public static string FormatCycleCount(BatteryInfo data)
        {
            return data.IsAvailable && data.CycleCount > 0 ? $"{data.CycleCount} 回" : "-- 回";
        }

        public static string FormatVoltage(double voltageV)
        {
            return (voltageV > 0) ? $"{voltageV:F1} V" : "-- V";
        }

        public static string FormatRemainingTime(BatteryInfo data, int chargeLimit)
        {
            if (!data.IsAvailable)
            {
                return "-- 時間 -- 分";
            }

            if (data.IsCharging)
            {
                if (data.ChargeRate <= 0)
                {
                    return "計算中...";
                }

                double targetCapacity = data.FullChargedCapacity * (chargeLimit / 100.0);
                double neededCapacity = targetCapacity - data.RemainingCapacity;

                if (neededCapacity <= 0)
                {
                    return $"充電制限({chargeLimit}%)に到達";
                }

                double hoursLeft = neededCapacity / data.ChargeRate;
                return FormatDuration(hoursLeft, $" ({chargeLimit}%まで)");
            }

            if (data.DischargeRate > 0)
            {
                double hoursLeft = (double)data.RemainingCapacity / data.DischargeRate;
                return FormatDuration(hoursLeft);
            }

            return "-- 時間 -- 分";
        }

        public static string FormatTemperature(double temperature)
        {
            return (double.IsNaN(temperature) || temperature <= -270) ? "-- °C" : $"{temperature:F1} °C";
        }

        public static string FormatCapacityDetail(BatteryInfo data)
        {
            if (!data.IsAvailable)
            {
                return "-- / -- Wh";
            }

            double remWh = data.RemainingCapacity / 1000.0;
            double fullWh = data.FullChargedCapacity / 1000.0;
            return $"{remWh:F1} / {fullWh:F1} Wh";
        }

        private static string FormatDuration(double hours, string suffix = "")
        {
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0)
            {
                return "-- 時間 -- 分";
            }

            long totalMinutes = (long)System.Math.Floor(hours * 60);
            long totalHours = totalMinutes / 60;
            long minutes = totalMinutes % 60;
            return $"あと {totalHours}時間 {minutes}分{suffix}";
        }
    }
}

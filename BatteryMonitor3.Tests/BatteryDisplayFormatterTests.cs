using BatteryMonitor.Helpers;
using BatteryMonitor.Models;
using NUnit.Framework;

namespace BatteryMonitor3.Tests;

[TestFixture]
public sealed class BatteryDisplayFormatterTests
{
    [Test]
    public void UnavailableBattery_UsesPlaceholdersInsteadOfZeroValues()
    {
        var battery = new BatteryInfo { Availability = BatteryAvailability.Error };

        Assert.That(BatteryDisplayFormatter.FormatBatteryLevel(battery), Is.EqualTo("--"));
        Assert.That(BatteryDisplayFormatter.FormatMainStatus(battery), Is.EqualTo("取得できません"));
        Assert.That(BatteryDisplayFormatter.FormatPowerRate(battery, 0), Is.EqualTo("-- W"));
        Assert.That(BatteryDisplayFormatter.FormatCapacityDetail(battery), Is.EqualTo("-- / -- Wh"));
    }

    [Test]
    public void MissingBattery_HasDistinctStatus()
    {
        var battery = new BatteryInfo { Availability = BatteryAvailability.NotPresent };

        Assert.That(BatteryDisplayFormatter.FormatMainStatus(battery), Is.EqualTo("バッテリーなし"));
    }

    [Test]
    public void RemainingTime_DoesNotWrapAfterTwentyFourHours()
    {
        var battery = new BatteryInfo
        {
            Availability = BatteryAvailability.Available,
            RemainingCapacity = 150_000,
            DischargeRate = 1_000
        };

        Assert.That(BatteryDisplayFormatter.FormatRemainingTime(battery, 100), Is.EqualTo("あと 150時間 0分"));
    }

    [Test]
    public void ChargingPastLimit_ReportsReachedLimit()
    {
        var battery = new BatteryInfo
        {
            Availability = BatteryAvailability.Available,
            IsCharging = true,
            FullChargedCapacity = 100_000,
            RemainingCapacity = 80_000,
            ChargeRate = 10_000
        };

        Assert.That(BatteryDisplayFormatter.FormatRemainingTime(battery, 80), Is.EqualTo("充電制限(80%)に到達"));
    }
}

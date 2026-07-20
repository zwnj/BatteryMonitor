using BatteryMonitor.Infrastructure.Startup;
using NUnit.Framework;

namespace BatteryMonitor3.Tests.Infrastructure.Startup;

public sealed class ApplicationLaunchTests
{
    [Test]
    public void StartupArgumentIsCaseInsensitive()
    {
        Assert.That(ApplicationLaunchModeCalculator.IsStartupLaunch(["--STARTUP"]), Is.True);
    }

    [Test]
    public void NormalLaunchIsInteractive()
    {
        Assert.That(ApplicationLaunchModeCalculator.IsStartupLaunch([]), Is.False);
        Assert.That(ApplicationLaunchModeCalculator.IsStartupLaunch(["--other"]), Is.False);
    }

    [Test]
    public void StartupSecondaryExitsWithoutActivatingPrimary()
    {
        ApplicationLaunchDecision decision = ApplicationLaunchDecision.Calculate(
            isPrimaryInstance: false,
            isStartupLaunch: true);

        Assert.That(decision.ShouldContinueStartup, Is.False);
        Assert.That(decision.ShouldNotifyPrimary, Is.False);
    }

    [Test]
    public void ExplicitSecondaryActivatesPrimary()
    {
        ApplicationLaunchDecision decision = ApplicationLaunchDecision.Calculate(
            isPrimaryInstance: false,
            isStartupLaunch: false);

        Assert.That(decision.ShouldContinueStartup, Is.False);
        Assert.That(decision.ShouldNotifyPrimary, Is.True);
    }
}

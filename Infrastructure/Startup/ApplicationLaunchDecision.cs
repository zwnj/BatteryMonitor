namespace BatteryMonitor.Infrastructure.Startup;

internal sealed record ApplicationLaunchDecision(
    bool ShouldContinueStartup,
    bool ShouldNotifyPrimary)
{
    internal static ApplicationLaunchDecision Calculate(bool isPrimaryInstance, bool isStartupLaunch)
    {
        if (isPrimaryInstance)
        {
            return new ApplicationLaunchDecision(true, false);
        }

        return new ApplicationLaunchDecision(false, !isStartupLaunch);
    }
}

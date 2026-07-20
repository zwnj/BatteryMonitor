namespace BatteryMonitor.Infrastructure.Startup;

internal interface ILegacyStartupRegistration
{
    bool TryRemoveOwnedRegistration();
}

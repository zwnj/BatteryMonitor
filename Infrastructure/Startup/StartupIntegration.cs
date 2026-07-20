namespace BatteryMonitor.Infrastructure.Startup;

internal static class StartupIntegration
{
    internal static bool TryEnsureRegisteredAndMigrate()
    {
        try
        {
            return StartupRegistrationService.CreateForCurrentInstallation().TryEnsureRegisteredAndMigrate();
        }
        catch (Exception exception) when (IsExpectedIntegrationFailure(exception))
        {
            return false;
        }
    }

    internal static bool TryRemoveAllRegistrations()
    {
        try
        {
            return StartupRegistrationService.CreateForCurrentInstallation().TryRemoveAllRegistrations();
        }
        catch (Exception exception) when (IsExpectedIntegrationFailure(exception))
        {
            return false;
        }
    }

    private static bool IsExpectedIntegrationFailure(Exception exception) =>
        exception is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException;
}

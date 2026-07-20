using System.IO;
using System.Security;
using Velopack.Locators;

namespace BatteryMonitor.Infrastructure.Startup;

internal sealed class StartupRegistrationService(
    IStartupRegistrationStore store,
    ILegacyStartupRegistration legacyRegistration,
    string? launcherPath)
{
    internal static StartupRegistrationService CreateForCurrentInstallation()
    {
        IVelopackLocator locator = VelopackLocator.Current;
        string? currentLauncherPath = null;
        if (!locator.IsPortable &&
            locator.CurrentlyInstalledVersion is not null &&
            !string.IsNullOrWhiteSpace(locator.RootAppDir) &&
            !string.IsNullOrWhiteSpace(locator.ThisExeRelativePath))
        {
            currentLauncherPath = Path.GetFullPath(Path.Combine(locator.RootAppDir, locator.ThisExeRelativePath));
        }

        return new StartupRegistrationService(
            new WindowsStartupRegistrationStore(),
            new LegacyScheduledTaskRegistration(),
            currentLauncherPath);
    }

    internal bool TryEnsureRegisteredAndMigrate()
    {
        string? currentLauncherPath = launcherPath;
        if (string.IsNullOrWhiteSpace(currentLauncherPath) || !File.Exists(currentLauncherPath))
        {
            legacyRegistration.TryRemoveOwnedRegistration();
            return false;
        }

        try
        {
            string expectedCommand = CreateCommand(currentLauncherPath);
            if (!string.Equals(store.ReadCommand(), expectedCommand, StringComparison.Ordinal))
            {
                store.WriteCommand(expectedCommand);
            }

            if (legacyRegistration.TryRemoveOwnedRegistration())
            {
                return true;
            }

            // 新旧両方から起動される状態を残さない。旧登録が消せない場合はRun登録を戻す。
            store.DeleteCommand();
            return false;
        }
        catch (Exception exception) when (IsRegistrationFailure(exception))
        {
            return false;
        }
    }

    internal bool TryRemoveAllRegistrations()
    {
        bool runRemoved;
        try
        {
            store.DeleteCommand();
            runRemoved = true;
        }
        catch (Exception exception) when (IsRegistrationFailure(exception))
        {
            runRemoved = false;
        }

        return legacyRegistration.TryRemoveOwnedRegistration() && runRemoved;
    }

    internal static string CreateCommand(string path) =>
        $"\"{path}\" {ApplicationLaunchModeCalculator.StartupArgument}";

    private static bool IsRegistrationFailure(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException or IOException;
}

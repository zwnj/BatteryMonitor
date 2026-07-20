namespace BatteryMonitor.Infrastructure.Startup;

internal static class ApplicationLaunchModeCalculator
{
    internal const string StartupArgument = "--startup";

    internal static bool IsStartupLaunch(IEnumerable<string> arguments) =>
        arguments.Any(argument =>
            string.Equals(argument, StartupArgument, StringComparison.OrdinalIgnoreCase));
}

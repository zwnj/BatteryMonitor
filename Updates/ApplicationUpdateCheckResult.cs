namespace BatteryMonitor.Updates;

internal sealed record ApplicationUpdateCheckResult(
    bool IsInstalled,
    bool IsUpdateAvailable,
    string? AvailableVersion);

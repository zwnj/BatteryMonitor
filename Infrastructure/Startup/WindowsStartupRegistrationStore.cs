using Microsoft.Win32;

namespace BatteryMonitor.Infrastructure.Startup;

internal sealed class WindowsStartupRegistrationStore : IStartupRegistrationStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BatteryMonitor";

    public string? ReadCommand()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    public void WriteCommand(string command)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, command, RegistryValueKind.String);
    }

    public void DeleteCommand()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

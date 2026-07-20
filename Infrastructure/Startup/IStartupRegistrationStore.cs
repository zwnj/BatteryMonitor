namespace BatteryMonitor.Infrastructure.Startup;

internal interface IStartupRegistrationStore
{
    string? ReadCommand();
    void WriteCommand(string command);
    void DeleteCommand();
}

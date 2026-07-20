using System.Diagnostics;
using System.Xml.Linq;

namespace BatteryMonitor.Infrastructure.Startup;

internal sealed class LegacyScheduledTaskRegistration : ILegacyStartupRegistration
{
    private const string TaskName = "BatteryMonitorAutoStart";
    private const string OwnedDescription = "BatteryMonitor Auto Start";
    private const string OwnedExecutableName = "BatteryMonitor3.exe";
    private const int TimeoutMilliseconds = 10_000;

    public bool TryRemoveOwnedRegistration()
    {
        try
        {
            ProcessResult query = RunSchTasks($"/query /tn \"{TaskName}\" /xml");
            if (!query.Succeeded)
            {
                return true;
            }

            if (!IsOwnedTaskDefinition(query.StandardOutput))
            {
                return false;
            }

            return RunSchTasks($"/delete /tn \"{TaskName}\" /f").Succeeded;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.IO.IOException or
                System.ComponentModel.Win32Exception or AggregateException)
        {
            return false;
        }
    }

    internal static bool IsOwnedTaskDefinition(string xml)
    {
        try
        {
            XDocument document = XDocument.Parse(xml);
            string? description = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Description")?.Value;
            string? command = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Command")?.Value;

            return string.Equals(description, OwnedDescription, StringComparison.Ordinal) &&
                string.Equals(System.IO.Path.GetFileName(command), OwnedExecutableName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or ArgumentException)
        {
            return false;
        }
    }

    private static ProcessResult RunSchTasks(string arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "schtasks",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using Process? process = Process.Start(startInfo);
        if (process is null)
        {
            return new ProcessResult(false, string.Empty);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            return new ProcessResult(false, string.Empty);
        }

        Task.WaitAll(outputTask, errorTask);
        return new ProcessResult(process.ExitCode == 0, outputTask.Result);
    }

    private readonly record struct ProcessResult(bool Succeeded, string StandardOutput);
}

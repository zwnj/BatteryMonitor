namespace BatteryMonitor.Updates;

internal interface IApplicationUpdateService
{
    string CurrentVersionText { get; }

    Task<ApplicationUpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);

    Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken = default);

    void ApplyAndRestart();
}

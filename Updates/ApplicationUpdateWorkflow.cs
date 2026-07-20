namespace BatteryMonitor.Updates;

internal sealed class ApplicationUpdateWorkflow(
    IApplicationUpdateService updateService,
    Func<Task> prepareForRestartAsync)
{
    public Task<ApplicationUpdateCheckResult> CheckAsync(
        CancellationToken cancellationToken = default) =>
        updateService.CheckAsync(cancellationToken);

    public async Task DownloadPrepareAndRestartAsync(
        IProgress<int> progress,
        CancellationToken cancellationToken = default)
    {
        await updateService.DownloadAsync(progress, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // ここから先は終了準備済みになるため、通常のキャンセルではアプリへ戻さない。
        await prepareForRestartAsync();
        updateService.ApplyAndRestart();
    }
}

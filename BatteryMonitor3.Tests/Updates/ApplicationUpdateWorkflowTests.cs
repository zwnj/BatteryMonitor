using BatteryMonitor.Updates;

using NUnit.Framework;

namespace BatteryMonitor3.Tests.Updates;

public sealed class ApplicationUpdateWorkflowTests
{
    [Test]
    public async Task DownloadPrepareAndRestartRunsInSafetyOrder()
    {
        List<string> operations = [];
        FakeApplicationUpdateService service = new(operations);
        ApplicationUpdateWorkflow workflow = new(
            service,
            () =>
            {
                operations.Add("prepare");
                return Task.CompletedTask;
            });

        await workflow.DownloadPrepareAndRestartAsync(new Progress<int>());

        Assert.That(operations, Is.EqualTo(new[] { "download", "prepare", "apply" }));
    }

    [Test]
    public void DownloadFailureSkipsPreparationAndApply()
    {
        List<string> operations = [];
        FakeApplicationUpdateService service = new(operations)
        {
            DownloadException = new InvalidOperationException("download failed"),
        };
        ApplicationUpdateWorkflow workflow = new(
            service,
            () =>
            {
                operations.Add("prepare");
                return Task.CompletedTask;
            });

        Func<Task> action = () => workflow.DownloadPrepareAndRestartAsync(new Progress<int>());

        Assert.ThrowsAsync<InvalidOperationException>(action);

        Assert.That(operations, Is.EqualTo(new[] { "download" }));
    }

    [Test]
    public void PreparationFailureSkipsApply()
    {
        List<string> operations = [];
        FakeApplicationUpdateService service = new(operations);
        ApplicationUpdateWorkflow workflow = new(
            service,
            () =>
            {
                operations.Add("prepare");
                throw new InvalidOperationException("prepare failed");
            });

        Func<Task> action = () => workflow.DownloadPrepareAndRestartAsync(new Progress<int>());

        Assert.ThrowsAsync<InvalidOperationException>(action);

        Assert.That(operations, Is.EqualTo(new[] { "download", "prepare" }));
    }

    private sealed class FakeApplicationUpdateService(List<string> operations) :
        IApplicationUpdateService
    {
        public Exception? DownloadException { get; init; }

        public string CurrentVersionText => "v1.0.0";

        public Task<ApplicationUpdateCheckResult> CheckAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationUpdateCheckResult(true, true, "1.0.1"));

        public Task DownloadAsync(
            IProgress<int> progress,
            CancellationToken cancellationToken = default)
        {
            operations.Add("download");
            return DownloadException is null
                ? Task.CompletedTask
                : Task.FromException(DownloadException);
        }

        public void ApplyAndRestart() => operations.Add("apply");
    }
}

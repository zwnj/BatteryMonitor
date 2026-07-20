using BatteryMonitor.Updates;

using NUnit.Framework;

namespace BatteryMonitor3.Tests.Updates;

public sealed class UpdateOperationStateTests
{
    [Test]
    public void PreventsConcurrentOperations()
    {
        UpdateOperationState state = new();

        Assert.That(state.TryBegin(out _), Is.True);
        Assert.That(state.TryBegin(out _), Is.False);
    }

    [Test]
    public void LateProgressCannotOverwriteCompletedState()
    {
        UpdateOperationState state = new();
        Assert.That(state.TryBegin(out long operationId), Is.True);
        Assert.That(state.TryReportProgress(operationId, 100), Is.True);

        state.Complete(operationId);

        Assert.That(state.TryReportProgress(operationId, 80), Is.False);
        Assert.That(state.ProgressPercentage, Is.EqualTo(100));
        Assert.That(state.IsActive, Is.False);
    }
}

namespace BatteryMonitor.Updates;

internal sealed class UpdateOperationState
{
    private readonly object syncRoot = new();
    private long currentOperationId;
    private bool isActive;
    private int progressPercentage;

    internal bool IsActive
    {
        get
        {
            lock (syncRoot)
            {
                return isActive;
            }
        }
    }

    internal int ProgressPercentage
    {
        get
        {
            lock (syncRoot)
            {
                return progressPercentage;
            }
        }
    }

    internal bool TryBegin(out long operationId)
    {
        lock (syncRoot)
        {
            if (isActive)
            {
                operationId = 0;
                return false;
            }

            currentOperationId++;
            operationId = currentOperationId;
            progressPercentage = 0;
            isActive = true;
            return true;
        }
    }

    internal bool TryReportProgress(long operationId, int percentage)
    {
        lock (syncRoot)
        {
            if (!isActive || operationId != currentOperationId)
            {
                return false;
            }

            progressPercentage = Math.Clamp(percentage, 0, 100);
            return true;
        }
    }

    internal void Complete(long operationId)
    {
        lock (syncRoot)
        {
            if (operationId == currentOperationId)
            {
                isActive = false;
            }
        }
    }
}

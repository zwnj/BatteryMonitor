using System.Threading;
using System.Windows.Threading;

namespace BatteryMonitor.Infrastructure.Startup;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\BatteryMonitor.SingleInstance";
    private const string ActivationEventName = @"Local\BatteryMonitor.Activate";
    private readonly Mutex mutex;
    private readonly EventWaitHandle activationEvent;
    private readonly RegisteredWaitHandle? registeredWait;
    private bool disposed;

    internal SingleInstanceCoordinator(Dispatcher dispatcher, Action activatePrimary)
    {
        mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        IsPrimaryInstance = createdNew;
        activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);

        if (IsPrimaryInstance)
        {
            registeredWait = ThreadPool.RegisterWaitForSingleObject(
                activationEvent,
                (_, _) => dispatcher.BeginInvoke(activatePrimary),
                null,
                Timeout.Infinite,
                executeOnlyOnce: false);
        }
    }

    internal bool IsPrimaryInstance { get; }

    internal void NotifyPrimaryInstance()
    {
        if (!IsPrimaryInstance)
        {
            activationEvent.Set();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        registeredWait?.Unregister(null);
        activationEvent.Dispose();
        if (IsPrimaryInstance)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
    }
}

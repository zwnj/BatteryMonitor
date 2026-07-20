using System.IO;
using BatteryMonitor.Infrastructure.Startup;
using NUnit.Framework;

namespace BatteryMonitor3.Tests.Infrastructure.Startup;

public sealed class StartupRegistrationServiceTests
{
    [Test]
    public void RegistersInstalledLauncherAndRemovesLegacyTask()
    {
        FakeStartupRegistrationStore store = new();
        FakeLegacyStartupRegistration legacy = new();
        string launcherPath = Environment.ProcessPath!;
        StartupRegistrationService service = new(store, legacy, launcherPath);

        Assert.That(service.TryEnsureRegisteredAndMigrate(), Is.True);
        Assert.That(store.Command, Is.EqualTo($"\"{launcherPath}\" --startup"));
        Assert.That(store.WriteCount, Is.EqualTo(1));
        Assert.That(legacy.RemoveCount, Is.EqualTo(1));
    }

    [Test]
    public void IdenticalCommandIsNotRewritten()
    {
        string launcherPath = Environment.ProcessPath!;
        FakeStartupRegistrationStore store = new()
        {
            Command = StartupRegistrationService.CreateCommand(launcherPath),
        };
        StartupRegistrationService service = new(store, new FakeLegacyStartupRegistration(), launcherPath);

        Assert.That(service.TryEnsureRegisteredAndMigrate(), Is.True);
        Assert.That(store.WriteCount, Is.Zero);
    }

    [Test]
    public void PortableOrDevelopmentLaunchDoesNotCreateRunValue()
    {
        FakeStartupRegistrationStore store = new();
        FakeLegacyStartupRegistration legacy = new();
        StartupRegistrationService service = new(store, legacy, launcherPath: null);

        Assert.That(service.TryEnsureRegisteredAndMigrate(), Is.False);
        Assert.That(store.WriteCount, Is.Zero);
        Assert.That(legacy.RemoveCount, Is.EqualTo(1));
    }

    [Test]
    public void RegistryFailureDoesNotEscapeOrDeleteLegacyFallback()
    {
        FakeStartupRegistrationStore store = new() { ReadException = new IOException("denied") };
        FakeLegacyStartupRegistration legacy = new();
        StartupRegistrationService service = new(store, legacy, Environment.ProcessPath);

        Assert.That(service.TryEnsureRegisteredAndMigrate(), Is.False);
        Assert.That(legacy.RemoveCount, Is.Zero);
    }

    [Test]
    public void LegacyRemovalFailureRollsBackRunValueToAvoidDuplicateStartup()
    {
        FakeStartupRegistrationStore store = new();
        FakeLegacyStartupRegistration legacy = new() { RemoveResult = false };
        StartupRegistrationService service = new(store, legacy, Environment.ProcessPath);

        Assert.That(service.TryEnsureRegisteredAndMigrate(), Is.False);
        Assert.That(store.Command, Is.Null);
        Assert.That(store.WriteCount, Is.EqualTo(1));
        Assert.That(store.DeleteCount, Is.EqualTo(1));
    }

    [Test]
    public void UninstallRemovesRunAndLegacyRegistrationsIdempotently()
    {
        FakeStartupRegistrationStore store = new() { Command = "existing" };
        FakeLegacyStartupRegistration legacy = new();
        StartupRegistrationService service = new(store, legacy, Environment.ProcessPath);

        Assert.That(service.TryRemoveAllRegistrations(), Is.True);
        Assert.That(service.TryRemoveAllRegistrations(), Is.True);
        Assert.That(store.Command, Is.Null);
        Assert.That(store.DeleteCount, Is.EqualTo(2));
        Assert.That(legacy.RemoveCount, Is.EqualTo(2));
    }

    private sealed class FakeStartupRegistrationStore : IStartupRegistrationStore
    {
        public string? Command { get; set; }
        public Exception? ReadException { get; init; }
        public int WriteCount { get; private set; }
        public int DeleteCount { get; private set; }

        public string? ReadCommand()
        {
            if (ReadException is not null)
            {
                throw ReadException;
            }

            return Command;
        }

        public void WriteCommand(string command)
        {
            Command = command;
            WriteCount++;
        }

        public void DeleteCommand()
        {
            Command = null;
            DeleteCount++;
        }
    }

    private sealed class FakeLegacyStartupRegistration : ILegacyStartupRegistration
    {
        public bool RemoveResult { get; init; } = true;
        public int RemoveCount { get; private set; }

        public bool TryRemoveOwnedRegistration()
        {
            RemoveCount++;
            return RemoveResult;
        }
    }
}

using BatteryMonitor.Infrastructure.Startup;
using NUnit.Framework;

namespace BatteryMonitor3.Tests.Infrastructure.Startup;

public sealed class LegacyScheduledTaskRegistrationTests
{
    private const string OwnedTask = """
        <?xml version="1.0" encoding="utf-8"?>
        <Task xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo><Description>BatteryMonitor Auto Start</Description></RegistrationInfo>
          <Actions><Exec><Command>C:\Apps\BatteryMonitor3.exe</Command></Exec></Actions>
        </Task>
        """;

    [Test]
    public void RecognizesOnlyOwnedLegacyDefinition()
    {
        Assert.That(LegacyScheduledTaskRegistration.IsOwnedTaskDefinition(OwnedTask), Is.True);
        Assert.That(
            LegacyScheduledTaskRegistration.IsOwnedTaskDefinition(
                OwnedTask.Replace("BatteryMonitor Auto Start", "User Task")),
            Is.False);
        Assert.That(
            LegacyScheduledTaskRegistration.IsOwnedTaskDefinition(
                OwnedTask.Replace("BatteryMonitor3.exe", "Other.exe")),
            Is.False);
    }

    [Test]
    public void InvalidXmlIsNotTreatedAsOwned()
    {
        Assert.That(LegacyScheduledTaskRegistration.IsOwnedTaskDefinition("not xml"), Is.False);
    }
}

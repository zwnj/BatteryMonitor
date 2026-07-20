using Velopack;

using BatteryMonitor.Infrastructure.Startup;

namespace BatteryMonitor;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => StartupIntegration.TryEnsureRegisteredAndMigrate())
            .OnAfterUpdateFastCallback(_ => StartupIntegration.TryEnsureRegisteredAndMigrate())
            .OnBeforeUninstallFastCallback(_ => StartupIntegration.TryRemoveAllRegistrations())
            .Run();

        App application = new();
        application.InitializeComponent();
        application.Run();
    }
}

using Velopack;

namespace BatteryMonitor;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();

        App application = new();
        application.InitializeComponent();
        application.Run();
    }
}

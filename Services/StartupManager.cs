using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace BatteryMonitor3.Services
{
    public static class StartupManager
    {
        private const string TaskName = "BatteryMonitor3AutoStart";

        public static bool IsStartupEnabled()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/query /tn \"{TaskName}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static void SetStartup(bool enable)
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            // 念のため .exe 拡張子を確認
            if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                exePath = Path.ChangeExtension(exePath, ".exe");
            }

            if (enable)
            {
                try
                {
                    // バッテリー駆動時でも実行できるようにタスク定義のXMLを作成
                    string xmlContent = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>BatteryMonitor3 Auto Start</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>true</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>""{exePath}""</Command>
    </Exec>
  </Actions>
</Task>";

                    string tempXml = Path.GetTempFileName();
                    File.WriteAllText(tempXml, xmlContent);

                    // XMLを使用してタスクを登録
                    // /f : 強制上書き
                    var args = $"/create /tn \"{TaskName}\" /xml \"{tempXml}\" /f";
                    RunSchTasks(args);
                    
                    try { File.Delete(tempXml); } catch { }
                }
                catch (Exception ex)
                {
                     System.Diagnostics.Debug.WriteLine($"タスク登録エラー: {ex.Message}");
                }
            }
            else
            {
                // タスクを削除
                var args = $"/delete /tn \"{TaskName}\" /f";
                RunSchTasks(args);
            }
        }

        private static void RunSchTasks(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var process = Process.Start(psi);
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SchTasks エラー: {ex.Message}");
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;

using BatteryMonitor.Helpers;

namespace BatteryMonitor.Services
{
    public static class StartupManager
    {
        private const string TaskName = "BatteryMonitorAutoStart";
        private const int SchTasksTimeoutMilliseconds = 10_000;

        public static bool IsStartupEnabled()
        {
            var result = RunSchTasks($"/query /tn \"{TaskName}\"");
            return result.Succeeded;
        }

        public static bool TrySetStartup(bool enable)
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                Logger.Error("タスク登録エラー: 実行ファイルのパスを取得できませんでした。");
                return false;
            }

            // 念のため .exe 拡張子を確認
            if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                exePath = Path.ChangeExtension(exePath, ".exe");
            }

            if (!enable)
            {
                return LogFailure(
                    RunSchTasks($"/delete /tn \"{TaskName}\" /f"),
                    "タスク削除エラー");
            }

            string? tempXml = null;
            try
            {
                // バッテリー駆動時でも実行できるようにタスク定義のXMLを作成
                var escapedExePath = SecurityElement.Escape(exePath);
                var xmlContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>BatteryMonitor Auto Start</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
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
      <Command>{escapedExePath}</Command>
    </Exec>
  </Actions>
</Task>";

                tempXml = Path.GetTempFileName();
                File.WriteAllText(tempXml, xmlContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                // XMLを使用してタスクを登録（/f: 強制上書き）
                return LogFailure(
                    RunSchTasks($"/create /tn \"{TaskName}\" /xml \"{tempXml}\" /f"),
                    "タスク登録エラー");
            }
            catch (Exception ex)
            {
                Logger.Error("タスク登録エラー", ex);
                return false;
            }
            finally
            {
                if (tempXml != null)
                {
                    try
                    {
                        File.Delete(tempXml);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("一時XML削除エラー", ex);
                    }
                }
            }
        }

        private static bool LogFailure(SchTasksResult result, string operation)
        {
            if (!result.Succeeded)
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput
                    : result.StandardError;
                Logger.Error($"{operation}: {detail.Trim()}");
            }

            return result.Succeeded;
        }

        private static SchTasksResult RunSchTasks(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return new SchTasksResult(false, string.Empty, "schtasksを起動できませんでした。");
                }

                var standardOutputTask = process.StandardOutput.ReadToEndAsync();
                var standardErrorTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(SchTasksTimeoutMilliseconds))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        _ = process.WaitForExit(2_000);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SchTasks 終了処理エラー: {ex.Message}");
                    }

                    return new SchTasksResult(
                        false,
                        GetCompletedOutput(standardOutputTask),
                        $"schtasksが{SchTasksTimeoutMilliseconds / 1000}秒以内に完了しませんでした。");
                }

                var standardOutput = standardOutputTask.GetAwaiter().GetResult();
                var standardError = standardErrorTask.GetAwaiter().GetResult();
                return new SchTasksResult(process.ExitCode == 0, standardOutput, standardError);
            }
            catch (Exception ex)
            {
                return new SchTasksResult(false, string.Empty, ex.Message);
            }
        }

        private static string GetCompletedOutput(System.Threading.Tasks.Task<string> outputTask)
        {
            return outputTask.IsCompletedSuccessfully ? outputTask.Result : string.Empty;
        }

        private readonly record struct SchTasksResult(
            bool Succeeded,
            string StandardOutput,
            string StandardError);
    }
}

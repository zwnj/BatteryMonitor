using System;
using System.IO;
using System.Text;
using System.Windows;

namespace BatteryMonitor3.Helpers
{
    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
        private static readonly object LockObj = new object();

        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public static void Error(string message, Exception? ex = null)
        {
            var sb = new StringBuilder();
            sb.Append(message);
            if (ex != null)
            {
                sb.AppendLine();
                sb.Append(ex.ToString());
            }
            WriteLog("ERROR", sb.ToString());
        }

        private static void WriteLog(string level, string message)
        {
            try
            {
                lock (LockObj)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logLine = $"[{timestamp}] [{level}] {message}";
                    
                    // Append to file
                    File.AppendAllText(LogFilePath, logLine + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // Failed to log - possibly disk full or permissions.
                // Avoid crashing the app just because logging failed.
            }
        }
    }
}

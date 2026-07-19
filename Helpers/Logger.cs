using System;
using System.IO;
using System.Text;
using System.Threading;

namespace BatteryMonitor.Helpers
{
    public static class Logger
    {
        private const long MaxLogFileSize = 2 * 1024 * 1024;
        private static readonly string LogDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BatteryMonitor",
            "Logs");
        private static readonly string LogFilePath = Path.Combine(LogDirectoryPath, "debug.log");
        private static readonly string PreviousLogFilePath = Path.Combine(LogDirectoryPath, "debug.previous.log");
        private static readonly object LockObj = new();
        private static readonly Timer FlushTimer = new(_ => Flush(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        private static StreamWriter? _writer;
        private static long _currentSize;

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
            WriteLog("ERROR", sb.ToString(), flushImmediately: true);
        }

        private static void WriteLog(string level, string message, bool flushImmediately = false)
        {
            try
            {
                lock (LockObj)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logLine = $"[{timestamp}] [{level}] {message}{Environment.NewLine}";
                    int byteCount = Encoding.UTF8.GetByteCount(logLine);
                    EnsureWriter(byteCount);
                    _writer!.Write(logLine);
                    _currentSize += byteCount;

                    if (flushImmediately)
                    {
                        _writer.Flush();
                    }
                }
            }
            catch (Exception)
            {
                // ログ出力失敗 - おそらくディスク容量不足や権限エラー
                // ログ失敗だけでアプリをクラッシュさせない
            }
        }

        private static void EnsureWriter(int incomingByteCount)
        {
            if (_writer == null)
            {
                Directory.CreateDirectory(LogDirectoryPath);
                _currentSize = File.Exists(LogFilePath) ? new FileInfo(LogFilePath).Length : 0;
            }

            if (_currentSize > 0 && _currentSize + incomingByteCount > MaxLogFileSize)
            {
                _writer?.Dispose();
                _writer = null;
                File.Move(LogFilePath, PreviousLogFilePath, overwrite: true);
                _currentSize = 0;
            }

            _writer ??= new StreamWriter(
                new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static void Flush()
        {
            try
            {
                lock (LockObj)
                {
                    _writer?.Flush();
                }
            }
            catch (Exception)
            {
                // ログ出力失敗だけでアプリをクラッシュさせない
            }
        }

        public static void Shutdown()
        {
            lock (LockObj)
            {
                FlushTimer.Dispose();
                _writer?.Dispose();
                _writer = null;
            }
        }
    }
}

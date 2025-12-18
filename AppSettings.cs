using System;
using System.IO;
using System.Text.Json;

namespace BatteryMonitor3
{
    public class AppSettings
    {
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BatteryMonitor3",
            "settings.json");

        public static void Save(double left, double top)
        {
            try
            {
                var settings = new AppSettings { WindowLeft = left, WindowTop = top };
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir!);
                }
                
                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // エラーは無視（本番環境ではログ出力を推奨）
            }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // 読み込みエラー時はデフォルトを返す
            }
            return new AppSettings();
        }
    }
}

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BatteryMonitor.Models
{
    public class AppSettings
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public int ChargeLimit { get; set; } = 100;

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BatteryMonitor",
            "settings.json");

        public static void Save(double left, double top, int chargeLimit)
        {
            try
            {
                var settings = new AppSettings { WindowLeft = left, WindowTop = top, ChargeLimit = chargeLimit };
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir!);
                }
                
                var json = JsonSerializer.Serialize(settings, JsonOptions);
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
                    return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
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

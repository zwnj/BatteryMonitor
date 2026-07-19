using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using BatteryMonitor.Helpers;

namespace BatteryMonitor.Models
{
    public class AppSettings
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            WriteIndented = true
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
            string? tempPath = null;
            try
            {
                var settings = new AppSettings { WindowLeft = left, WindowTop = top, ChargeLimit = chargeLimit };
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir!);
                }
                
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                tempPath = SettingsPath + ".tmp";
                File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                if (File.Exists(SettingsPath))
                {
                    File.Replace(tempPath, SettingsPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, SettingsPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save application settings", ex);
            }
            finally
            {
                if (tempPath != null && File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to remove temporary settings file", ex);
                    }
                }
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
            catch (JsonException ex)
            {
                Logger.Error("Application settings are corrupted; defaults will be used", ex);
                BackupCorruptedSettings();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load application settings; defaults will be used", ex);
            }
            return new AppSettings();
        }

        private static void BackupCorruptedSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return;
                }

                string backupPath = $"{SettingsPath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
                File.Move(SettingsPath, backupPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to preserve corrupted settings file", ex);
            }
        }
    }
}

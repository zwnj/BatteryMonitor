using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BatteryMonitor3.Helpers
{
    public class SvgIconGenerator
    {
        private readonly string _iconDirectory;
        private readonly Dictionary<(int BucketPercentage, int ColorBand, bool IsCharging), ImageSource> _iconCacheByBand = new();

        public SvgIconGenerator(string iconDirectory)
        {
            _iconDirectory = iconDirectory;
            if (!Directory.Exists(_iconDirectory))
            {
                Logger.Error($"トレイアイコンフォルダが見つかりません: {_iconDirectory}");
            }
        }

        public ImageSource GenerateIcon(int batteryPercentage, bool? isCharging = null)
        {
            int normalizedPercentage = NormalizePercentage(batteryPercentage);
            int colorBand = GetColorBand(batteryPercentage);
            bool normalizedCharging = isCharging == true;
            var cacheKey = (normalizedPercentage, colorBand, normalizedCharging);

            if (_iconCacheByBand.TryGetValue(cacheKey, out var cachedIcon))
            {
                return cachedIcon;
            }

            string fileName = normalizedCharging
                ? $"battery_{normalizedPercentage}_charging.ico"
                : $"battery_{normalizedPercentage}_{GetColorBandName(colorBand)}.ico";
            string iconPath = Path.Combine(_iconDirectory, fileName);

            if (!File.Exists(iconPath))
            {
                Logger.Error($"トレイアイコンファイルが見つかりません: {iconPath}");
                return null;
            }

            var icon = LoadBitmap(iconPath);
            _iconCacheByBand[cacheKey] = icon;
            return icon;
        }

        private static int NormalizePercentage(int batteryPercentage)
        {
            int clamped = Math.Max(0, Math.Min(100, batteryPercentage));
            if (clamped == 100) return 100;

            return (clamped / 10) * 10;
        }

        private static ImageSource LoadBitmap(string iconPath)
        {
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.UriSource = new Uri(iconPath, UriKind.Absolute);
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        private static int GetColorBand(int batteryPercentage)
        {
            int clamped = Math.Max(0, Math.Min(100, batteryPercentage));
            if (clamped <= 20) return 0;
            if (clamped <= 50) return 1;
            return 2;
        }

        private static string GetColorBandName(int colorBand)
        {
            return colorBand switch
            {
                0 => "red",
                1 => "orange",
                _ => "teal",
            };
        }
    }
}

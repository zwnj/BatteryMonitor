using System;
using System.Linq;
using System.Windows;

namespace BatteryMonitor3.Services
{
    public enum ThemeType
    {
        Dark,
        Light
    }

    public static class ThemeManager
    {
        public static ThemeType CurrentTheme { get; private set; } = ThemeType.Dark;

        public static event EventHandler? ThemeChanged;

        public static void SetTheme(ThemeType theme)
        {
            try
            {
                var appResources = Application.Current.Resources;
                var uri = GetThemeUri(theme);
                
                // 新しいテーマ辞書を読み込み
                var newDict = new ResourceDictionary { Source = uri };

                // 削除ロジック: 既存のテーマ辞書を探して削除する
                // ソースURIが DarkTheme.xaml または LightTheme.xaml で終わるものを識別
                var oldDicts = appResources.MergedDictionaries
                    .Where(d => d.Source != null && 
                                (d.Source.OriginalString.EndsWith("DarkTheme.xaml") || 
                                 d.Source.OriginalString.EndsWith("LightTheme.xaml")))
                    .ToList();

                foreach (var oldDict in oldDicts)
                {
                    appResources.MergedDictionaries.Remove(oldDict);
                }

                // 新しいテーマを追加
                appResources.MergedDictionaries.Add(newDict);
                CurrentTheme = theme;
                
                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"テーマ切り替えエラー: {ex.Message}");
            }
        }

        public static void ToggleTheme()
        {
            SetTheme(CurrentTheme == ThemeType.Dark ? ThemeType.Light : ThemeType.Dark);
        }

        public static Uri GetThemeUri(ThemeType theme)
        {
            return new Uri(theme == ThemeType.Dark ? "Views/Themes/DarkTheme.xaml" : "Views/Themes/LightTheme.xaml", UriKind.Relative);
        }
    }
}

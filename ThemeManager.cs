using System;
using System.Linq;
using System.Windows;

namespace BatteryMonitor3
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
                
                // Load the new theme dictionary
                var newDict = new ResourceDictionary
                {
                    Source = uri
                };

                // Remove logic: Find any existing theme dictionaries and remove them
                // We identify them by source URI ending in DarkTheme.xaml or LightTheme.xaml
                var oldDicts = appResources.MergedDictionaries
                    .Where(d => d.Source != null && 
                                (d.Source.OriginalString.EndsWith("DarkTheme.xaml") || 
                                 d.Source.OriginalString.EndsWith("LightTheme.xaml")))
                    .ToList();

                foreach (var oldDict in oldDicts)
                {
                    appResources.MergedDictionaries.Remove(oldDict);
                }

                // Add the new theme
                appResources.MergedDictionaries.Add(newDict);
                CurrentTheme = theme;
                
                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error switching theme: {ex.Message}");
            }
        }

        public static void ToggleTheme()
        {
            SetTheme(CurrentTheme == ThemeType.Dark ? ThemeType.Light : ThemeType.Dark);
        }

        public static Uri GetThemeUri(ThemeType theme)
        {
            return new Uri(theme == ThemeType.Dark ? "DarkTheme.xaml" : "LightTheme.xaml", UriKind.Relative);
        }
    }
}

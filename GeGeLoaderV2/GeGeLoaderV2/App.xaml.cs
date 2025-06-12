using System;
using System.Windows;
using System.Windows.Media; // Required for Color, ColorConverter, SolidColorBrush
// No direct using for GeGeLoaderV2.Properties is needed if accessed via fully qualified name.

namespace GeGeLoaderV2
{
    public partial class App : Application
    {
        public void ApplyTheme(string themeName, string primaryColorHex, string accentColorHex)
        {
            var mergedDictionaries = Resources.MergedDictionaries;

            // Find and remove existing theme dictionaries to prevent duplication or incorrect layering
            for (int i = mergedDictionaries.Count - 1; i >= 0; i--)
            {
                var dict = mergedDictionaries[i];
                if (dict.Source != null && (dict.Source.OriginalString.EndsWith("LightTheme.xaml") || dict.Source.OriginalString.EndsWith("DarkTheme.xaml")))
                {
                    mergedDictionaries.RemoveAt(i);
                }
            }

            string themeUriPath = themeName == "Light" ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";
            var themeDictionary = new ResourceDictionary { Source = new Uri(themeUriPath, UriKind.Relative) };

            mergedDictionaries.Add(themeDictionary); // Add the chosen theme.

            // Update dynamic brushes for primary and accent colors defined in App.xaml's direct resources
            try
            {
                if (Resources["PrimaryHueBrush"] is SolidColorBrush primaryBrush)
                    primaryBrush.Color = (Color)ColorConverter.ConvertFromString(primaryColorHex);

                if (Resources["AccentHueBrush"] is SolidColorBrush accentBrush)
                    accentBrush.Color = (Color)ColorConverter.ConvertFromString(accentColorHex);
            }
            catch (FormatException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Color conversion error: {ex.Message}");
                if (Resources["PrimaryHueBrush"] is SolidColorBrush primaryBrush)
                    primaryBrush.Color = (Color)ColorConverter.ConvertFromString("#FF4081FF"); // Default Pink
                if (Resources["AccentHueBrush"] is SolidColorBrush accentBrush)
                    accentBrush.Color = (Color)ColorConverter.ConvertFromString("#FFFFFFFF"); // Default White
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            string currentTheme = "Dark"; // Default if setting is somehow null/empty
            string primaryColor = "#FF4081FF"; // Default
            string accentColor = "#FFFFFFFF"; // Default

            try
            {
                // Ensure settings have default values if they are new or empty
                if (string.IsNullOrEmpty(GeGeLoaderV2.Properties.Settings.Default.CurrentTheme))
                {
                    GeGeLoaderV2.Properties.Settings.Default.CurrentTheme = "Dark";
                }
                if (string.IsNullOrEmpty(GeGeLoaderV2.Properties.Settings.Default.PrimaryColor))
                {
                    GeGeLoaderV2.Properties.Settings.Default.PrimaryColor = "#FF4081FF";
                }
                // if (string.IsNullOrEmpty(GeGeLoaderV2.Properties.Settings.Default.AccentColor)) // When AccentColor is added
                // {
                //     GeGeLoaderV2.Properties.Settings.Default.AccentColor = "#FFFFFFFF";
                // }
                GeGeLoaderV2.Properties.Settings.Default.Save(); // Save if defaults were set

                currentTheme = GeGeLoaderV2.Properties.Settings.Default.CurrentTheme;
                primaryColor = GeGeLoaderV2.Properties.Settings.Default.PrimaryColor;
                // accentColor = GeGeLoaderV2.Properties.Settings.Default.AccentColor; // Uncomment when AccentColor is confirmed in settings
            }
            catch (System.Configuration.SettingsPropertyNotFoundException ex)
            {
                 System.Diagnostics.Debug.WriteLine($"Setting property not found, initializing: {ex.Message}");
                 // This might happen if the .settings file is corrupted or new settings haven't been saved yet.
                 GeGeLoaderV2.Properties.Settings.Default.CurrentTheme = currentTheme;
                 GeGeLoaderV2.Properties.Settings.Default.PrimaryColor = primaryColor;
                 // GeGeLoaderV2.Properties.Settings.Default.AccentColor = accentColor; // When implemented
                 GeGeLoaderV2.Properties.Settings.Default.Save();
            }
            catch (Exception ex) // Catch other potential exceptions during settings access
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings, using defaults: {ex.Message}");
                // Defaults are already set, so just proceed
            }

            ApplyTheme(currentTheme, primaryColor, accentColor);
        }
    }
}

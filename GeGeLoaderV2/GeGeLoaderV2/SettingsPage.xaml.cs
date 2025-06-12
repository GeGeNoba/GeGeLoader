using System;
using System.Windows;
using System.Windows.Controls;

namespace GeGeLoaderV2
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            btnDarkTheme.Click += BtnDarkTheme_Click;
            btnLightTheme.Click += BtnLightTheme_Click;
            btnColorPink.Click += BtnColorPink_Click;
            btnColorBlue.Click += BtnColorBlue_Click;
            btnColorGreen.Click += BtnColorGreen_Click;
        }

        private void ApplyAndSaveTheme(string themeToSet, string primaryColorToSet)
        {
            // Use current settings as baseline
            string currentThemeSetting = Properties.Settings.Default.CurrentTheme;
            string currentPrimaryColorSetting = Properties.Settings.Default.PrimaryColor;
            string currentAccentColorSetting = "#FFFFFFFF"; // Default, or load from Properties.Settings.Default.AccentColor if implemented

            if (themeToSet != null)
            {
                Properties.Settings.Default.CurrentTheme = themeToSet;
                currentThemeSetting = themeToSet;
            }
            if (primaryColorToSet != null)
            {
                Properties.Settings.Default.PrimaryColor = primaryColorToSet;
                currentPrimaryColorSetting = primaryColorToSet;
            }
            // if (accentColorToSet != null) { ... Properties.Settings.Default.AccentColor = accentColorToSet; ... }


            Properties.Settings.Default.Save();

            ((App)Application.Current).ApplyTheme(
                currentThemeSetting,
                currentPrimaryColorSetting,
                currentAccentColorSetting
            );
            UpdateStatus($"Theme updated to: {currentThemeSetting}, Primary: {currentPrimaryColorSetting}");
        }

        private void BtnDarkTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndSaveTheme("Dark", null);
        }

        private void BtnLightTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndSaveTheme("Light", null);
        }

        private void BtnColorPink_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndSaveTheme(null, (sender as Button)?.Tag?.ToString() ?? "#FF4081FF");
        }

        private void BtnColorBlue_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndSaveTheme(null, (sender as Button)?.Tag?.ToString() ?? "#FF007ACC");
        }

        private void BtnColorGreen_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndSaveTheme(null, (sender as Button)?.Tag?.ToString() ?? "#FF2ECC71");
        }

        private void UpdateStatus(string message)
        {
             MessageBox.Show(message, "Theme Updated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

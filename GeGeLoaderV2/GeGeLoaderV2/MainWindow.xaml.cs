using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls; // Required for Page, Frame

namespace GeGeLoaderV2
{
    public partial class MainWindow : Window
    {
        // DLL injection related imports, constants, fields, and methods have been moved to InjectorPage.xaml.cs

        public MainWindow()
        {
            InitializeComponent();

            // Make the window draggable
            this.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    this.DragMove();
            };

            // Removed chkAutoInject event handlers

            // Navigate to InjectorPage by default
            MainFrame.Navigate(new InjectorPage());
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            // Removed autoInjectCts cancellation logic
            this.Close();
        }

        // Event handlers for new navigation buttons
        private void btnNavMain_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new InjectorPage());
        }

        private void btnNavSettings_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SettingsPage());
        }

        // btnRefreshCsgoProcess_Click, btnRefreshSteamProcess_Click, btnBrowseDll_Click,
        // btnInject_Click, btnStartGame_Click, MonitorAndInjectAsync,
        // GetSteamPath, InjectDLL have all been moved to InjectorPage.xaml.cs
    }
}

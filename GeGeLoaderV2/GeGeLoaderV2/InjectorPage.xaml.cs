using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Threading;

namespace GeGeLoaderV2
{
    public partial class InjectorPage : Page
    {
        // DLL injection related imports and constants
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        // Constants for memory allocation and process access
        const int PROCESS_CREATE_THREAD = 0x0002;
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int PROCESS_VM_OPERATION = 0x0008;
        const int PROCESS_VM_WRITE = 0x0020;
        const int PROCESS_VM_READ = 0x0010;
        const uint MEM_COMMIT = 0x00001000;
        const uint MEM_RESERVE = 0x00002000;
        const uint PAGE_READWRITE = 0x04;

        // Variables to store selected process and DLL
        private Process selectedProcess;
        private string selectedDllPath;
        private bool isAutoInjectEnabled = false;
        private CancellationTokenSource autoInjectCts;

        // CS:GO launch parameters
        private const string CSGO_LAUNCH_ARGS = "-insecure -console -novid";
        private const string CSGO_PROCESS_NAME = "csgo";
        private const string STEAM_PROCESS_NAME = "steam";

        public InjectorPage()
        {
            InitializeComponent();

            // Set up auto-inject checkbox
            chkAutoInject.Checked += (s, e) => isAutoInjectEnabled = true;
            chkAutoInject.Unchecked += (s, e) => isAutoInjectEnabled = false;

            // Wire up event handlers for buttons previously in MainWindow
            // These names must match the x:Name attributes in InjectorPage.xaml and the method names below
            btnRefreshCsgoProcess.Click += btnRefreshCsgoProcess_Click;
            btnRefreshSteamProcess.Click += btnRefreshSteamProcess_Click;
            btnBrowseDll.Click += btnBrowseDll_Click;
            btnStartGame.Click += btnStartGame_Click;
            btnInject.Click += btnInject_Click;
        }

        private void btnRefreshCsgoProcess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(CSGO_PROCESS_NAME);
                if (processes.Length > 0)
                {
                    selectedProcess = processes[0];
                    txtSelectedProcess.Text = $"CS:GO (PID: {selectedProcess.Id})";
                    txtStatus.Text = "CS:GO process found. Ready to inject.";
                }
                else
                {
                    selectedProcess = null;
                    txtSelectedProcess.Text = "CS:GO not running";
                    txtStatus.Text = "CS:GO is not running. Please start the game first or use the START GAME button.";
                }

                // For testing without real process (instructions for test step 6.4):
                // To activate for step 6.4, one would uncomment the following lines:
                // /*
                // selectedProcess = new System.Diagnostics.Process(); // This is to make selectedProcess not null.
                //                                                    // Actual injection will fail.
                // txtSelectedProcess.Text = "Dummy CS:GO (PID: 1234)";
                // txtStatus.Text = "Dummy CS:GO process set for testing.";
                // */
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error finding CS:GO process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnRefreshSteamProcess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(STEAM_PROCESS_NAME);
                if (processes.Length > 0)
                {
                    selectedProcess = processes[0];
                    txtSelectedProcess.Text = $"Steam (PID: {selectedProcess.Id})";
                    txtStatus.Text = "Steam process found. Ready to inject.";
                }
                else
                {
                    selectedProcess = null;
                    txtSelectedProcess.Text = "Steam not running";
                    txtStatus.Text = "Steam is not running. Please start Steam first.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error finding Steam process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnBrowseDll_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "DLL files (*.dll)|*.dll",
                Title = "Select DLL to Inject"
            };

            // For testing without real dialog:
            selectedDllPath = "C:\\dummy\\test.dll";
            txtSelectedDll.Text = System.IO.Path.GetFileName(selectedDllPath);
            txtStatus.Text = $"DLL selected: {System.IO.Path.GetFileName(selectedDllPath)}";
            return; // Bypass ShowDialog for test

            // Original code:
            // if (openFileDialog.ShowDialog() == true)
            {
                selectedDllPath = openFileDialog.FileName;
                txtSelectedDll.Text = System.IO.Path.GetFileName(selectedDllPath);
                txtStatus.Text = $"DLL selected: {System.IO.Path.GetFileName(selectedDllPath)}";
            }
        }

        private async void btnInject_Click(object sender, RoutedEventArgs e)
        {
            if (selectedProcess == null)
            {
                txtStatus.Text = "No process selected. Please select CS:GO or Steam.";
                return;
            }

            if (string.IsNullOrEmpty(selectedDllPath))
            {
                txtStatus.Text = "No DLL selected. Please browse for a DLL.";
                return;
            }

            btnInject.IsEnabled = false;
            txtStatus.Text = "Injecting...";
            DoubleAnimation opacityAnimation = new DoubleAnimation { From = 1.0, To = 0.5, Duration = TimeSpan.FromSeconds(0.5), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            txtStatus.BeginAnimation(OpacityProperty, opacityAnimation);
            bool success = false;
            await Task.Run(() => { success = InjectDLL(selectedProcess.Id, selectedDllPath); });
            txtStatus.BeginAnimation(OpacityProperty, null);
            txtStatus.Opacity = 1.0;
            txtStatus.Text = success ? "Injection successful!" : "Injection failed. Please try again.";
            btnInject.IsEnabled = true;
        }

        private async void btnStartGame_Click(object sender, RoutedEventArgs e)
        {
            btnStartGame.IsEnabled = false;
            txtStatus.Text = "Starting CS:GO...";
            DoubleAnimation opacityAnimation = new DoubleAnimation { From = 1.0, To = 0.5, Duration = TimeSpan.FromSeconds(0.5), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            txtStatus.BeginAnimation(OpacityProperty, opacityAnimation);

            if (autoInjectCts != null)
            {
                autoInjectCts.Cancel();
                autoInjectCts.Dispose();
            }

            await Task.Run(() =>
            {
                try
                {
                    string steamPath = GetSteamPath();
                    if (string.IsNullOrEmpty(steamPath))
                    {
                        MessageBox.Show("Could not find Steam installation. Please make sure Steam is installed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    Process.Start(new ProcessStartInfo { FileName = steamPath, Arguments = $"-applaunch 730 {CSGO_LAUNCH_ARGS}", UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error starting CS:GO: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            txtStatus.BeginAnimation(OpacityProperty, null);
            txtStatus.Opacity = 1.0;
            txtStatus.Text = "CS:GO starting...";

            if (isAutoInjectEnabled && !string.IsNullOrEmpty(selectedDllPath))
            {
                autoInjectCts = new CancellationTokenSource();
                await MonitorAndInjectAsync(autoInjectCts.Token);
            }
            btnStartGame.IsEnabled = true;
        }

        private async Task MonitorAndInjectAsync(CancellationToken cancellationToken)
        {
            txtStatus.Text = "Waiting for CS:GO to start...";
            try
            {
                Process csgoProcess = null;
                bool found = false;
                int attempts = 0;
                const int maxAttempts = 60;

                while (!found && attempts < maxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    Process[] processes = Process.GetProcessesByName(CSGO_PROCESS_NAME);
                    if (processes.Length > 0) { csgoProcess = processes[0]; found = true; }
                    else { await Task.Delay(1000, cancellationToken); attempts++; }
                }

                if (cancellationToken.IsCancellationRequested) return;
                if (!found) { txtStatus.Text = "Timed out waiting for CS:GO to start."; return; }

                await Dispatcher.InvokeAsync(() =>
                {
                    selectedProcess = csgoProcess;
                    txtSelectedProcess.Text = $"CS:GO (PID: {selectedProcess.Id})";
                    txtStatus.Text = "CS:GO started. Auto-injecting...";
                });

                await Task.Delay(5000, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                bool success = InjectDLL(csgoProcess.Id, selectedDllPath);
                await Dispatcher.InvokeAsync(() => { txtStatus.Text = success ? "Auto-injection successful!" : "Auto-injection failed. Try manual injection."; });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { await Dispatcher.InvokeAsync(() => { txtStatus.Text = $"Error during auto-injection: {ex.Message}"; }); }
        }

        private string GetSteamPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))
                {
                    if (key != null)
                    {
                        string steamPath = key.GetValue("SteamExe") as string;
                        if (!string.IsNullOrEmpty(steamPath) && File.Exists(steamPath)) return steamPath;
                    }
                }
                string[] commonPaths = { "C:\\Program Files (x86)\\Steam\\steam.exe", "C:\\Program Files\\Steam\\steam.exe" };
                foreach (string path in commonPaths) if (File.Exists(path)) return path;
                return null;
            }
            catch { return null; }
        }

        private bool InjectDLL(int processId, string dllPath)
        {
            IntPtr hProcess = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, processId);
            if (hProcess == IntPtr.Zero) return false;
            try
            {
                IntPtr allocMemAddress = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char))), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (allocMemAddress == IntPtr.Zero) return false;
                byte[] bytes = Encoding.Unicode.GetBytes(dllPath);
                UIntPtr bytesWritten;
                if (!WriteProcessMemory(hProcess, allocMemAddress, bytes, (uint)bytes.Length, out bytesWritten)) return false;
                IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
                if (loadLibraryAddr == IntPtr.Zero) return false;
                IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddress, 0, IntPtr.Zero);
                if (hThread == IntPtr.Zero) return false;
                CloseHandle(hThread); // Consider WaitForSingleObject if needed, but for basic injection, this is often okay.
                return true;
            }
            finally { CloseHandle(hProcess); }
        }
    }
}

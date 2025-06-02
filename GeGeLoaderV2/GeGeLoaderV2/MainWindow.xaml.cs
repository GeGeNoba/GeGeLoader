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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
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

        public MainWindow()
        {
            InitializeComponent();
            
            // Make the window draggable
            this.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    this.DragMove();
            };

            // Set up auto-inject checkbox
            chkAutoInject.Checked += (s, e) => isAutoInjectEnabled = true;
            chkAutoInject.Unchecked += (s, e) => isAutoInjectEnabled = false;
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            // Cancel any pending auto-inject operations
            if (autoInjectCts != null)
            {
                autoInjectCts.Cancel();
                autoInjectCts.Dispose();
            }
            this.Close();
        }

        private void btnRefreshCsgoProcess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Find CS:GO process
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
                // Find Steam process
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

            if (openFileDialog.ShowDialog() == true)
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

            // Disable the button during injection
            btnInject.IsEnabled = false;
            txtStatus.Text = "Injecting...";

            // Create animation for the status text
            DoubleAnimation opacityAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.5,
                Duration = TimeSpan.FromSeconds(0.5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            txtStatus.BeginAnimation(OpacityProperty, opacityAnimation);

            bool success = false;

            // Perform injection in a background task
            await Task.Run(() =>
            {
                success = InjectDLL(selectedProcess.Id, selectedDllPath);
            });

            // Stop the animation
            txtStatus.BeginAnimation(OpacityProperty, null);
            txtStatus.Opacity = 1.0;

            // Update UI based on result
            if (success)
            {
                txtStatus.Text = "Injection successful!";
            }
            else
            {
                txtStatus.Text = "Injection failed. Please try again.";
            }

            // Re-enable the button
            btnInject.IsEnabled = true;
        }

        private async void btnStartGame_Click(object sender, RoutedEventArgs e)
        {
            // Disable the button during startup
            btnStartGame.IsEnabled = false;
            txtStatus.Text = "Starting CS:GO...";

            // Create animation for the status text
            DoubleAnimation opacityAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.5,
                Duration = TimeSpan.FromSeconds(0.5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            txtStatus.BeginAnimation(OpacityProperty, opacityAnimation);

            // Cancel any existing auto-inject operation
            if (autoInjectCts != null)
            {
                autoInjectCts.Cancel();
                autoInjectCts.Dispose();
            }

            Process csgoProcess = null;

            // Start CS:GO in a background task
            await Task.Run(() =>
            {
                try
                {
                    // Try to find Steam path from registry
                    string steamPath = GetSteamPath();
                    if (string.IsNullOrEmpty(steamPath))
                    {
                        MessageBox.Show("Could not find Steam installation. Please make sure Steam is installed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Launch CS:GO through Steam
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = steamPath,
                        Arguments = $"-applaunch 730 {CSGO_LAUNCH_ARGS}",
                        UseShellExecute = true
                    };

                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error starting CS:GO: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            // Stop the animation
            txtStatus.BeginAnimation(OpacityProperty, null);
            txtStatus.Opacity = 1.0;
            txtStatus.Text = "CS:GO starting...";

            // If auto-inject is enabled, start monitoring for CS:GO process
            if (isAutoInjectEnabled && !string.IsNullOrEmpty(selectedDllPath))
            {
                autoInjectCts = new CancellationTokenSource();
                await MonitorAndInjectAsync(autoInjectCts.Token);
            }

            // Re-enable the button
            btnStartGame.IsEnabled = true;
        }

        private async Task MonitorAndInjectAsync(CancellationToken cancellationToken)
        {
            txtStatus.Text = "Waiting for CS:GO to start...";

            try
            {
                // Wait for CS:GO to start (with timeout)
                Process csgoProcess = null;
                bool found = false;
                int attempts = 0;
                const int maxAttempts = 60; // 60 * 1 second = 60 seconds timeout

                while (!found && attempts < maxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    Process[] processes = Process.GetProcessesByName(CSGO_PROCESS_NAME);
                    if (processes.Length > 0)
                    {
                        csgoProcess = processes[0];
                        found = true;
                    }
                    else
                    {
                        await Task.Delay(1000, cancellationToken); // Check every second
                        attempts++;
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                    return;

                if (!found)
                {
                    txtStatus.Text = "Timed out waiting for CS:GO to start.";
                    return;
                }

                // Update UI with found process
                await Dispatcher.InvokeAsync(() =>
                {
                    selectedProcess = csgoProcess;
                    txtSelectedProcess.Text = $"CS:GO (PID: {selectedProcess.Id})";
                    txtStatus.Text = "CS:GO started. Auto-injecting...";
                });

                // Wait a bit for the game to initialize
                await Task.Delay(5000, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    return;

                // Perform injection
                bool success = InjectDLL(csgoProcess.Id, selectedDllPath);

                // Update UI based on result
                await Dispatcher.InvokeAsync(() =>
                {
                    if (success)
                    {
                        txtStatus.Text = "Auto-injection successful!";
                    }
                    else
                    {
                        txtStatus.Text = "Auto-injection failed. Try manual injection.";
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Operation was canceled, do nothing
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    txtStatus.Text = $"Error during auto-injection: {ex.Message}";
                });
            }
        }

        private string GetSteamPath()
        {
            try
            {
                // Try to get Steam path from registry
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))
                {
                    if (key != null)
                    {
                        string steamPath = key.GetValue("SteamExe") as string;
                        if (!string.IsNullOrEmpty(steamPath) && File.Exists(steamPath))
                        {
                            return steamPath;
                        }
                    }
                }

                // Try common installation paths if registry fails
                string[] commonPaths = new string[]
                {
                    "C:\\Program Files (x86)\\Steam\\steam.exe",
                    "C:\\Program Files\\Steam\\steam.exe"
                };

                foreach (string path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private bool InjectDLL(int processId, string dllPath)
        {
            // Get handle to process
            IntPtr hProcess = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, processId);
            if (hProcess == IntPtr.Zero)
                return false;

            try
            {
                // Allocate memory for the DLL path
                IntPtr allocMemAddress = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char))), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (allocMemAddress == IntPtr.Zero)
                    return false;

                // Write the DLL path to the allocated memory
                byte[] bytes = Encoding.Unicode.GetBytes(dllPath);
                UIntPtr bytesWritten;
                if (!WriteProcessMemory(hProcess, allocMemAddress, bytes, (uint)bytes.Length, out bytesWritten))
                    return false;

                // Get the address of LoadLibraryW
                IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
                if (loadLibraryAddr == IntPtr.Zero)
                    return false;

                // Create a remote thread that calls LoadLibraryW with the DLL path as argument
                IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddress, 0, IntPtr.Zero);
                if (hThread == IntPtr.Zero)
                    return false;

                // Wait for the thread to finish and clean up
                CloseHandle(hThread);
                return true;
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
    }
}

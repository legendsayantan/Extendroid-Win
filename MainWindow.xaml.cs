using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.UI.Input;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using System.Threading;
using Extendroid.Lib;
using AdvancedSharpAdbClient;
using ListViewItem = Microsoft.UI.Xaml.Controls.ListViewItem;
using Button = Microsoft.UI.Xaml.Controls.Button;
using SelectionChangedEventArgs = Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs;
using System.Text;
using StackPanel = Microsoft.UI.Xaml.Controls.StackPanel;
using TextBlock = Microsoft.UI.Xaml.Controls.TextBlock;
using Grid = Microsoft.UI.Xaml.Controls.Grid;
using AdvancedSharpAdbClient.DeviceCommands;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Extendroid
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private StorageFolder localDir = ApplicationData.Current.LocalFolder;
        private StorageFolder scrcpyPath = null;
        Thread tickThread;
        AdbManager adbManager;
        SessionManager sessionManager;
        IEnumerable<AdvancedSharpAdbClient.Models.DeviceData> devices;
        Boolean loading = false;
        private InputCursor? OriginalInputCursor { get; set; }

        List<AppItem> installedApps = new List<AppItem>();
        List<AppItem> systemApps = new List<AppItem>();
        List<AppItem> windows = new List<AppItem>();
        public string SmsLimitValue
        {
            get {
                return ApplicationData.Current.LocalSettings.Values["smslimit"]?.ToString() ?? "25";
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["smslimit"] = value;
            }
        }

        public MainWindow()
        {
            this.InitializeComponent();
            this.Closed += MainWindow_Closed;
            InitializeAsync();
            adbManager = new AdbManager();
            sessionManager = new SessionManager();
            //Refresh connected devices periodically
            tickThread = new Thread(new ThreadStart(OnTick));
            tickThread.Start();

        }

        private async Task InitializeAsync()
        {
            scrcpyPath = await localDir.CreateFolderAsync("scrcpy", CreationCollisionOption.OpenIfExists);
            try {
                await scrcpyPath.GetFileAsync("scrcpy.exe");
            }
            catch (FileNotFoundException e)
            {
                string scrcpyFolder = Path.Combine(AppContext.BaseDirectory, "Assets\\scrcpy");
                string[] files = Directory.GetFiles(scrcpyFolder);
                foreach (string file in files)
                {
                    string destPath = Path.Combine(scrcpyPath.Path, Path.GetFileName(file));
                    File.Copy(file, destPath, true);
                }
            }
        }

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            foreach(var item in sessionManager.GetProcesses().Keys)
            {
                await sessionManager.RequestTermination(item);
            }
            Application.Current.Exit();
        }

        private void OnTick()
        {
            Boolean run = true;
            int count = 0;
            while (run && AdbServer.Instance.GetStatus().IsRunning)
            {
                count++;
                devices = AdbManager.adbClient.GetDevices().Where(d => d.State == AdvancedSharpAdbClient.Models.DeviceState.Online);
                var devicenames = devices.Select(s => s.Name + " " + s.Serial);
                try
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        foreach (var device in devicenames)
                        {
                            var listItems = DeviceList.Items;
                            if (!listItems.Any(a => ((Microsoft.UI.Xaml.Controls.ListViewItem)a).Content.ToString() == device))
                            {
                                ListViewItem item = new();
                                item.Content = device;
                                var brush = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["theme5"]);
                                item.Foreground = brush;
                                item.Resources["ListViewItemForegroundPointerOver"] = brush;
                                item.Resources["ListViewItemForegroundSelected"] = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["theme4"]);
                                DeviceList.Items.Add(item);
                                Console.WriteLine(DeviceList.Items.ToString());
                            }
                        }
                        foreach (var device in DeviceList.Items)
                        {
                            if (!devicenames.Any(d => d.Equals((device as ListViewItem).Content))) DeviceList.Items.Remove(device);
                        }
                    });
                }
                catch (System.NullReferenceException e) { }
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device)
                    {
                        ReloadActive(device);
                    }
                });
                if (count == 6)
                {
                    count = 0;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device)
                        {
                            ReloadApps(device);
                            ReloadBattery(device);
                        }
                    });

                }
                Thread.Sleep(5000);
                try
                {
                    if (!this.Visible) run = false;
                }
                catch (Exception e) { }
            }
        }

        

        private AdvancedSharpAdbClient.Models.DeviceData? ActiveDevice()
        {
            var item = (ListViewItem)DeviceList.SelectedItem;
            if (item == null) return null;
            var splits = item.Content.ToString().Split(" ");
            return devices.First(d => d.Name == splits[0] && d.Serial == splits[1]);
        }

        private async void ReloadBattery(AdvancedSharpAdbClient.Models.DeviceData device)
        {
            AdbManager.adbClient.ExecuteShellCommand(device, "dumpsys battery | grep level", (output) =>
            {
                var level = output.Split(":")[1].Trim();
                DispatcherQueue.TryEnqueue(() =>
                {
                    BatteryLevel.Text = level;
                });
                return true;
            });
        }

        private async Task ReloadActive(AdvancedSharpAdbClient.Models.DeviceData device)
        {
            var active = sessionManager.GetProcesses();
            windows.Clear();
            foreach (var item in active)
            {
                windows.Add(new AppItem
                {
                    Name = item.Key.Name,
                    ID = item.Key.Package,
                    Info = item.Key.Info.Replace("=", "")
                });
            }
            // Update UI
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (!DispatcherQueue.HasThreadAccess)
                    {
                        Console.Error.WriteLine("Not on UI Thread!");
                    }
                    if (ActiveGridRepeater != null && windows != null)
                    {
                        ActiveGridRepeater.ItemsSource = null;
                        ActiveGridRepeater.ItemsSource = windows;
                        ActiveGridRepeater.UpdateLayout();
                    }
                    else
                    {
                        Debug.WriteLine("UI elements not ready");
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    App.LogError(e);
                }
            });
        }

        private async Task ReloadApps(AdvancedSharpAdbClient.Models.DeviceData device, int delay = 0)
        {
            if (loading) return;
            loading = true;

            try
            {
                await Task.Delay(delay);

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(scrcpyPath.Path, "scrcpy.exe"),
                        Arguments = $"-s {device.Serial} --list-apps",
                        WorkingDirectory = scrcpyPath.Path,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (s, e) => Extendroid.Lib.Utils.AppendSafe(output, e.Data);
                process.ErrorDataReceived += (s, e) => Extendroid.Lib.Utils.AppendSafe(error, e.Data);

                process.Start();

                // Begin async reading immediately
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for process exit AND all output received
                await process.WaitForExitAsync().ConfigureAwait(false);

                // Wait an additional moment for final output
                await Task.Delay(100);

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Process failed (exit code {process.ExitCode}): {error}");
                }

                // Combine both streams since scrcpy mixes output
                HandleScrcpyOutput(output.ToString());
            }catch(Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                App.LogError(ex);
            }
            finally
            {
                loading = false;
            }
        }

        private void HandleScrcpyOutput(string output)
        {
            
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            systemApps.Clear();
            installedApps.Clear();
            foreach (var line in lines)
            {
                // Skip non-app lines
                if (!line.StartsWith(" * ") && !line.StartsWith(" - ")) continue;

                var parts = line.Substring(2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                // Combine app name (might contain spaces)
                var appName = string.Join(" ", parts.Take(parts.Length - 1));
                var packageId = parts.Last();

                if(line.StartsWith(" * "))
                {
                    systemApps.Add(new AppItem
                    {
                        Name = appName,
                        ID = packageId,
                        Info = "System"
                    });
                }
                else
                {
                    installedApps.Add(new AppItem
                    {
                        Name = appName,
                        ID = packageId,
                        Info = "Installed"
                    });
                }
            }

            // Update UI
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (!DispatcherQueue.HasThreadAccess)
                    {
                        Console.Error.WriteLine("Not on UI Thread!");
                    }
                    if (SystemGridRepeater != null && systemApps!=null)
                    {
                        SystemGridRepeater.ItemsSource = null;
                        SystemGridRepeater.ItemsSource = systemApps;
                        SystemGridRepeater.UpdateLayout();
                    }
                    if (AllGridRepeater != null && installedApps != null)
                    {
                        
                        AllGridRepeater.ItemsSource = null;
                        AllGridRepeater.ItemsSource = installedApps;
                        AllGridRepeater.UpdateLayout();
                    }
                    else
                    {
                        Debug.WriteLine("UI elements not ready");
                    }
                }
                catch(Exception e)
                {
                    Console.Error.WriteLine(e);
                    App.LogError(e);
                }
            });
        }

        private void OnConnectBtnClick(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            ConnectNewDevice connectNewDevice = new ConnectNewDevice(() => {
                devices = AdbManager.adbClient.GetDevices().Where(d => d.State == AdvancedSharpAdbClient.Models.DeviceState.Online);
            });
            connectNewDevice.Activate();
        }

        private async void OnRestartBtnClick(object sender, RoutedEventArgs e)
        {
            await adbManager.refreshAdb();

        }
        private void OnPanelBtnClick(object sender, RoutedEventArgs e)
        {
            Panel panel = new Panel();
            panel.Activate();
        }

        private async void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
        {
            if (ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device)
            {
                await ReloadApps(device);
                await ReloadActive(device);
                ReloadBattery(device);

                NotiList.ItemsSource = null;
                SMSList.ItemsSource = null;
            }
        }

        private async void OnLockBtnClick(object sender, RoutedEventArgs e)
        {
            if (ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device)
            {
                await sessionManager.execScrcpy(ArgumentsBuilder.DimPhysicalDisplay(device), null);
            }
        }

        private async void OnMirrorDeviceBtnClick(object sender, RoutedEventArgs e)
        {
            if (ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device)
            {
                CreateWindow createWindow = new CreateWindow(this, sessionManager, device, null);
                createWindow.Activate();
            }
        }
        
        private async void OnNewActionBtnClick(object sender, RoutedEventArgs e)
        {
            //new action
        }
        private async void OnTerminalBtnClick(object sender, RoutedEventArgs e)
        {
            if(ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device)
            {
                AdbManager.startTerminal(device);
            }
        }

        private async void OnReloadBtnClick(object sender, RoutedEventArgs e)
        {
            if (ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device)
            {
                await ReloadApps(device);
                await ReloadActive(device);
                ReloadBattery(device);
            }
        }

        private async void OnNotiBtnClick(object sender, RoutedEventArgs e)
        {
            if (ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device)
            {
                NotiHeader.Text = "Notifications (loading...)";
                // Run the notification retrieval and processing on a background thread
                var data = await Task.Run(() =>
                {
                    var notifications = AdbManager.getNotifications(device).Result
                        .OrderByDescending(x => x.when_val)
                        .GroupBy(x => new { x.opPkg, x.text_val, x.title })
                        .Select(group => group.First())
                        .Where(x => !string.IsNullOrWhiteSpace(x.title + x.text_val))
                        .Where(x => !(x.title.Trim().Equals("true") && x.text_val.Trim().Equals("0")))
                        .ToList();

                    // Map app names
                    foreach (var item in notifications)
                    {
                        AppItem? app = installedApps.Concat(systemApps).ToList().Find(a => a.ID == item.opPkg);
                        item.appName = app == null ? item.opPkg : app.Name;
                    }

                    return notifications;
                });

                // Update the UI on the main thread
                NotiList.ItemsSource = null;
                NotiHeader.Text = "Notifications";
                NotiList.ItemsSource = data;
            }
        }


        private async void OnSmsBtnClick(object sender, RoutedEventArgs e)
        {
            if (ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device)
            {
                SMSHeader.Text = "SMS Inbox (loading...)";
                var data = await Task.Run(() =>
                {
                    var smsData = AdbManager.getSms(device).Result
                        .OrderByDescending(x => x.date)
                        .GroupBy(x => new { x.address, x.body })
                        .Select(group => group.First())
                        .Where(x => !string.IsNullOrWhiteSpace(x.body))
                        .ToList();
                    smsData.RemoveAll(x => (x.body).Trim().Equals(string.Empty));
                    return smsData;
                });
                SMSList.ItemsSource = null;
                SMSHeader.Text = "SMS Inbox";
                SMSList.ItemsSource = data;

            }
        }

        private void OnActiveGridItemClick(object sender, PointerRoutedEventArgs e)
        {
            
        }

        private void OnSystemAppGridItemClick(object sender, PointerRoutedEventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                if(ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device)
                {
                    var appPackage = (stackPanel.Children.ElementAt(1) as TextBlock).Text;
                    var app = systemApps.Find(a => a.ID == appPackage);
                    CreateWindow createWindow = new CreateWindow(this, sessionManager, device, app);
                    createWindow.Activate();
                }
            }
        }

        private void OnInstalledAppGridItemClick(object sender, PointerRoutedEventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                if(ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device)
                {
                    var appPackage = (stackPanel.Children.ElementAt(1) as TextBlock).Text;
                    var app = installedApps.Find(a => a.ID == appPackage);
                    CreateWindow createWindow = new CreateWindow(this, sessionManager, device, app);
                    createWindow.Activate();
                }
            }
        }

        private async void OnKillActiveItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button b)
            {
                try
                {
                    if(ActiveDevice() is AdvancedSharpAdbClient.Models.DeviceData device )
                    {
                        var package = (((b.Parent as Grid).Children.ElementAt(0) as StackPanel).Children.ElementAt(1) as TextBlock).Text;
                        if(package == device.Name)
                        {
                            await sessionManager.RequestTermination(new ThreadKey
                            {
                                Name = "Screen Mirroring",
                                Package = device.Name,
                                Serial = device.Serial
                            });
                        }
                        else
                        {
                            var app = installedApps.Concat(systemApps).ToList().Find(a => a.ID == package);

                            await sessionManager.RequestTermination(new ThreadKey
                            {
                                Name = app.Name,
                                Package = app.ID,
                                Serial = device.Serial
                            });
                        }
                    }
                    
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    App.LogError(ex);
                }
            }
        }

        private void OnPointerEnter(object sender, PointerRoutedEventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                OriginalInputCursor = GetCursor(stackPanel) ?? InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                ChangeCursor(stackPanel, InputSystemCursor.Create(InputSystemCursorShape.Hand));
            }
        }
        private void OnPointerExit(object sender, PointerRoutedEventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                if (OriginalInputCursor != null)
                {
                    ChangeCursor(stackPanel, OriginalInputCursor);
                }
            }
        }
        public static InputCursor GetCursor(UIElement element)
        {
            // Get the type of the UIElement
            Type elementType = element.GetType();

            // Get the field info for the ProtectedCursor
            PropertyInfo protectedCursorField = elementType.GetProperty("ProtectedCursor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance);

            if (protectedCursorField == null)
            {
                throw new InvalidOperationException("ProtectedCursor field not found.");
            }

            // Get the value of the ProtectedCursor
            return (InputCursor)protectedCursorField.GetValue(element);
        }
        public static void ChangeCursor(UIElement uiElement, InputCursor cursor)
        {
            Type type = typeof(UIElement);
            type.InvokeMember("ProtectedCursor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance, null, uiElement, new object[] { cursor });
        }
        public static void RestartApp()
        {
            // Get the current process
            var currentProcess = Process.GetCurrentProcess();

            // Start a new instance of the application
            Process.Start(currentProcess.MainModule.FileName);

            // Close the current instance
            Application.Current.Exit();
        }

    }

    public class AppItem
    {
        public string Name { get; set; }
        public string ID { get; set; }
        public string Info { get; set; }
    }
}

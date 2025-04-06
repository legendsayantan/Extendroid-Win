using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Graphics;
using Extendroid.Lib;
using System.Threading;
using Windows.Storage;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Extendroid
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 
    public sealed partial class Panel : Window
    {
        private StorageFolder scrcpyFolder;

        public Panel()
        {
            this.InitializeComponent();

            // Get the AppWindow for the current window
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Set the size of the window
            appWindow.Resize(new SizeInt32(600, 500));


            scrcpyFolder = ApplicationData.Current.LocalFolder.CreateFolderAsync("scrcpy", CreationCollisionOption.OpenIfExists).AsTask().Result;

        }

        private void OnLogsClick(object sender, RoutedEventArgs e)
        {
            App.openLogFolder();
        }
        private void OnKillAdbClick(object sender, RoutedEventArgs e)
        {
            AdbManager.adbClient.KillAdb();
        }
        private void OnShortcutClick(object sender, RoutedEventArgs e)
        {
            Thread t = new Thread(() =>
            {
                try
                {
                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = Path.Combine(scrcpyFolder.Path, "scrcpy.exe"),
                            Arguments = $"-h",
                            WorkingDirectory = scrcpyFolder.Path,
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
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Process failed (exit code {process.ExitCode}): {error}");
                    }

                    var data = Utils.GetSectionDataAsString(output.ToString(), "Shortcuts", 2);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        Output.Text = data;
                    });
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    App.LogError(ex);
                }
            });
            t.Start();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.AppNotifications;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Path = System.IO.Path;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Extendroid
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Console.WriteLine("App()");
            this.InitializeComponent();

            this.UnhandledException += App_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Console.WriteLine("OnLaunched()");
            m_window = new MainWindow();
            m_window.Activate();
        }

        private Window? m_window;

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogError(e.Exception);
            // Optionally, mark the exception as handled so the app can continue.
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogError(e.Exception);
            // Mark as observed to prevent process termination.
            e.SetObserved();
        }

        public static string getLogFolder()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        public static async void openLogFolder()
        {
            string localCachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages",
                Windows.ApplicationModel.Package.Current.Id.FamilyName,
                "LocalCache\\Local");
            if (Directory.Exists(localCachePath))
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = localCachePath,
                    UseShellExecute = true
                });
            }
            else
            {
                Debug.WriteLine("LocalCache folder not found!");
            }

        }

        /// <summary>
        /// Logs the provided exception to a file with detailed information.
        /// </summary>
        /// <param name="ex">The exception to log.</param>
        public static async void LogError(Exception ex)
        {
            try
            {
                // Create a path in the LocalApplicationData folder so the user can access it.
                string logFolderPath = getLogFolder();
                string logFilePath = System.IO.Path.Combine(logFolderPath, "CrashLog.txt");
                //create folder
                if (!Directory.Exists(logFolderPath))
                {
                    Directory.CreateDirectory(logFolderPath);
                }
                string logEntry = BuildLogEntry(ex);
                using (StreamWriter writer = new StreamWriter(logFilePath, true, Encoding.UTF8))
                {
                    writer.WriteLine(logEntry);
                }
                ShowNotification("Encountered an error!", ex.Message);
            }
            catch (Exception logEx)
            {
                // If logging fails, write to Debug output.
                Debug.WriteLine("Error logging exception: " + logEx);
            }
        }

        public static async void LogRaw(string message)
        {
            try
            {
                // Create a path in the LocalApplicationData folder so the user can access it.
                string logFolderPath = getLogFolder();
                string logFilePath = System.IO.Path.Combine(logFolderPath, "CrashLog.txt");
                //create folder
                if (!Directory.Exists(logFolderPath))
                {
                    Directory.CreateDirectory(logFolderPath);
                }
                using (StreamWriter writer = new StreamWriter(logFilePath, true, Encoding.UTF8))
                {
                    writer.WriteLine($"Log -> {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine(message);
                }
            }
            catch (Exception logEx)
            {
                // If logging fails, write to Debug output.
                Debug.WriteLine("Error logging exception: " + logEx);
            }
        }

        /// <summary>
        /// Builds a detailed string containing exception and environment information.
        /// </summary>
        private static string BuildLogEntry(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==== Crash Report ====");
            sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("OS: " + Environment.OSVersion.ToString());
            sb.AppendLine("App Version: " + GetAppVersion());
            sb.AppendLine("Exception Type: " + ex.GetType().FullName);
            sb.AppendLine("Message: " + ex.Message);
            sb.AppendLine("Stack Trace: " + ex.StackTrace);

            // Include inner exceptions (if any)
            Exception? inner = ex.InnerException;
            while (inner != null)
            {
                sb.AppendLine("---- Inner Exception ----");
                sb.AppendLine("Exception Type: " + inner.GetType().FullName);
                sb.AppendLine("Message: " + inner.Message);
                sb.AppendLine("Stack Trace: " + inner.StackTrace);
                inner = inner.InnerException;
            }
            sb.AppendLine("=======================");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Retrieves the current app version.
        /// </summary>
        private static string GetAppVersion()
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? version.ToString() : "Unknown";
        }

        public static void ShowNotification(string title, string message)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(message)
                    .Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Notification failed: {ex.Message}");
            }
        }

    }
}

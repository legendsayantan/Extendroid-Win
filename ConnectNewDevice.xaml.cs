using Extendroid.Lib;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics;

namespace Extendroid
{
    public sealed partial class ConnectNewDevice : Window
    {
        // Constants for window styles
        private const int GWL_STYLE = -16;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const int WS_THICKFRAME = 0x00040000; // Allows resizing
        private const int WS_BORDER = 0x00800000; // Border of the window
        private const int WS_CAPTION = 0x00C00000; // Caption of the window
        private const int WS_MINIMIZEBOX = 0x00020000; // Minimize box
        private const int WS_MAXIMIZEBOX = 0x00010000; // Maximize box
        private const int WS_SYSMENU = 0x00080000; // System menu

        // Import SetWindowLong from user32.dll
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // Importing the SetWindowPos function from user32.dll
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        string providedIP;
        string providedPort;

        public ConnectNewDevice(Action OnConnect)
        {
            this.InitializeComponent();
            // Get the AppWindow for the current window
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Set the size of the window
            appWindow.Resize(new SizeInt32(600, 450));

            // Remove the thick frame style to make the window non-resizable
            SetWindowLong(hwnd, GWL_STYLE,
                WS_BORDER | WS_CAPTION | WS_MINIMIZEBOX | WS_SYSMENU);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);

            AdbManager adbManager = new AdbManager(QR, (Windows.UI.Color)Application.Current.Resources["theme5"], (Windows.UI.Color)Application.Current.Resources["theme1"]);

            AdbManager.StateCallback = (msg) => {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (msg.Contains("connected", StringComparison.OrdinalIgnoreCase))
                    {
                        Close();
                        OnConnect.Invoke();
                    }
                    ConnectStatus.Text = msg;
                });
            };
        }

        private void PairingCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Get the current text
            var textBox = sender as TextBox;
            string input = textBox.Text;

            // Remove non-numeric characters
            if (!string.IsNullOrEmpty(input) && !int.TryParse(input, out _))
            {
                // Remove the last character if it is not a number
                textBox.Text = string.Join("", input.Where(char.IsDigit));
                textBox.SelectionStart = textBox.Text.Length; // Move the cursor to the end
            }
        }

        private void IpPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Get the current text
            var textBox = sender as TextBox;
            string input = textBox.Text;

            // Check validity
            string pattern = @"^(?:(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9]):([1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])$";
            Regex regex = new Regex(pattern);
            if (regex.IsMatch(input))
            {
                var providedIP = input.Split(':')[0];
                var providedPort = input.Split(':')[1];
            }
        }

        private async void OnPairingCodeSubmit(object sender, RoutedEventArgs e)
        {
            // Get the current text
            var textBox = PairingCode;
            string input = textBox.Text;

            // Check if the input is a valid pairing code
            if (input.Length == 6)
            {
                await AdbManager.FindDeviceAndPair(input);
            }
        }


        private async void OnIpPortSubmit(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(providedIP) || string.IsNullOrEmpty(providedPort))
            {
                AdbManager.tryConnectTo(providedIP, int.Parse(providedPort), () =>
                {
                    //success
                });
            }
        }
    }
}

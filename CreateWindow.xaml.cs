using Extendroid.Lib;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.AppNotifications;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using Windows.Graphics;
using Windows.Storage;
using TextBox = Microsoft.UI.Xaml.Controls.TextBox;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;

namespace Extendroid
{
    public sealed partial class CreateWindow : Window
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

        SessionManager sessionManager;
        AdvancedSharpAdbClient.Models.DeviceData device;
        AppItem app;

        public List<OptionItem> ResolutionOptions { get; set; }
        public List<OptionItem> AspectRatioOptions { get; set; }
        public List<string> DensityOptions { get; set; }
        public List<string> Orientations { get; set; }
        public List<string> ShortcutKeys { get; set; }


        public CreateWindow(Window parent,SessionManager s,AdvancedSharpAdbClient.Models.DeviceData d, AppItem item)
        {
            this.InitializeComponent();
            RootPanel.DataContext = this;
            Densities.Loaded += (sender, e) => LoadSettings();
            Advanced.Loaded += (sender, e) => LoadAdvancedSettings();
            ResolutionOptions = new List<OptionItem>
            {
                new OptionItem("Default", "Assets/default.png"),
                new OptionItem("480", "Assets/res_480p.png"),
                new OptionItem("720", "Assets/res_720p.png"),
                new OptionItem("1440", "Assets/res_2k.png"),
                new OptionItem("2160", "Assets/res_4k.png")
            };

            AspectRatioOptions = new List<OptionItem>
            {
                new OptionItem("9/16", "Assets/ar_9_16.png"),
                new OptionItem("2/3", "Assets/ar_2_3.png"),
                new OptionItem("1/1", "Assets/ar_1_1.png"),
                new OptionItem("3/2", "Assets/ar_3_2.png"),
                new OptionItem("16/9", "Assets/ar_16_9.png")
            };

            DensityOptions = new List<string>
            {
                "Default","120","160","200","240","320","480","640"
            };

            Orientations = new List<string>
            {
                "Assets/portrait.png","Assets/landscape.png"
            };

            ShortcutKeys = new List<string>
            {
                "lctrl", "rctrl", "lalt", "ralt", "lsuper", "rsuper"
            };

            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(500, 380));
            // Remove the thick frame style to make the window non-resizable
            SetWindowLong(hwnd, GWL_STYLE,
                WS_BORDER | WS_CAPTION | WS_MINIMIZEBOX | WS_SYSMENU);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
            
            sessionManager = s;
            device = d;
            app = item;
            Title = "Start " + item.Name+" ("+item.Info+") on "+d.Name;

            
        }

        private async void StartAppClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettings();
                SaveAdvancedSettings();
                await sessionManager.RequestNew(device, app);
                Close();
            }catch(Exception ex){
                Console.Error.WriteLine(ex);
            }
        }

        private async void OnOrientationToggled(object sender, RoutedEventArgs e)
        {
            if(Orientation.Rotation == 0)
            {
                Orientation.Rotation = 90;
                Orientation.Translation = new System.Numerics.Vector3(25, 0, 0);
            }
            else
            {
                Orientation.Rotation = 0;
                Orientation.Translation = new System.Numerics.Vector3(0, 0, 0);
            }
        }
        private async void OnResolutionToggled(object sender, RoutedEventArgs e)
        {
            selectOnlyOne(sender, Resolutions);
        }
        private async void OnAspectToggled(object sender, RoutedEventArgs e)
        {
            selectOnlyOne(sender, Aspects);
        }
        private async void OnDensityToggled(object sender, RoutedEventArgs e)
        {
            selectOnlyOne(sender, Densities);
        }
        private static void selectOnlyOne(object sender,ItemsControl group)
        {
            if (sender is ToggleButton clickedButton)
            {
                // Loop through all items in the ItemsControl
                foreach (var item in group.Items)
                {
                    var container = group.ContainerFromItem(item) as ContentPresenter;
                    if (container != null)
                    {
                        // Get the ToggleButton inside the DataTemplate
                        ToggleButton toggleButton = FindChild<ToggleButton>(container);
                        if (toggleButton != null && toggleButton != clickedButton)
                        {
                            toggleButton.IsChecked = false;
                        }
                    }
                }
            }
        }
        // Helper method to find a child of a specific type in the visual tree
        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }
                var foundChild = FindChild<T>(child);
                if (foundChild != null)
                {
                    return foundChild;
                }
            }
            return null;
        }

        public void SaveSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;
            // Save Resolution
            var selectedResolution = GetSelectedOptionItem(Resolutions);
            settings.Values["Resolution"] = selectedResolution?.Name ?? "Default";

            // Save Aspect Ratio
            var selectedAspect = GetSelectedOptionItem(Aspects);
            settings.Values["AspectRatio"] = selectedAspect?.Name ?? "1/1";

            // Save Density
            var selectedDensity = GetSelectedDensity();
            settings.Values["Density"] = selectedDensity ?? "Default";

            // Save Orientation
            string orientation = Orientation.Rotation==0 ? "Portrait" : "Landscape";
            settings.Values["Orientation"] = orientation;

            // Save Toggle States
            SaveToggleState(settings, "OnTop", OnTopBtn);
            SaveToggleState(settings, "KeepAwake", KeepAwakeBtn);
            SaveToggleState(settings, "DisableScreen", DisableScreenBtn);
            
        }

        public void SaveAdvancedSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;
            SaveToggleState(settings, "DisableControl", DisableControlBtn);
            SaveToggleState(settings, "DisableVideo", DisableVideoBtn);
            SaveToggleState(settings, "DisableAudio", DisableAudioBtn);
            SaveToggleState(settings, "OnBackground", OnBackgroundBtn);
            SaveToggleState(settings, "NoBorder", NoBorderBtn);

            // Save Text Inputs
            SaveTextSetting(settings, "CropParams", CropParams);
            SaveTextSetting(settings, "RotationParams", RotationParams);
            SaveTextSetting(settings, "RecordPath", RecordPath);
            SaveTextSetting(settings, "ScreenTimeout", ScreenTimeout);
            SaveTextSetting(settings, "MirrorTimeout", MirrorTimeout);
            SaveTextSetting(settings, "MaxFPS", MaxFPS);

            // Save Shortcuts
            if (Shortcuts.SelectedItems.Count > 0)
            {
                var shortcuts = string.Join(",", Shortcuts.SelectedItems.Cast<string>());
                settings.Values["ShortcutKeys"] = shortcuts;
            }
            else
            {
                settings.Values["ShortcutKeys"] = string.Empty;
            }
        }

        private OptionItem GetSelectedOptionItem(ItemsControl itemsControl)
        {
            foreach (var item in itemsControl.Items)
            {
                var container = itemsControl.ContainerFromItem(item);
                if (container != null)
                {
                    var toggleButton = FindChild<ToggleButton>(container);
                    if (toggleButton?.IsChecked == true)
                    {
                        return item as OptionItem;
                    }
                }
            }
            return null;
        }

        private string GetSelectedDensity()
        {
            foreach (var item in Densities.Items)
            {
                var container = Densities.ContainerFromItem(item);
                if (container != null)
                {
                    var toggleButton = FindChild<ToggleButton>(container);
                    if (toggleButton?.IsChecked == true)
                    {
                        return item as string;
                    }
                }
            }
            return null;
        }

        private void SaveToggleState(ApplicationDataContainer settings, string key, ToggleButton button)
        {
            settings.Values[key] = button.IsChecked ?? false;
        }

        private void SaveTextSetting(ApplicationDataContainer settings, string key, TextBox textBox)
        {
            settings.Values[key] = string.IsNullOrWhiteSpace(textBox.Text) ? string.Empty : textBox.Text.Trim();
        }

        public void LoadSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;

            // Load Resolution
            LoadOptionSelection(Resolutions, settings.Values["Resolution"] as string ?? "Default");

            // Load Aspect Ratio
            LoadOptionSelection(Aspects, settings.Values["AspectRatio"] as string ?? "Default");

            // Load Density
            LoadDensitySelection(settings.Values["Density"] as string ?? "Default");

            // Load Orientation
            LoadOrientation(settings.Values["Orientation"] as string ?? "Portrait");

            // Load Toggle States
            LoadToggleState(OnTopBtn, settings.Values["OnTop"] as bool?);
            LoadToggleState(KeepAwakeBtn, settings.Values["KeepAwake"] as bool?);
            LoadToggleState(DisableScreenBtn, settings.Values["DisableScreen"] as bool?);
            
        }

        public void LoadAdvancedSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;

            LoadToggleState(DisableControlBtn, settings.Values["DisableControl"] as bool?);
            LoadToggleState(DisableVideoBtn, settings.Values["DisableVideo"] as bool?);
            LoadToggleState(DisableAudioBtn, settings.Values["DisableAudio"] as bool?);
            LoadToggleState(OnBackgroundBtn, settings.Values["OnBackground"] as bool?);
            LoadToggleState(NoBorderBtn, settings.Values["NoBorder"] as bool?);
            // Load Text Inputs
            LoadTextSetting(CropParams, settings.Values["CropParams"] as string);
            LoadTextSetting(RotationParams, settings.Values["RotationParams"] as string);
            LoadTextSetting(RecordPath, settings.Values["RecordPath"] as string);
            LoadTextSetting(ScreenTimeout, settings.Values["ScreenTimeout"] as string);
            LoadTextSetting(MirrorTimeout, settings.Values["MirrorTimeout"] as string);
            LoadTextSetting(MaxFPS, settings.Values["MaxFPS"] as string);

            // Load Shortcuts
            if (settings.Values["ShortcutKeys"] is string savedShortcuts)
            {
                if (savedShortcuts.Length == 0)
                {
                    savedShortcuts = "lalt,lsuper"; //Default Value
                }
                var shortcuts = savedShortcuts.Split(',').ToList();
                foreach (var item in shortcuts)
                {
                    var index = Shortcuts.Items.IndexOf(item);
                    if (index >= 0) Shortcuts.SelectedItems.Add(Shortcuts.Items[index]);
                }
            }
        }

        private void LoadOptionSelection(ItemsControl itemsControl, string selectedName)
        {
            foreach (var item in itemsControl.Items)
            {
                if (item is OptionItem option && option.Name == selectedName)
                {
                    var container = itemsControl.ContainerFromItem(item);
                    if (container != null)
                    {
                        var toggleButton = FindChild<ToggleButton>(container);
                        toggleButton.IsChecked = true;
                        selectOnlyOne(toggleButton, itemsControl);
                    }
                    break;
                }
            }
        }

        private void LoadDensitySelection(string selectedDensity)
        {
            foreach (var item in Densities.Items)
            {
                if (item as string == selectedDensity)
                {
                    var container = Densities.ContainerFromItem(item);
                    if (container != null)
                    {
                        var toggleButton = FindChild<ToggleButton>(container);
                        toggleButton.IsChecked = true;
                        selectOnlyOne(toggleButton, Densities);
                    }
                    break;
                }
            }
        }

        private void LoadOrientation(string orientation)
        {
            Console.WriteLine(orientation);
            var path = orientation == "Portrait"? "ms-appx:///Assets/portrait.png": "ms-appx:///Assets/landscape.png";
            var uri = new Uri(path, UriKind.Absolute);
            Orientation.Source = new BitmapImage(uri);
        }

        private void LoadToggleState(ToggleButton button, bool? state)
        {
            button.IsChecked = state ?? false;
        }

        private void LoadTextSetting(TextBox textBox, string value)
        {
            textBox.Text = value ?? string.Empty;
        }
    }

    public class OptionItem
    {
        public string Name { get; set; }
        public string ImagePath { get; set; }

        public OptionItem(string name, string imagePath)
        {
            Name = name;
            ImagePath = imagePath;
        }
    }
}

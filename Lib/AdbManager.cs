using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Zeroconf;
using QRCoder;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Microsoft.UI.Xaml.Media.Imaging;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using Image = Microsoft.UI.Xaml.Controls.Image;
using Windows.Storage;
using System.Text.Json;
using System.Diagnostics;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Receivers;
using System.Text.Json.Serialization;
using static System.Windows.Forms.Design.AxImporter;
namespace Extendroid.Lib
{
    internal class AdbManager
    {
        private static ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        readonly static string AdbPairingServiceType = "_adb-tls-pairing._tcp.local.";
        readonly static string PairingName = "Extendroid";
        private static string PairingPassword;
        private const string QrFormat = "WIFI:T:ADB;S:{0};P:{1};;";
        static Thread thread;
        static IReadOnlyList<IZeroconfHost> DiscoveredDomains = null;
        public static AdbClient adbClient;
        static List<string> ConnectedIps = new List<string>();
        static List<int> availablePorts = new List<int>();
        static StorageFolder adbFolder;

        public static Action<string> StateCallback;
        public AdbManager()
        {
            EnsureAdb();
            adbClient = new AdbClient();
            DiscoveredDomains = null;
            adbFolder = ApplicationData.Current.LocalFolder.CreateFolderAsync("scrcpy", CreationCollisionOption.OpenIfExists).AsTask().Result;
        }
        public AdbManager(Image i, Windows.UI.Color foregroundColor, Windows.UI.Color backgroundColor) : this()
        {
            //generate random password for qr pairing
            PairingPassword = Guid.NewGuid().ToString().Substring(0, 8);
            i.Source = GenerateQR(string.Format(QrFormat, PairingName, PairingPassword), foregroundColor, backgroundColor);
            TryToQRConnect();
        }

        public async Task EnsureAdb()
        {
            if (!AdbServer.Instance.GetStatus().IsRunning)
            {
                AdbServer server = new();
                StorageFolder adbFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync("scrcpy");
                StorageFile adbFile = await adbFolder.GetFileAsync("adb.exe");
                StartServerResult result = server.StartServer(adbFile.Path, false);
                if (result != StartServerResult.Started)
                {
                    Console.WriteLine("Can't start adb server");
                }
            }
        }

        public async Task refreshAdb()
        {
            await adbClient.KillAdbAsync();
            await Task.Delay(1500);
            await EnsureAdb();
        }

        public void TryToQRConnect()
        {
            thread = new Thread(new ThreadStart(QRConnect));
            thread.Start();
        }

        public static async Task FindDeviceAndPair(String Code)
        {
            while (!(DiscoveredDomains != null
                && DiscoveredDomains.Any()
                && DiscoveredDomains[0].IPAddresses.FirstOrDefault() != null
                ))
            {
                DiscoveredDomains = await ZeroconfResolver.ResolveAsync(AdbPairingServiceType);
            }

            foreach (var domain in DiscoveredDomains)
            {
                var ip = domain.IPAddresses.FirstOrDefault();
                int port = -1;
                if (Code == PairingPassword)
                {
                    if (domain.Services.ContainsKey(PairingName + "." + AdbPairingServiceType))
                    {
                        port = domain.Services[PairingName + "." + AdbPairingServiceType].Port;
                    }
                }
                else
                {
                    port = domain.Services.FirstOrDefault().Value.Port;
                }
                if (port != -1)
                {
                    StateCallback.Invoke($"Pairing with {ip}");
                    try
                    {
                        adbClient.Pair(ip, port, Code);
                        if (!ConnectedIps.Contains(ip)) await ConnectTo(ip);
                    }
                    catch (Exception e)
                    {
                        StateCallback.Invoke($"Error: {e.Message}");
                        App.LogError(e);
                        if (e.Message.Contains("'FAIL'", StringComparison.OrdinalIgnoreCase))
                        {
                            //fallback pairing
                            await Task.Delay(1500);
                            var dev = adbClient.GetDevices();
                            if (dev.Any((x) => x.Serial.Contains(ip)))
                            {
                                return;
                            }
                            StateCallback.Invoke($"Fallback Pairing with {ip}");
                            //run adb.exe pair ip:port code
                            ProcessStartInfo psi = new ProcessStartInfo
                            {
                                FileName = Path.Combine(adbFolder.Path,"adb.exe"),
                                Arguments = $"pair {ip}:{port} {Code}",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = adbFolder.Path
                            };
                            Process p = Process.Start(psi);
                            await p.WaitForExitAsync();
                            if (p.ExitCode == 0)
                            {
                                StateCallback.Invoke($"Fallback Pairing Success");
                                if (!ConnectedIps.Contains(ip)) await ConnectTo(ip);
                            }
                            else
                            {
                                StateCallback.Invoke($"Fallback Pairing Failed");
                            }
                        }
                    }
                }
            }
        }

        private static async void QRConnect()
        {
            await FindDeviceAndPair(PairingPassword);
        }

        public async static Task ConnectTo(string IP)
        {
            StateCallback.Invoke($"Connecting with {IP}");
            availablePorts = new List<int>();
            // Create a CancellationTokenSource to control task cancellation
            var cancellationTokenSource = new CancellationTokenSource();
            Thread thread = new Thread(new ThreadStart(async () =>
            {
                void OnPortFound(int port)
                {
                    availablePorts.Add(port);
                }
                await PortScanner.StartScan(IP, 30000, 49151, OnPortFound, cancellationTokenSource.Token);
            }));
            thread.Start();
            //schedule task every 3 seconds
            Thread connectionThread = new Thread(new ThreadStart(async () =>
            {
                var running = true;
                while (running)
                {
                    foreach (var port in availablePorts)
                    {
                        tryConnectTo(IP, port, () =>
                        {
                            //cancellationTokenSource.Cancel(); //lags out
                            running = false;
                        });
                        if (!running) break;
                    }
                    await Task.Delay(3000);
                }
            }));
            connectionThread.Start();

        }

        public static void tryConnectTo(string IP, int port, Action OnSuccess)
        {
            StateCallback.Invoke($"Connecting with \n{IP}:{port}");
            var output = adbClient.Connect(IP, port);
            App.LogRaw(output);
            if (output.Contains("connected", StringComparison.OrdinalIgnoreCase))
            {
                ConnectedIps.Add(IP);
                availablePorts = new List<int>();
                StateCallback.Invoke($"Connected.");
                OnSuccess.Invoke();
            }
        }

        public BitmapImage GenerateQR(string input, Windows.UI.Color foregroundColor, Windows.UI.Color backgroundColor)
        {
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(input, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new QRCode(qrCodeData))
                {
                    using (MemoryStream memory = new MemoryStream())
                    {
                        qrCode.GetGraphic(20, ConvertToDrawingColor(foregroundColor), ConvertToDrawingColor(backgroundColor), false).Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                        memory.Position = 0;
                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.SetSource(memory.AsRandomAccessStream());
                        return bitmapImage;
                    }
                }
            }
        }

        private System.Drawing.Color ConvertToDrawingColor(Windows.UI.Color color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static async Task<bool> ScanPortAsync(IPAddress ip, int port)
        {
            try
            {
                using (var tcpClient = new TcpClient())
                {
                    var connectTask = tcpClient.ConnectAsync(ip, port);
                    return await Task.WhenAny(connectTask, Task.Delay(5)) == connectTask && tcpClient.Connected;
                }
            }
            catch
            {
                //not a real exception, just a bad port
                return false;
            }
        }


        public static void startTerminal(AdvancedSharpAdbClient.Models.DeviceData device)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k @ECHO OFF & title Device Terminal & adb -s {device.Serial} shell",
                UseShellExecute = true, // Ensures the window is visible
                CreateNoWindow = false, // Allows the window to be seen
                WorkingDirectory = adbFolder.Path,
                WindowStyle = ProcessWindowStyle.Normal // Normal-sized window
            };

            Process.Start(psi);
        }

        public static List<NotificationRecord> getNotifications(AdvancedSharpAdbClient.Models.DeviceData device)
        {
            List<NotificationRecord> notifications = new List<NotificationRecord>();
            Thread t = new Thread(new ThreadStart(() =>
            {
                ConsoleOutputReceiver outputReceiver = new ConsoleOutputReceiver();
                string shellFilePath = Path.Combine(adbFolder.Path, "DeviceNotifications.sh");
                //read the file
                string shellFileContent = File.ReadAllText(shellFilePath);
                //send the content as input to the process
                adbClient.ExecuteShellCommand(device, shellFileContent, outputReceiver);

                outputReceiver.ToString();

                //deserialise
                notifications = JsonSerializer.Deserialize<List<NotificationRecord>>(outputReceiver.ToString());
            }));
            t.Start();
            t.Join();
            return notifications;
        }
    }
    public class NotificationRecord
    {
        //opPkg, icon, key, when_val, title, subText, text_val, progress, progressMax
        public string opPkg{ get; set; }
        public string appName{ get; set; }
        public string key { get; set; }
        [JsonPropertyName("when")]
        public string when_val { get; set; }
        [JsonPropertyName("android.title")]
        public string title { get; set; }
        [JsonPropertyName("android.subText")]
        public string subText { get; set; }
        [JsonPropertyName("android.text")]
        public string text_val { get; set; }
        [JsonPropertyName("android.progress")]
        public string progress { get; set; }
        [JsonPropertyName("android.progressMax")]
        public string progressMax { get; set; }

        // Computed property to get formatted time
        public string FormattedTime
        {
            get
            {
                

                if ((!when_val.Trim().Equals("0")) && long.TryParse(when_val, out long unixMilliseconds))
                {
                    // Convert Unix milliseconds to DateTimes
                    DateTime dateTime = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).ToLocalTime().DateTime;
                    string DateAndMonth = DateTime.Now.ToString("ddd").Equals(dateTime.ToString("ddd"))? string.Empty : dateTime.ToString("dd MMM ");
                    return dateTime.ToString($"{DateAndMonth}HH:mm");
                }
                return string.Empty;
            }
        }
    }
}

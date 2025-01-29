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

        public static Action<string> StateCallback;
        public AdbManager()
        {
            EnsureAdb();
            adbClient = new AdbClient();
            DiscoveredDomains = null;
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
                    StateCallback.Invoke($"Pairing with \n{ip}");
                    adbClient.Pair(ip, port, Code);
                    if (!ConnectedIps.Contains(ip)) await ConnectTo(ip);
                }
            }
        }

        private static async void QRConnect()
        {
            await FindDeviceAndPair(PairingPassword);
        }

        public async static Task ConnectTo(string IP)
        {
            StateCallback.Invoke($"Connecting with \n{IP}");
            var ports = new List<int>();
            // Create a CancellationTokenSource to control task cancellation
            var cancellationTokenSource = new CancellationTokenSource();
            Thread thread = new Thread(new ThreadStart(async () =>
            {
                void OnPortFound(int port)
                {
                    ports.Add(port);
                }
                await PortScanner.StartScan(IP, 30000, 49151, OnPortFound, cancellationTokenSource.Token);
            }));
            thread.Start();
            //schedule task every 3 seconds
            Thread connectionThread = new Thread(new ThreadStart(async () =>
            {
                while (true)
                {
                    foreach (var port in ports)
                    {
                        if (adbClient.Connect(IP, port).Contains("connected", StringComparison.OrdinalIgnoreCase))
                        {
                            ConnectedIps.Add(IP);
                            ports = new List<int>();
                            StateCallback.Invoke($"Connected.");
                            //cancellationTokenSource.Cancel(); //Because Cancelling lags out the app
                            return;
                        }
                    }
                    await Task.Delay(3000);
                }
            }));
            connectionThread.Start();

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
                return false;
            }
        }
    }
}

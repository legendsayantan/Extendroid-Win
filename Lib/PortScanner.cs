using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace Extendroid.Lib
{
    internal class PortScanner
    {
        // Callback delegate definition: will be called when an open port is found
        public delegate void PortFoundHandler(int port);

        // Method to scan a single port asynchronously
        public static async Task ScanPortAsync(IPAddress ip, int port, PortFoundHandler onPortFound, CancellationToken cancellationToken)
        {
            try
            {
                using (var tcpClient = new TcpClient() { SendTimeout = 100, ReceiveTimeout = 100 })
                {
                    await tcpClient.ConnectAsync(ip, port, cancellationToken);
                    if (tcpClient.Connected)
                    {
                        // If the port is open, invoke the callback with the port number
                        onPortFound?.Invoke(port);
                    }
                }
            }
            catch
            {
                // Catch block intentionally left empty since we are scanning for open ports only.
                // If an exception occurs, it indicates the port is not open or other errors.
            }
        }

        // Method to scan a range of ports in parallel and trigger callback when an open port is found
        public static async Task ScanPortsAsync(IPAddress ip, int startPort, int endPort, PortFoundHandler onPortFound, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            for (int port = startPort; port <= endPort; port++)
            {
                int currentPort = port; // Capture the loop variable

                // Check if the operation is canceled
                if (cancellationToken.IsCancellationRequested)
                    return;
                tasks.Add(Task.Run(() =>
                {
                    if (!cancellationToken.IsCancellationRequested) ScanPortAsync(ip, currentPort, onPortFound, cancellationToken);
                }));
            }
        }

        public static async Task StartScan(string ipAddressString, int startPort, int endPort, PortFoundHandler OnPortFound, CancellationToken cancellationToken)
        {

            // Parse the IP address
            IPAddress ipAddress = IPAddress.Parse(ipAddressString);
            await ScanPortsAsync(ipAddress, startPort, endPort, OnPortFound, cancellationToken);
        }
    }
}

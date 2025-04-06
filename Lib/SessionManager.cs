using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Extendroid.Lib
{
    public class SessionManager
    {
        ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
        private StorageFolder localDir = ApplicationData.Current.LocalFolder;
        private StorageFolder scrcpyFolder;
        private readonly ConcurrentDictionary<ThreadKey, Process> _processes = new ConcurrentDictionary<ThreadKey, Process>();
        private readonly Task initializationTask;

        public SessionManager() {
            initializationTask = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            scrcpyFolder = await localDir.CreateFolderAsync("scrcpy", CreationCollisionOption.OpenIfExists);
        }

        public async Task RequestNew(AdvancedSharpAdbClient.Models.DeviceData device, AppItem app)
        {
            await initializationTask;

            var argsBuilder = new ArgumentsBuilder();
            var args = argsBuilder.get(device, app);
            var threadKey = new ThreadKey { 
                Serial = device.Serial, 
                Name = (app == null ? "Screen Mirroring" : app.Name), 
                Package = (app == null ? device.Name : app.ID), 
                Info=argsBuilder.getResolution(settings) 
            };
            await execScrcpy(args, threadKey);
        }

        public async Task execScrcpy(string args,ThreadKey? threadKey)
        {
            string scrcpyExePath = Path.Combine(scrcpyFolder.Path, "scrcpy.exe"); // Ensure this path is correct for your environment

            var startInfo = new ProcessStartInfo
            {
                FileName = scrcpyExePath,
                Arguments = args,
                WorkingDirectory = scrcpyFolder.Path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (s, e) => AppendSafe(output, e.Data);
            process.ErrorDataReceived += (s, e) => AppendSafe(error, e.Data);

            if(threadKey != null)
            {
                process.Exited += (sender, e) =>
                {
                    _processes.TryRemove(threadKey, out _);
                    process.Dispose();
                };
            }

            try
            {
                process.Start();
                if(threadKey!=null) _processes.TryAdd(threadKey, process);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting scrcpy: {ex.Message}");
                App.LogError(ex);
                process.Dispose();
                throw;
            }
        }
        private static void AppendSafe(StringBuilder builder, string? data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                lock (builder)
                {
                    builder.AppendLine(data);
                }
            }
        }

        public IReadOnlyDictionary<ThreadKey, Process> GetProcesses()
        {
            return new Dictionary<ThreadKey, Process>(_processes);
        }

        public async Task RequestTermination(ThreadKey key)
        {
            if (_processes.TryRemove(key, out Process process))
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    public class ThreadKey
    {
        public string Serial { get; set; }
        public string Package { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }

        public override bool Equals(object obj)
        {
            return obj is ThreadKey key &&
                   Serial == key.Serial &&
                   Package == key.Package;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Serial, Name, Package);
        }
    }
}

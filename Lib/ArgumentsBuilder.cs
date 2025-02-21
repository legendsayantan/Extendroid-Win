using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Extendroid.Lib
{
    internal class ArgumentsBuilder
    {

        public string getResolution(ApplicationDataContainer settings)
        {
            settings = ApplicationData.Current.LocalSettings;
            var res = settings.Values["Resolution"] as string ?? "Default";
            var density = settings.Values["Density"] as string ?? "Default";
            var resolutionStr = "";
            if (!res.Equals("Default"))
            {
                var aspect = (settings.Values["AspectRatio"] as string).Split("/");
                if (int.Parse(aspect[0]) < int.Parse(aspect[1]))
                {
                    var height = int.Parse(res) * int.Parse(aspect[1]) / int.Parse(aspect[0]);
                    resolutionStr = $"={res}x{height}";
                }
                else
                {
                    var width = int.Parse(res) * int.Parse(aspect[0]) / int.Parse(aspect[1]);
                    resolutionStr = $"={width}x{res}";
                }
            }
            if (!density.Equals("Default"))
            {
                if (res.Equals("Default")) resolutionStr += "=";
                resolutionStr += $"/{density}dpi";
            }
            return resolutionStr;
        }
        public string get(AdvancedSharpAdbClient.Models.DeviceData device,AppItem app)
        {
            var settings = ApplicationData.Current.LocalSettings;
            string args = $"-s {device.Serial} --new-display{getResolution(settings).Replace("dpi","")}";
            args += $" --start-app={app.ID} --window-title=\"{app.Name} via Extendroid\" ";

            if (settings.Values["OnTop"] as bool? == true)
            {
                args += "--always-on-top ";
            }
            if (settings.Values["KeepAwake"] as bool? == true)
            {
                args += "--stay-awake ";
            }
            if (settings.Values["DisableScreen"] as bool? == true)
            {
                args += "--turn-screen-off ";
            }
            var orientation = settings.Values["Orientation"] as string ?? "Portrait";
            if (orientation.Equals("Landscape"))
            {
                args += "--orientation=90";
            }

            //Advanced settings
            if (settings.Values["DisableControl"] as bool? == true)
            {
                args += "--no-control ";
            }
            if (settings.Values["DisableVideo"] as bool? == true)
            {
                args += "--no-video ";
            }
            if (settings.Values["DisableAudio"] as bool? == true)
            {
                args += "--no-audio ";
            }
            if (settings.Values["OnBackground"] as bool? == true)
            {
                args += "--no-vd-destroy-content ";
            }
            if (settings.Values["NoBorder"] as bool? == true)
            {
                args += "--window-borderless ";
            }

            var cropParams = settings.Values["CropParams"] as string;
            var rotationParams = settings.Values["RotationParams"] as string;
            var recordPath = settings.Values["RecordPath"] as string;
            var screenTimeout = settings.Values["ScreenTimeout"] as string;
            var mirrorTimeout = settings.Values["MirrorTimeout"] as string;
            var maxFps = settings.Values["MaxFps"] as string;
            var shortcutKeys = settings.Values["ShortcutKeys"] as string;

            if (!string.IsNullOrEmpty(cropParams))
            {
                args += $"--crop={cropParams} ";
            }
            if (!string.IsNullOrEmpty(rotationParams))
            {
                args += $"--angle={rotationParams} ";
            }
            if (!string.IsNullOrEmpty(recordPath))
            {
                args += $"--record=\"{recordPath}\" ";
            }
            if (!string.IsNullOrEmpty(screenTimeout))
            {
                var mins = int.Parse(screenTimeout.Split(":")[0]);
                var secs = int.Parse(screenTimeout.Split(":")[1]);
                args += $"--screen-off-timeout={((mins * 60)+secs).ToString()} ";
            }
            if (!string.IsNullOrEmpty(mirrorTimeout))
            {
                var mins = int.Parse(mirrorTimeout.Split(":")[0]);
                var secs = int.Parse(mirrorTimeout.Split(":")[1]);
                args += $"--stop-mirroring-timeout={((mins * 60) + secs).ToString()} ";
            }
            if (!string.IsNullOrEmpty(maxFps))
            {
                args += $"--max-fps={maxFps} ";
            }
            if (!string.IsNullOrEmpty(shortcutKeys))
            {
                args += $"--shortcut-mod={shortcutKeys} ";
            }

            return args;
        }
    }
}

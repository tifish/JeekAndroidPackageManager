using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace JeekAndroidPackageManager;

public class Adb
{
    private static List<string> Run(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("adb.exe", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
        });

        var result = new List<string>();

        if (process == null)
            return result;

        var line = process.StandardOutput.ReadLine();

        while (line != null)
        {
            result.Add(line);
            line = process.StandardOutput.ReadLine();
        }

        process.WaitForExit();
        return result;
    }

    private static List<string> RunWithDevice(string device, string arguments)
    {
        return Run($"-s {device} {arguments}");
    }


    public static List<string> GetDevices()
    {
        var cmdLines = Run("devices");
        var result = new List<string>();
        for (var i = 1; i < cmdLines.Count; i++)
        {
            var line = cmdLines[i];
            if (string.IsNullOrEmpty(line))
                break;
            var device = line.Substring(0, line.IndexOf('\t'));
            result.Add(device);
        }

        return result;
    }

    public enum AppStatus
    {
        All,
        Enabled,
        Disabled,
    }

    public enum AppCategory
    {
        All,
        System,
        User,
    }

    public static List<string> GetPackages(string device, AppCategory appCategory, AppStatus appStatus)
    {
        var arguments = "shell pm list package";
        switch (appCategory)
        {
            case AppCategory.System:
                arguments += " -s";
                break;
            case AppCategory.User:
                arguments += " -3";
                break;
        }

        switch (appStatus)
        {
            case AppStatus.Enabled:
                arguments += " -e";
                break;
            case AppStatus.Disabled:
                arguments += " -d";
                break;
        }

        var cmdLines = RunWithDevice(device, arguments);

        var result = new List<string>();
        for (var i = 1; i < cmdLines.Count; i++)
        {
            var line = cmdLines[i];
            if (string.IsNullOrEmpty(line))
                break;
            var package = line.Substring(line.IndexOf(':') + 1);
            result.Add(package);
        }

        return result;
    }

    public static void DisablePackage(string device, string package)
    {
        RunWithDevice(device, $"shell pm disable-user {package}");
    }

    public static void EnablePackage(string device, string package)
    {
        RunWithDevice(device, $"shell pm enable {package}");
    }

    public static Dictionary<string, string> GetPackagesWithApkPaths(string device)
    {
        var cmdLines = RunWithDevice(device, "shell pm list package -f");

        var result = new Dictionary<string, string>();
        for (var i = 1; i < cmdLines.Count; i++)
        {
            var line = cmdLines[i];
            if (string.IsNullOrEmpty(line))
                break;
            line = line.Substring("package:".Length);
            var sepIndex = line.LastIndexOf('=');
            var path = line.Substring(0, sepIndex);
            var package = line.Substring(sepIndex + 1);
            result.Add(package, path);
        }

        return result;
    }

    public static void PushAapt(string device)
    {
        RunWithDevice(device, "push aapt-arm-pie /data/local/tmp");
        RunWithDevice(device, "shell chmod 0755 /data/local/tmp/aapt-arm-pie");
    }

    public static string GetAppName(string device, string apkPath)
    {
        var cmdLines = RunWithDevice(device, $"shell /data/local/tmp/aapt-arm-pie d badging {apkPath}");
        if (cmdLines.Count == 0)
            return null;

        var cnName = string.Empty;
        var name = string.Empty;
        for (var i = 1; i < cmdLines.Count; i++)
        {
            var line = cmdLines[i];
            if (string.IsNullOrEmpty(line))
                break;
            var sepIndex = line.IndexOf(':');
            if (sepIndex == -1)
                continue;
            var key = line[..sepIndex];
            if (key == "application-label-zh-CN")
                cnName = line[(sepIndex + 1)..].Trim('\'');
            if (key == "application-label")
                name = line[(sepIndex + 1)..].Trim('\'');
        }

        return cnName != string.Empty ? cnName : name;
    }
}

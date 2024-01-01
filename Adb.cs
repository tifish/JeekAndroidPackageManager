using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JeekAndroidPackageManager;

public class Adb
{
    private static async Task<List<string>> Run(string arguments)
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

        var line = await process.StandardOutput.ReadLineAsync();

        while (line != null)
        {
            result.Add(line);
            line = await process.StandardOutput.ReadLineAsync();
        }

        await process.WaitForExitAsync();
        return result;
    }

    private static async Task<List<string>> RunWithDevice(string device, string arguments)
    {
        return await Run($"-s {device} {arguments}");
    }

    public static async Task<List<string>> GetDevices()
    {
        var cmdLines = await Run("devices");
        var result = new List<string>();
        for (var i = 1; i < cmdLines.Count; i++)
        {
            var line = cmdLines[i];
            if (string.IsNullOrEmpty(line))
                break;
            var device = line[..line.IndexOf('\t')];
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

    public static async Task<List<string>> GetPackages(string device, AppCategory appCategory, AppStatus appStatus)
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

        var cmdLines = await RunWithDevice(device, arguments);

        var result = new List<string>();
        for (var i = 1; i < cmdLines.Count; i++)
        {
            var line = cmdLines[i];
            if (string.IsNullOrEmpty(line))
                break;
            var package = line[(line.IndexOf(':') + 1)..];
            result.Add(package);
        }

        return result;
    }

    public static async Task DisablePackage(string device, string package)
    {
        await RunWithDevice(device, $"shell pm disable-user {package}");
    }

    public static async Task EnablePackage(string device, string package)
    {
        await RunWithDevice(device, $"shell pm enable {package}");
    }

    public static async Task<Dictionary<string, string>> GetPackagesWithApkPaths(string device)
    {
        var cmdLines = await RunWithDevice(device, "shell pm list package -f");

        var result = new Dictionary<string, string>();
        for (var i = 1; i < cmdLines.Count; i++)
        {
            var line = cmdLines[i];
            if (string.IsNullOrEmpty(line))
                break;
            line = line["package:".Length..];
            var sepIndex = line.LastIndexOf('=');
            var path = line[..sepIndex];
            var package = line[(sepIndex + 1)..];
            result.Add(package, path);
        }

        return result;
    }

    public static async Task PushAapt(string device)
    {
        await RunWithDevice(device, "push aapt-arm-pie /data/local/tmp");
        await RunWithDevice(device, "shell chmod 0755 /data/local/tmp/aapt-arm-pie");
    }

    private static readonly Regex AttrRegex = new Regex(@"(\w+)='([^']*)'");

    public static async Task<string> GetAppName(string device, string apkPath)
    {
        var cmdLines = await RunWithDevice(device, $"shell /data/local/tmp/aapt-arm-pie d badging {apkPath}");
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
            switch (key)
            {
                case "application-label-zh-CN":
                    cnName = line[(sepIndex + 1)..].Trim('\'');
                    break;
                case "application-label":
                    name = line[(sepIndex + 1)..].Trim('\'');
                    break;
                case "application":
                    {
                        var content = line[(sepIndex + 1)..].Trim();
                        foreach (Match match in AttrRegex.Matches(content))
                        {
                            var attrName = match.Groups[1].Value;
                            var attrValue = match.Groups[2].Value;

                            if (attrName != "label")
                                continue;

                            name = attrValue;

                            if (cnName == string.Empty && name.Any(c => c >= 128))
                                cnName = name;
                        }

                        break;
                    }
            }
        }

        return cnName != string.Empty ? cnName : name;
    }
}

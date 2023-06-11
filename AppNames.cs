using System;
using System.Collections.Generic;
using System.IO;

namespace JeekAndroidPackageManager;

static class AppNames
{
    private static readonly Dictionary<string, string> _appPackageNameDict = new();

    public static string GetAppName(string package)
    {
        return _appPackageNameDict.TryGetValue(package, out var appName) ? appName : null;
    }

    public static void SetAppName(string package, string name)
    {
        _appPackageNameDict[package] = name;
    }

    private static readonly string ConfigFile =
        Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty, "AppNames.tab");

    public static void Load()
    {
        _appPackageNameDict.Clear();

        if (!File.Exists(ConfigFile))
            return;

        foreach (var line in File.ReadAllLines(ConfigFile))
        {
            if (string.IsNullOrEmpty(line))
                continue;

            var items = line.Split('\t');
            if (items.Length != 2)
                continue;

            _appPackageNameDict.Add(items[0], items[1]);
        }
    }

    public static void Save()
    {
        using var fs = new StreamWriter(ConfigFile);
        foreach (var (package, appName) in _appPackageNameDict)
            fs.WriteLine($"{package}\t{appName}");
    }
}

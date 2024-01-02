using System;
using System.Collections.Generic;
using System.IO;

namespace JeekAndroidPackageManager;

static class AppNames
{
    private static readonly Dictionary<string, Adb.AppName> _appPackageNameDict = new();

    public static Adb.AppName? GetAppName(string package)
    {
        return _appPackageNameDict.GetValueOrDefault(package);
    }

    public static void SetAppName(string package, Adb.AppName name)
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
            if (items.Length != 3)
                continue;

            _appPackageNameDict.Add(items[0], new Adb.AppName(items[1], items[2]));
        }
    }

    public static void Save()
    {
        using var fs = new StreamWriter(ConfigFile);
        foreach (var (package, appName) in _appPackageNameDict)
            fs.WriteLine($"{package}\t{appName.DefaultName}\t{appName.LocalName}");
    }
}

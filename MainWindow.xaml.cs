using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JeekAndroidPackageManager;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const char PackageNameSep = '=';

    private Adb.AppCategory _appCategory = Adb.AppCategory.All;
    private Adb.AppStatus _appStatus = Adb.AppStatus.All;

    public MainWindow()
    {
        AppNames.Load();

        InitializeComponent();
        DataContext = this;

        Devices = new ObservableCollection<string>();
        DisplayPackages = new ObservableCollection<string>();

        RefreshDevices().ConfigureAwait(false);
    }

    public ObservableCollection<string> Devices { get; set; }
    public List<string> Packages { get; set; } = new();
    public ObservableCollection<string> DisplayPackages { get; set; }

    public string CurrentDevice => DeviceListBox.SelectedItem as string ?? "";

    public string CurrentPackage
    {
        get
        {
            var text = PackageListBox.SelectedItem as string;
            var sepIndex = text.IndexOf(PackageNameSep);
            return sepIndex == -1 ? text : text.Substring(0, sepIndex);
        }
    }

    private async Task RefreshDevices()
    {
        Devices.Clear();
        foreach (var device in await Adb.GetDevices())
            Devices.Add(device);
    }

    private async Task RefreshPackages()
    {
        var device = CurrentDevice;
        if (string.IsNullOrEmpty(device))
            return;

        Packages = await Adb.GetPackages(device, _appCategory, _appStatus);
        FilterPackages();
    }

    private void FilterPackages()
    {
        DisplayPackages.Clear();
        var filter = FilterTextBox.Text;
        foreach (var package in Packages)
        {
            var appName = AppNames.GetAppName(package);
            if (package.Contains(filter)
                || (appName != null && (appName.DefaultName.Contains(filter) || appName.LocalName.Contains(filter))))
            {
                var displayName = package;
                if (appName != null)
                {
                    if (appName.LocalName != "")
                        displayName += $"{PackageNameSep}{appName.LocalName}";
                    if (appName.DefaultName != "" && appName.DefaultName != appName.LocalName)
                        displayName += $"{PackageNameSep}{appName.DefaultName}";
                }

                DisplayPackages.Add(displayName);
            }
        }
    }

    private async void DeviceListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await RefreshPackages();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDevices();
    }

    private async void DisablePackageButton_Click(object sender, RoutedEventArgs e)
    {
        var device = CurrentDevice;
        if (string.IsNullOrEmpty(device))
            return;

        var package = CurrentPackage;
        if (string.IsNullOrEmpty(package))
            return;

        await Adb.DisablePackage(device, package);
    }

    private async void EnablePackageButton_Click(object sender, RoutedEventArgs e)
    {
        var device = CurrentDevice;
        if (string.IsNullOrEmpty(device))
            return;

        var package = CurrentPackage;
        if (string.IsNullOrEmpty(package))
            return;

        await Adb.EnablePackage(device, package);
    }

    private async void GetNamesButton_Click(object sender, RoutedEventArgs e)
    {
        await CacheAppNames();
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterPackages();
    }

    private async void AppCategoryRadioButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = (RadioButton)sender;
        Enum.TryParse(btn.Content.ToString(), out _appCategory);
        await RefreshPackages();
    }

    private async void AppStatusRadioButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = (RadioButton)sender;
        Enum.TryParse(btn.Content.ToString(), out _appStatus);
        await RefreshPackages();
    }

    private async Task CacheAppNames()
    {
        GetNamesButton.IsEnabled = false;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var device = CurrentDevice;
            if (string.IsNullOrEmpty(device))
                return;

            Environment.CurrentDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty;
            await Adb.PushAapt(device);

            var appPackagePathsDict = await Adb.GetPackagesWithApkPaths(device);
            appPackagePathsDict = appPackagePathsDict
                .Where(pair => AppNames.GetAppName(pair.Key) == null)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            var total = appPackagePathsDict.Count;
            const int workerCount = 12;
            var packageDictList = SplitDictionary(appPackagePathsDict, workerCount);
            var workers = new List<BackgroundWorker>();
            var finishedCountList = new int[workerCount];

            for (var i = 0; i < workerCount; i++)
            {
                var worker = new BackgroundWorker
                {
                    WorkerReportsProgress = true,
                };

                var packageDict = packageDictList[i];
                worker.DoWork += (sender, args) =>
                {
                    var result = new Dictionary<string, Adb.AppName>();
                    var count = 0;

                    foreach (var (package, path) in packageDict)
                    {
                        var appName = Adb.GetAppName(device, path).Result;
                        count++;
                        worker.ReportProgress(count);

                        if (appName != null)
                            result.Add(package, appName);
                    }

                    args.Result = result;
                };

                var index = i;
                worker.ProgressChanged += (sender, args) =>
                {
                    finishedCountList[index] = args.ProgressPercentage;
                    StatusTextBlock.Text = $"Getting app names: {finishedCountList.Sum()} / {total}";
                };

                worker.RunWorkerCompleted += (sender, args) =>
                {
                    if (args.Error != null)
                        MessageBox.Show(args.Error.ToString());
                    else if (args.Result is Dictionary<string, Adb.AppName> result)
                        foreach (var (package, appName) in result)
                            AppNames.SetAppName(package, appName);
                };

                worker.RunWorkerAsync();
                workers.Add(worker);
            }

            while (workers.Any(worker => worker.IsBusy))
                await Task.Delay(100);

            AppNames.Save();

            stopwatch.Stop();

            StatusTextBlock.Text = $"Got {total} app names in {stopwatch.Elapsed.Seconds}s.";

            await RefreshPackages();
        }
        finally
        {
            GetNamesButton.IsEnabled = true;
        }
    }

    private static List<Dictionary<string, string>> SplitDictionary(Dictionary<string, string> originalDictionary, int numberOfPartitions)
    {
        // Calculate the approximate number of elements per partition
        var elementsPerPartition = (int)Math.Ceiling((double)originalDictionary.Count / numberOfPartitions);

        // Split the dictionary into partitions
        var partitions = new List<Dictionary<string, string>>();
        for (var i = 0; i < numberOfPartitions; i++)
        {
            var partition = originalDictionary
                .Skip(i * elementsPerPartition)
                .Take(elementsPerPartition)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            partitions.Add(partition);
        }

        return partitions;
    }
}
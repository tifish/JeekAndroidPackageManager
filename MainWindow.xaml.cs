using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JeekAndroidPackageManager;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
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
    public List<string> Packages { get; set; }
    public ObservableCollection<string> DisplayPackages { get; set; }

    private async Task RefreshDevices()
    {
        Devices.Clear();
        foreach (var device in await Adb.GetDevices())
            Devices.Add(device);
    }

    public string CurrentDevice => DeviceListBox.SelectedItem as string;
    private const char PackageNameSep = '=';

    public string CurrentPackage
    {
        get
        {
            var text = PackageListBox.SelectedItem as string;
            var sepIndex = text.IndexOf(PackageNameSep);
            return sepIndex == -1 ? text : text.Substring(0, sepIndex);
        }
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
                || appName != null && (appName.DefaultName.Contains(filter) || appName.LocalName.Contains(filter)))
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

    private Adb.AppCategory _appCategory = Adb.AppCategory.All;
    private Adb.AppStatus _appStatus = Adb.AppStatus.All;

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

        var device = CurrentDevice;
        if (string.IsNullOrEmpty(device))
            return;

        Environment.CurrentDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty;
        await Adb.PushAapt(device);

        var appPackagePathsDict = await Adb.GetPackagesWithApkPaths(device);
        var total = appPackagePathsDict.Count;
        var count = 0;
        foreach (var (package, path) in appPackagePathsDict)
        {
            count++;
            StatusTextBlock.Text = $"Getting app names: {count} / {total} {package}";

            var appName = AppNames.GetAppName(package);
            if (appName != null)
                continue;

            appName = await Adb.GetAppName(device, path);
            if (appName == null)
                continue;

            AppNames.SetAppName(package, appName);
        }

        AppNames.Save();

        StatusTextBlock.Text = "Got app names.";

        await RefreshPackages();
        GetNamesButton.IsEnabled = true;
    }
}
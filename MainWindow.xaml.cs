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
        RefreshDevices();
    }

    public ObservableCollection<string> Devices { get; set; }
    public List<string> Packages { get; set; }
    public ObservableCollection<string> DisplayPackages { get; set; }

    private void RefreshDevices()
    {
        Devices.Clear();
        foreach (var device in Adb.GetDevices())
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

    private void RefreshPackages()
    {
        var device = CurrentDevice;
        if (string.IsNullOrEmpty(device))
            return;

        Packages = Adb.GetPackages(device, _appCategory, _appStatus);
        FilterPackages();
    }

    private void FilterPackages()
    {
        DisplayPackages.Clear();
        var filter = FilterTextBox.Text;
        foreach (var package in Packages)
        {
            var appName = AppNames.GetAppName(package) ?? "";
            if (package.Contains(filter) || appName.Contains(filter))
                DisplayPackages.Add(string.IsNullOrEmpty(appName)
                    ? package
                    : $"{package}{PackageNameSep}{appName}");
        }
    }

    private void DeviceListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshPackages();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshDevices();
    }

    private void DisablePackageButton_Click(object sender, RoutedEventArgs e)
    {
        var device = CurrentDevice;
        if (string.IsNullOrEmpty(device))
            return;

        var package = CurrentPackage;
        if (string.IsNullOrEmpty(package))
            return;

        Adb.DisablePackage(device, package);
    }

    private void EnablePackageButton_Click(object sender, RoutedEventArgs e)
    {
        var device = CurrentDevice;
        if (string.IsNullOrEmpty(device))
            return;

        var package = CurrentPackage;
        if (string.IsNullOrEmpty(package))
            return;

        Adb.EnablePackage(device, package);
    }

    private void GetNamesButton_Click(object sender, RoutedEventArgs e)
    {
        CacheAppNames();
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterPackages();
    }

    private Adb.AppCategory _appCategory = Adb.AppCategory.All;
    private Adb.AppStatus _appStatus = Adb.AppStatus.All;

    private void AppCategoryRadioButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = (RadioButton)sender;
        Enum.TryParse(btn.Content.ToString(), out _appCategory);
        RefreshPackages();
    }

    private void AppStatusRadioButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = (RadioButton)sender;
        Enum.TryParse(btn.Content.ToString(), out _appStatus);
        RefreshPackages();
    }

    private async void CacheAppNames()
    {
        GetNamesButton.IsEnabled = false;

        var device = CurrentDevice;
        if (string.IsNullOrEmpty(device))
            return;

        await Task.Run(() =>
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty;
            Adb.PushAapt(device);

            var appPackagePathsDict = Adb.GetPackagesWithApkPaths(device);
            foreach (var (package, path) in appPackagePathsDict)
            {
                if (AppNames.GetAppName(package) != null)
                    continue;

                var appName = Adb.GetAppName(device, path);
                if (appName == null)
                    continue;

                AppNames.SetAppName(package, appName);
            }

            AppNames.Save();
        });

        // await new UIThreadAwaiter();
        RefreshPackages();
        GetNamesButton.IsEnabled = true;
    }
}

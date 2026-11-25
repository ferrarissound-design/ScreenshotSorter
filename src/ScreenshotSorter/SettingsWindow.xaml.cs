using System;
using System.IO;
using System.Windows;
using ScreenshotSorter.Core;

namespace ScreenshotSorter;

public partial class SettingsWindow : Window
{
    private readonly App _app;
    private readonly string _defaultFolder;

    public SettingsWindow(App app)
    {
        InitializeComponent();
        _app = app;
        _defaultFolder = KnownFolders.GetScreenshotsFolder();

        var settings = app.Settings;
        FolderTextBox.Text = string.IsNullOrWhiteSpace(settings.WatchFolder) ? _defaultFolder : settings.WatchFolder;
        SortExistingCheckBox.IsChecked = settings.SortExistingOnStartup;
        StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;

        DefaultFolderText.Text = $"既定（Windows の「スクリーンショット」フォルダ）：{_defaultFolder}\n" +
                                 "サブフォルダは監視しません。画像は同じフォルダ内の「yyyy-MM-dd」フォルダへ移動します。";
        LogPathRun.Text = $"ログ：{AppPaths.LogFile}";
        VersionText.Text = $"バージョン {AppInfo.Version}";
        Title = $"ScreenshotSorter の設定（v{AppInfo.Version}）";
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "監視フォルダを選択",
            Multiselect = false,
        };
        var current = FolderTextBox.Text.Trim();
        if (Directory.Exists(current)) dialog.InitialDirectory = current;

        if (dialog.ShowDialog(this) == true)
        {
            FolderTextBox.Text = dialog.FolderName;
        }
    }

    private void ResetFolderButton_Click(object sender, RoutedEventArgs e)
    {
        FolderTextBox.Text = _defaultFolder;
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        TrayIcon.OpenInExplorer(AppPaths.DataDirectory, create: true);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderTextBox.Text.Trim();
        if (folder.Length == 0) folder = _defaultFolder;

        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, $"フォルダが見つかりません。\n{folder}", "ScreenshotSorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var newSettings = new AppSettings
        {
            // 既定フォルダと同じなら null で保存（OneDrive 移行などでパスが変わっても追従させる）
            WatchFolder = PathEquals(folder, _defaultFolder) ? null : Path.GetFullPath(folder),
            SortExistingOnStartup = SortExistingCheckBox.IsChecked == true,
            StartWithWindows = StartWithWindowsCheckBox.IsChecked == true,
        };

        if (_app.ApplySettings(newSettings))
        {
            Close();
        }
    }

    internal static bool PathEquals(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);
}

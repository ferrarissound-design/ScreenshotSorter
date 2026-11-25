using System;
using System.IO;

namespace ScreenshotSorter.Core;

/// <summary>設定・ログの保存場所（%AppData%\ScreenshotSorter\）。</summary>
public static class AppPaths
{
    public const string AppName = "ScreenshotSorter";

    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppName);

    public static string LogFile => Path.Combine(DataDirectory, "log.txt");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
}

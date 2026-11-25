using System;
using Microsoft.Win32;

namespace ScreenshotSorter;

/// <summary>
/// 「Windows 起動時に自動で起動する」。
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run に exe のパスを登録する（管理者権限不要）。
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenshotSorter";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>
    /// 設定どおりに登録／解除する。登録済みでも exe の場所が変わっていれば書き直す。
    /// </summary>
    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        var current = key.GetValue(ValueName) as string;
        if (enabled)
        {
            if (!string.Equals(current, Command, StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(ValueName, Command, RegistryValueKind.String);
                App.Log.Info($"自動起動を登録しました: {Command}");
            }
        }
        else if (current != null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            App.Log.Info("自動起動を解除しました");
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using ScreenshotSorter.Core;

namespace ScreenshotSorter;

/// <summary>タスクトレイのアイコンと右クリックメニュー。</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly App _app;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Drawing.Icon _icon;

    public TrayIcon(App app)
    {
        _app = app;
        _icon = LoadIcon();

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(AppInfo.DisplayName) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("ログフォルダを開く", null, (_, _) => OpenInExplorer(AppPaths.DataDirectory)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("終了", null, (_, _) => _app.Quit()));

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = AppInfo.DisplayName,
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
            if (info != null)
            {
                using var stream = info.Stream;
                return new System.Drawing.Icon(stream, SystemInformation.SmallIconSize);
            }
        }
        catch (Exception ex)
        {
            App.Log.Error("アイコンの読み込みに失敗しました", ex);
        }
        return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
    }

    internal static void OpenInExplorer(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            App.Log.Error($"フォルダを開けませんでした: {path}", ex);
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}

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
    private readonly ToolStripMenuItem _pauseItem;

    public TrayIcon(App app)
    {
        _app = app;
        _icon = LoadIcon();

        _pauseItem = new ToolStripMenuItem("一時停止", null, (_, _) => _app.TogglePause());

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(AppInfo.DisplayName) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("監視フォルダを開く", null, (_, _) => OpenInExplorer(_app.GetEffectiveWatchFolder())));
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripMenuItem("設定...", null, (_, _) => _app.ShowSettings()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("ログフォルダを開く", null, (_, _) => OpenInExplorer(AppPaths.DataDirectory, create: true)));
        menu.Items.Add(new ToolStripMenuItem("終了", null, (_, _) => _app.Quit()));

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = AppInfo.DisplayName,
            ContextMenuStrip = menu,
            Visible = true,
        };
        // ダブルクリックで監視フォルダを開く
        _notifyIcon.DoubleClick += (_, _) => OpenInExplorer(_app.GetEffectiveWatchFolder());

        _app.StateChanged += UpdateState;
        UpdateState();
    }

    private void UpdateState()
    {
        var running = _app.Watcher.IsRunning;
        _pauseItem.Text = running ? "一時停止" : "再開";
        var status = running ? "監視中" : _app.IsPaused ? "一時停止中" : "停止中";
        var text = $"{AppInfo.DisplayName}（{status}）";
        // NotifyIcon.Text は 127 文字まで
        _notifyIcon.Text = text.Length > 127 ? text[..127] : text;
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

    internal static void OpenInExplorer(string path, bool create = false)
    {
        try
        {
            if (create) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            App.Log.Error($"フォルダを開けませんでした: {path}", ex);
        }
    }

    public void Dispose()
    {
        _app.StateChanged -= UpdateState;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}

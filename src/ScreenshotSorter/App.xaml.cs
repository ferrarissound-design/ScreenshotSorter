using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ScreenshotSorter.Core;

namespace ScreenshotSorter;

public partial class App : System.Windows.Application
{
    // ユーザーごとに 1 つだけ起動させる（Local\ = 同一ログオンセッション内）
    private const string MutexName = @"Local\ScreenshotSorter_SingleInstance_6E1C0F0B";

    private Mutex? _singleInstanceMutex;
    private TrayIcon? _tray;
    private int _fatalShown;

    public static ILog Log { get; } = new FileLog(AppPaths.LogFile);

    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterGlobalExceptionHandlers();

        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            System.Windows.MessageBox.Show(
                "ScreenshotSorter はすでに起動しています。\nタスクトレイのアイコンから操作してください。",
                "ScreenshotSorter",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
        Log.Info($"起動しました ({AppInfo.DisplayName})");

        SettingsStore = new SettingsStore(AppPaths.SettingsFile, Log);
        Settings = SettingsStore.Load();
        Watcher = new FolderWatcher(new FileSorter(Log), Log);

        _tray = new TrayIcon(this);

        if (StartWatching() && Settings.SortExistingOnStartup)
        {
            Watcher.EnqueueExistingFiles();
        }
    }

    public SettingsStore SettingsStore { get; private set; } = null!;

    public AppSettings Settings { get; private set; } = new();

    public FolderWatcher Watcher { get; private set; } = null!;

    public bool IsPaused { get; private set; }

    /// <summary>一時停止・監視フォルダなどの状態が変わった（トレイ表示の更新用）。</summary>
    public event Action? StateChanged;

    /// <summary>実際に監視するフォルダ（設定が空なら Windows の「スクリーンショット」フォルダ）。</summary>
    public string GetEffectiveWatchFolder() =>
        string.IsNullOrWhiteSpace(Settings.WatchFolder)
            ? KnownFolders.GetScreenshotsFolder()
            : Settings.WatchFolder;

    /// <summary>監視を（再）開始する。失敗したらユーザーに知らせて false。</summary>
    public bool StartWatching()
    {
        var folder = GetEffectiveWatchFolder();
        try
        {
            Watcher.Start(folder);
            IsPaused = false;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"監視を開始できませんでした: {folder}", ex);
            Watcher.Stop();
            System.Windows.MessageBox.Show(
                $"監視フォルダを監視できませんでした。\n{folder}\n\n{ex.Message}\n\n「設定」から監視フォルダを変更してください。",
                "ScreenshotSorter",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
        finally
        {
            StateChanged?.Invoke();
        }
    }

    public void TogglePause()
    {
        if (IsPaused || !Watcher.IsRunning)
        {
            Log.Info("再開しました");
            StartWatching();
        }
        else
        {
            Watcher.Stop();
            IsPaused = true;
            Log.Info("一時停止しました");
            StateChanged?.Invoke();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        Watcher?.Dispose();
        if (_singleInstanceMutex != null)
        {
            Log.Info("終了しました");
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }
        base.OnExit(e);
    }

    public void Quit() => Shutdown(0);

    private void RegisterGlobalExceptionHandlers()
    {
        // UI スレッドの未処理例外
        DispatcherUnhandledException += (_, args) =>
        {
            ReportFatal("UI スレッドで未処理の例外が発生しました", args.Exception);
            args.Handled = true;
            Shutdown(1);
        };

        // それ以外のスレッドの未処理例外（この後プロセスは終了する）
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            ReportFatal("未処理の例外が発生しました", args.ExceptionObject as Exception);
        };

        // await されなかった Task の例外（落とさずログだけ残す）
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("未監視のタスク例外", args.Exception);
            args.SetObserved();
        };
    }

    private void ReportFatal(string message, Exception? ex)
    {
        Log.Error(message, ex);
        if (Interlocked.Exchange(ref _fatalShown, 1) != 0) return;
        try
        {
            System.Windows.MessageBox.Show(
                $"予期しないエラーが発生したため、ScreenshotSorter を終了します。\n\n" +
                $"{ex?.Message}\n\n" +
                $"詳細はログファイルを確認してください：\n{AppPaths.LogFile}",
                "ScreenshotSorter - エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // メッセージボックスすら出せない状況では諦める
        }
    }
}

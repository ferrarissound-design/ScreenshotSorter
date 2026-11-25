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

        _tray = new TrayIcon(this);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
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

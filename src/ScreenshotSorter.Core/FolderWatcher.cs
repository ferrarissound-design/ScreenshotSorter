using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ScreenshotSorter.Core;

/// <summary>
/// 監視フォルダ直下（サブフォルダは見ない）に増えた画像を検知し、
/// 1 本のワーカーで順番に FileSorter へ渡す。
/// </summary>
public sealed class FolderWatcher : IDisposable
{
    private readonly FileSorter _sorter;
    private readonly ILog _log;
    private readonly TimeSpan _settleDelay;
    private readonly Channel<WorkItem> _queue = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions { SingleReader = true });
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Task _worker;

    private FileSystemWatcher? _watcher;
    private int _generation;

    /// <param name="settleDelay">検知してから移動を試みるまでの待ち時間（書き込み完了待ち）</param>
    public FolderWatcher(FileSorter sorter, ILog log, TimeSpan? settleDelay = null)
    {
        _sorter = sorter;
        _log = log;
        _settleDelay = settleDelay ?? TimeSpan.FromMilliseconds(800);
        _worker = Task.Run(WorkerLoopAsync);
    }

    public string? Folder { get; private set; }

    public bool IsRunning
    {
        get { lock (_gate) return _watcher != null; }
    }

    /// <summary>監視を開始する（既に監視中なら止めてから切り替える）。</summary>
    public void Start(string folder)
    {
        // "C:\Pics\" と "C:\Pics" を同一視する（ルート "C:\" はそのまま）
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"監視フォルダが見つかりません: {full}");

        lock (_gate)
        {
            StopCore();
            var watcher = new FileSystemWatcher(full)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName,
                InternalBufferSize = 64 * 1024,
            };
            watcher.Created += (_, e) => Enqueue(e.FullPath);
            watcher.Renamed += (_, e) => Enqueue(e.FullPath);
            watcher.Error += OnWatcherError;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
            Folder = full;
        }
        _log.Info($"監視を開始しました: {full}");
    }

    /// <summary>監視を止め、まだ処理していないキューも破棄する。</summary>
    public void Stop()
    {
        bool wasRunning;
        lock (_gate)
        {
            wasRunning = _watcher != null;
            StopCore();
        }
        if (wasRunning) _log.Info("監視を停止しました");
    }

    /// <summary>監視フォルダ直下に既にある画像をすべてキューに入れる。件数を返す。</summary>
    public int EnqueueExistingFiles()
    {
        string? folder;
        lock (_gate) folder = _watcher != null ? Folder : null;
        if (folder == null) return 0;

        var count = 0;
        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                if (!SortingRules.IsTargetFile(path)) continue;
                if (Enqueue(path, immediate: true)) count++;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error($"既存ファイルの列挙に失敗しました: {folder}", ex);
        }
        _log.Info($"既存の画像 {count} 件を整理対象にしました: {folder}");
        return count;
    }

    private bool Enqueue(string path, bool immediate = false)
    {
        if (!SortingRules.IsTargetFile(path)) return false;
        lock (_gate)
        {
            if (_watcher == null) return false;
            // 監視フォルダ直下のファイルだけ（念のため）
            if (!string.Equals(Path.GetDirectoryName(path), Folder, StringComparison.OrdinalIgnoreCase)) return false;
            if (!_pending.Add(path)) return false;
            var notBefore = immediate ? DateTime.UtcNow : DateTime.UtcNow + _settleDelay;
            return _queue.Writer.TryWrite(new WorkItem(path, notBefore, _generation));
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        _log.Error("フォルダ監視でエラーが発生しました。監視を再開します", ex);

        string? folder;
        lock (_gate)
        {
            if (!ReferenceEquals(sender, _watcher)) return; // 既に停止・切替済み
            folder = Folder;
        }
        if (folder == null) return;

        try
        {
            Start(folder);
            // バッファあふれ等で取りこぼした可能性があるので、直下を拾い直す
            EnqueueExistingFiles();
        }
        catch (Exception restartEx)
        {
            _log.Error($"監視の再開に失敗しました: {folder}", restartEx);
            lock (_gate) StopCore();
        }
    }

    private void StopCore()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        _pending.Clear();
        _generation++;
    }

    private async Task WorkerLoopAsync()
    {
        var ct = _disposeCts.Token;
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                lock (_gate)
                {
                    if (item.Generation != _generation) continue; // 停止前に積まれたもの
                }

                var wait = item.NotBefore - DateTime.UtcNow;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct).ConfigureAwait(false);

                lock (_gate)
                {
                    if (item.Generation != _generation) continue;
                    _pending.Remove(item.Path);
                }

                try
                {
                    _sorter.Sort(item.Path, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // 1 ファイルの失敗で監視全体を止めない
                    _log.Error($"整理中に予期しないエラーが発生しました: {item.Path}", ex);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        lock (_gate) StopCore();
        _queue.Writer.TryComplete();
        _disposeCts.Cancel();
        try { _worker.Wait(TimeSpan.FromSeconds(5)); } catch { }
        _disposeCts.Dispose();
    }

    private readonly record struct WorkItem(string Path, DateTime NotBefore, int Generation);
}

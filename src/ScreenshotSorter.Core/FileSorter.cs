using System;
using System.IO;
using System.Threading;

namespace ScreenshotSorter.Core;

public enum SortOutcome
{
    /// <summary>日付フォルダへ移動した。</summary>
    Moved,
    /// <summary>対象外・既に存在しない等で何もしなかった。</summary>
    Skipped,
    /// <summary>リトライしても移動できなかった。</summary>
    Failed,
}

public sealed record SortResult(SortOutcome Outcome, string Source, string? Destination = null, string? Reason = null);

/// <summary>
/// 1 ファイルを「同じフォルダ内の yyyy-MM-dd フォルダ」へ移動する。
/// 書き込み中（ロック中）ならリトライする。
/// </summary>
public sealed class FileSorter
{
    private readonly ILog _log;
    private readonly Action<TimeSpan> _sleep;
    private readonly Func<string, DateTime> _getCreationTime;

    public FileSorter(
        ILog log,
        int maxAttempts = 6,
        TimeSpan? retryDelay = null,
        Action<TimeSpan>? sleep = null,
        Func<string, DateTime>? getCreationTime = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        _log = log;
        MaxAttempts = maxAttempts;
        RetryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        _sleep = sleep ?? Thread.Sleep;
        _getCreationTime = getCreationTime ?? File.GetCreationTime;
    }

    public int MaxAttempts { get; }

    /// <summary>1 回目の待ち時間。n 回目の失敗後は RetryDelay × n 待つ。</summary>
    public TimeSpan RetryDelay { get; }

    public SortResult Sort(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!SortingRules.IsTargetFile(sourcePath))
            return new SortResult(SortOutcome.Skipped, sourcePath, Reason: "対象外の拡張子");

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(sourcePath))
                return new SortResult(SortOutcome.Skipped, sourcePath, Reason: "ファイルが存在しません");

            try
            {
                var destination = MoveOnce(sourcePath);
                _log.Info($"移動: {sourcePath} → {destination}");
                return new SortResult(SortOutcome.Moved, sourcePath, destination);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 途中で消された・リネームされた場合は失敗扱いにしない
                if (!File.Exists(sourcePath))
                    return new SortResult(SortOutcome.Skipped, sourcePath, Reason: "ファイルが存在しません");

                if (attempt >= MaxAttempts)
                {
                    _log.Error($"移動に失敗しました（{MaxAttempts} 回試行）: {sourcePath}", ex);
                    return new SortResult(SortOutcome.Failed, sourcePath, Reason: ex.Message);
                }

                _sleep(RetryDelay * attempt);
            }
        }
    }

    private string MoveOnce(string sourcePath)
    {
        // 書き込み中なら排他オープンに失敗する（IOException）→ 呼び出し側でリトライ
        using (new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
        }

        var sourceDir = Path.GetDirectoryName(Path.GetFullPath(sourcePath))
            ?? throw new IOException($"フォルダを特定できません: {sourcePath}");
        var created = _getCreationTime(sourcePath);
        var destDir = Path.Combine(sourceDir, SortingRules.GetDateFolderName(created));
        Directory.CreateDirectory(destDir);

        var destination = SortingRules.GetUniqueDestinationPath(destDir, Path.GetFileName(sourcePath));
        // overwrite: false — 直前に同名ができていたら IOException になり、次の試行で別の連番を選ぶ
        File.Move(sourcePath, destination, overwrite: false);
        return destination;
    }
}

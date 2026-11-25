using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ScreenshotSorter.Core;

public interface ILog
{
    void Info(string message);
    void Error(string message, Exception? exception = null);
}

/// <summary>
/// log.txt への追記ログ。複数スレッドから呼ばれても安全。
/// 1MB を超えたら log.old.txt にローテーションする。
/// ログ書き込み自体の失敗でアプリを落とさないよう、例外は握りつぶす。
/// </summary>
public sealed class FileLog : ILog
{
    private const long MaxBytes = 1024 * 1024;
    private readonly object _gate = new();

    public FileLog(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public void Info(string message) => Write("INFO", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var sb = new StringBuilder();
        sb.Append('[')
          .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
          .Append("] [").Append(level).Append("] ")
          .Append(message);
        if (exception != null)
        {
            sb.AppendLine();
            // ToString() は型・メッセージ・InnerException・スタックトレースを全部含む
            sb.Append(exception.ToString());
        }
        sb.AppendLine();

        lock (_gate)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                RotateIfNeeded();
                File.AppendAllText(Path, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
                // ログが書けない場合でも本体の動作は止めない
            }
        }
    }

    private void RotateIfNeeded()
    {
        var info = new FileInfo(Path);
        if (!info.Exists || info.Length < MaxBytes) return;
        var old = System.IO.Path.ChangeExtension(Path, ".old.txt");
        File.Move(Path, old, overwrite: true);
    }
}

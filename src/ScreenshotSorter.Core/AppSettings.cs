using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ScreenshotSorter.Core;

public sealed class AppSettings
{
    /// <summary>監視フォルダ。null/空なら Windows の「スクリーンショット」フォルダを使う。</summary>
    public string? WatchFolder { get; set; }

    /// <summary>起動時に監視フォルダ直下の既存画像もまとめて整理する。</summary>
    public bool SortExistingOnStartup { get; set; }

    /// <summary>Windows 起動時に自動で起動する。</summary>
    public bool StartWithWindows { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

/// <summary>settings.json の読み書き（%AppData%\ScreenshotSorter\settings.json）。</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ILog _log;

    public SettingsStore(string path, ILog log)
    {
        Path = path;
        _log = log;
    }

    public string Path { get; }

    /// <summary>読み込む。ファイルが無い・壊れている場合は既定値を返す（壊れていたらログに残す）。</summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new AppSettings();
            var json = File.ReadAllText(Path, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _log.Error($"設定ファイルを読み込めなかったため既定値を使います: {Path}", ex);
            return new AppSettings();
        }
    }

    /// <summary>保存する。一時ファイルに書いてから置き換えるので、途中で落ちても壊れにくい。</summary>
    public void Save(AppSettings settings)
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOptions), new UTF8Encoding(false));
        File.Move(tmp, Path, overwrite: true);
    }
}

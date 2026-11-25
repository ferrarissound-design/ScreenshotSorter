using System;
using System.Globalization;
using System.IO;

namespace ScreenshotSorter.Core;

/// <summary>ファイル移動のルール（純粋関数のみ。単体テスト対象）。</summary>
public static class SortingRules
{
    /// <summary>対象拡張子（大文字小文字は区別しない）。</summary>
    public static readonly string[] TargetExtensions = [".png", ".jpg", ".jpeg"];

    public const string DateFolderFormat = "yyyy-MM-dd";

    /// <summary>整理対象の画像ファイルかどうか。</summary>
    public static bool IsTargetFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext)) return false;
        foreach (var target in TargetExtensions)
        {
            if (string.Equals(ext, target, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>日付フォルダ名（例: 2025-01-31）。カルチャに依存しない。</summary>
    public static string GetDateFolderName(DateTime date) =>
        date.ToString(DateFolderFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// 移動先に同名ファイルがあれば「名前 (2).png」「名前 (3).png」…と連番を付けた、
    /// まだ存在しないパスを返す。
    /// </summary>
    /// <param name="directory">移動先フォルダ</param>
    /// <param name="fileName">元のファイル名</param>
    /// <param name="exists">パスが既に存在するか（テスト用に差し替え可能）</param>
    public static string GetUniqueDestinationPath(string directory, string fileName, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(exists);

        var candidate = Path.Combine(directory, fileName);
        if (!exists(candidate)) return candidate;

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var n = 2; n < 100_000; n++)
        {
            candidate = Path.Combine(directory, $"{baseName} ({n}){ext}");
            if (!exists(candidate)) return candidate;
        }
        throw new IOException($"空いているファイル名が見つかりません: {Path.Combine(directory, fileName)}");
    }

    /// <summary>実ファイルシステムで判定する版。</summary>
    public static string GetUniqueDestinationPath(string directory, string fileName) =>
        GetUniqueDestinationPath(directory, fileName, p => File.Exists(p) || Directory.Exists(p));
}

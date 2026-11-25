using System;
using System.IO;
using ScreenshotSorter.Core;
using Xunit;

namespace ScreenshotSorter.Tests;

public class FileLogTests
{
    [Fact]
    public void Error_WritesMessageAndStackTrace()
    {
        using var dir = new TempDir();
        var log = new FileLog(dir.Combine("sub", "log.txt"));

        try { throw new InvalidOperationException("boom"); }
        catch (Exception ex) { log.Error("失敗しました", ex); }

        var text = File.ReadAllText(log.Path);
        Assert.Contains("[ERROR] 失敗しました", text);
        Assert.Contains("InvalidOperationException: boom", text);
        Assert.Contains(nameof(Error_WritesMessageAndStackTrace), text); // スタックトレース
    }

    [Fact]
    public void Info_AppendsLines()
    {
        using var dir = new TempDir();
        var log = new FileLog(dir.Combine("log.txt"));

        log.Info("one");
        log.Info("two");

        var lines = File.ReadAllLines(log.Path);
        Assert.Equal(2, lines.Length);
        Assert.Matches(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\] \[INFO\] one$", lines[0]);
        Assert.EndsWith("[INFO] two", lines[1]);
    }
}

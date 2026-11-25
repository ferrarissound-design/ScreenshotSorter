using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using ScreenshotSorter.Core;
using Xunit;

namespace ScreenshotSorter.Tests;

public class SortingRulesTests
{
    [Theory]
    [InlineData("a.png", true)]
    [InlineData("a.PNG", true)]
    [InlineData("a.jpg", true)]
    [InlineData("a.JPG", true)]
    [InlineData("a.jpeg", true)]
    [InlineData("スクリーンショット 2025-01-31 123456.png", true)]
    [InlineData("a.gif", false)]
    [InlineData("a.png.tmp", false)]
    [InlineData("a.txt", false)]
    [InlineData("png", false)]
    [InlineData("a.", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsTargetFile(string path, bool expected)
    {
        Assert.Equal(expected, SortingRules.IsTargetFile(path));
    }

    [Fact]
    public void IsTargetFile_FullPath()
    {
        Assert.True(SortingRules.IsTargetFile(Path.Combine("dir.with.dots", "shot.png")));
        Assert.False(SortingRules.IsTargetFile(Path.Combine("dir.png", "shot")));
    }

    [Theory]
    [InlineData(2025, 1, 5, "2025-01-05")]
    [InlineData(2025, 12, 31, "2025-12-31")]
    [InlineData(2024, 2, 29, "2024-02-29")]
    public void GetDateFolderName(int y, int m, int d, string expected)
    {
        Assert.Equal(expected, SortingRules.GetDateFolderName(new DateTime(y, m, d, 23, 59, 59)));
    }

    [Theory]
    [InlineData("ja-JP")]
    [InlineData("ar-SA")] // ヒジュラ暦の既定カレンダー
    [InlineData("th-TH")] // 仏暦
    public void GetDateFolderName_IsCultureInvariant(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Assert.Equal("2025-03-04", SortingRules.GetDateFolderName(new DateTime(2025, 3, 4)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void GetUniqueDestinationPath_NoConflict_ReturnsOriginalName()
    {
        var result = SortingRules.GetUniqueDestinationPath("dir", "shot.png", _ => false);
        Assert.Equal(Path.Combine("dir", "shot.png"), result);
    }

    [Fact]
    public void GetUniqueDestinationPath_Conflict_AppendsNumberFrom2()
    {
        var existing = new HashSet<string> { Path.Combine("dir", "shot.png") };
        var result = SortingRules.GetUniqueDestinationPath("dir", "shot.png", existing.Contains);
        Assert.Equal(Path.Combine("dir", "shot (2).png"), result);
    }

    [Fact]
    public void GetUniqueDestinationPath_MultipleConflicts_UsesNextFreeNumber()
    {
        var existing = new HashSet<string>
        {
            Path.Combine("dir", "shot.png"),
            Path.Combine("dir", "shot (2).png"),
            Path.Combine("dir", "shot (3).png"),
        };
        var result = SortingRules.GetUniqueDestinationPath("dir", "shot.png", existing.Contains);
        Assert.Equal(Path.Combine("dir", "shot (4).png"), result);
    }

    [Fact]
    public void GetUniqueDestinationPath_KeepsOnlyLastExtension()
    {
        var existing = new HashSet<string> { Path.Combine("dir", "my.shot.jpg") };
        var result = SortingRules.GetUniqueDestinationPath("dir", "my.shot.jpg", existing.Contains);
        Assert.Equal(Path.Combine("dir", "my.shot (2).jpg"), result);
    }

    [Fact]
    public void GetUniqueDestinationPath_RealFileSystem()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.Combine("a.png"), "x");
        File.WriteAllText(dir.Combine("a (2).png"), "x");
        Assert.Equal(dir.Combine("a (3).png"), SortingRules.GetUniqueDestinationPath(dir.Path, "a.png"));
    }
}

public class FileSorterTests
{
    private sealed class ListLog : ILog
    {
        public List<string> Infos { get; } = new();
        public List<string> Errors { get; } = new();
        public void Info(string message) => Infos.Add(message);
        public void Error(string message, Exception? exception = null) => Errors.Add(message);
    }

    private static readonly DateTime Created = new(2025, 1, 31, 10, 0, 0);

    [Fact]
    public void Sort_MovesIntoDateFolderInSameDirectory_AndLogs()
    {
        using var dir = new TempDir();
        var src = dir.Combine("shot.png");
        File.WriteAllText(src, "img");
        var log = new ListLog();
        var sorter = new FileSorter(log, getCreationTime: _ => Created);

        var result = sorter.Sort(src);

        var expected = dir.Combine("2025-01-31", "shot.png");
        Assert.Equal(SortOutcome.Moved, result.Outcome);
        Assert.Equal(expected, result.Destination);
        Assert.False(File.Exists(src));
        Assert.Equal("img", File.ReadAllText(expected));
        Assert.Contains(log.Infos, m => m.Contains(src) && m.Contains("→") && m.Contains(expected));
    }

    [Fact]
    public void Sort_DoesNotOverwrite_AddsNumber()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("2025-01-31"));
        File.WriteAllText(dir.Combine("2025-01-31", "shot.png"), "old");
        File.WriteAllText(dir.Combine("shot.png"), "new");
        var sorter = new FileSorter(new ListLog(), getCreationTime: _ => Created);

        var result = sorter.Sort(dir.Combine("shot.png"));

        Assert.Equal(dir.Combine("2025-01-31", "shot (2).png"), result.Destination);
        Assert.Equal("old", File.ReadAllText(dir.Combine("2025-01-31", "shot.png")));
        Assert.Equal("new", File.ReadAllText(dir.Combine("2025-01-31", "shot (2).png")));
    }

    [Fact]
    public void Sort_UsesCreationTime()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.Combine("a.jpg"), "x");
        var sorter = new FileSorter(new ListLog(), getCreationTime: _ => new DateTime(2023, 7, 8, 0, 0, 1));

        var result = sorter.Sort(dir.Combine("a.jpg"));

        Assert.Equal(dir.Combine("2023-07-08", "a.jpg"), result.Destination);
    }

    [Fact]
    public void Sort_NonTarget_IsSkippedAndUntouched()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.Combine("memo.txt"), "x");
        var sorter = new FileSorter(new ListLog(), getCreationTime: _ => Created);

        var result = sorter.Sort(dir.Combine("memo.txt"));

        Assert.Equal(SortOutcome.Skipped, result.Outcome);
        Assert.True(File.Exists(dir.Combine("memo.txt")));
    }

    [Fact]
    public void Sort_MissingFile_IsSkipped()
    {
        using var dir = new TempDir();
        var sorter = new FileSorter(new ListLog(), getCreationTime: _ => Created);
        Assert.Equal(SortOutcome.Skipped, sorter.Sort(dir.Combine("gone.png")).Outcome);
    }

    [Fact]
    public void Sort_LockedFile_RetriesUntilUnlocked()
    {
        using var dir = new TempDir();
        var src = dir.Combine("writing.png");
        File.WriteAllText(src, "x");
        var lockStream = new FileStream(src, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var sleeps = new List<TimeSpan>();
        var sorter = new FileSorter(new ListLog(), maxAttempts: 5, retryDelay: TimeSpan.FromMilliseconds(10),
            sleep: d =>
            {
                sleeps.Add(d);
                if (sleeps.Count == 2) lockStream.Dispose(); // 2 回待ったら書き込み完了
            },
            getCreationTime: _ => Created);

        try
        {
            var result = sorter.Sort(src);

            Assert.Equal(SortOutcome.Moved, result.Outcome);
            Assert.Equal(new[] { TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20) }, sleeps);
        }
        finally
        {
            lockStream.Dispose();
        }
    }

    [Fact]
    public void Sort_LockedFile_GivesUpAfterMaxAttempts_AndLogsError()
    {
        using var dir = new TempDir();
        var src = dir.Combine("locked.png");
        File.WriteAllText(src, "x");
        using var lockStream = new FileStream(src, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var log = new ListLog();
        var sleepCount = 0;
        var sorter = new FileSorter(log, maxAttempts: 3, sleep: _ => sleepCount++, getCreationTime: _ => Created);

        var result = sorter.Sort(src);

        Assert.Equal(SortOutcome.Failed, result.Outcome);
        Assert.Equal(2, sleepCount); // 3 回試して、間に 2 回待つ
        Assert.True(File.Exists(src));
        Assert.Single(log.Errors);
    }
}

public class FolderWatcherTests
{
    private sealed class NullLog : ILog
    {
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }

    private static readonly DateTime Created = new(2025, 5, 6);

    private static bool WaitUntil(Func<bool> condition, int timeoutMs = 10_000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition()) return true;
            Thread.Sleep(50);
        }
        return condition();
    }

    [Fact]
    public void NewFile_IsMoved_SubfolderFile_IsIgnored()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("sub"));
        var log = new NullLog();
        using var watcher = new FolderWatcher(new FileSorter(log, getCreationTime: _ => Created), log, TimeSpan.FromMilliseconds(50));
        watcher.Start(dir.Path);

        File.WriteAllText(dir.Combine("sub", "inner.png"), "x");
        File.WriteAllText(dir.Combine("new.png"), "x");
        File.WriteAllText(dir.Combine("note.txt"), "x");

        Assert.True(WaitUntil(() => File.Exists(dir.Combine("2025-05-06", "new.png"))));
        Thread.Sleep(300);
        Assert.True(File.Exists(dir.Combine("sub", "inner.png")));
        Assert.True(File.Exists(dir.Combine("note.txt")));
        Assert.False(Directory.Exists(dir.Combine("sub", "2025-05-06")));
    }

    [Fact]
    public void ExistingFiles_AreMovedOnlyWhenRequested()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.Combine("old1.png"), "x");
        File.WriteAllText(dir.Combine("old2.jpg"), "x");
        var log = new NullLog();
        using var watcher = new FolderWatcher(new FileSorter(log, getCreationTime: _ => Created), log, TimeSpan.FromMilliseconds(50));
        watcher.Start(dir.Path);

        Thread.Sleep(300);
        Assert.True(File.Exists(dir.Combine("old1.png"))); // 何もしなければ既存ファイルはそのまま

        Assert.Equal(2, watcher.EnqueueExistingFiles());
        Assert.True(WaitUntil(() =>
            File.Exists(dir.Combine("2025-05-06", "old1.png")) && File.Exists(dir.Combine("2025-05-06", "old2.jpg"))));
    }

    [Fact]
    public void Stopped_DoesNotMove()
    {
        using var dir = new TempDir();
        var log = new NullLog();
        using var watcher = new FolderWatcher(new FileSorter(log, getCreationTime: _ => Created), log, TimeSpan.FromMilliseconds(50));
        watcher.Start(dir.Path);
        watcher.Stop();
        Assert.False(watcher.IsRunning);

        File.WriteAllText(dir.Combine("paused.png"), "x");
        Thread.Sleep(500);

        Assert.True(File.Exists(dir.Combine("paused.png")));
    }

    [Fact]
    public void Start_MissingFolder_Throws()
    {
        using var dir = new TempDir();
        var log = new NullLog();
        using var watcher = new FolderWatcher(new FileSorter(log), log);
        Assert.Throws<DirectoryNotFoundException>(() => watcher.Start(dir.Combine("nope")));
        Assert.False(watcher.IsRunning);
    }
}

public class SettingsStoreTests
{
    private sealed class ListLog : ILog
    {
        public int ErrorCount;
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) => ErrorCount++;
    }

    [Fact]
    public void Missing_ReturnsDefaults()
    {
        using var dir = new TempDir();
        var s = new SettingsStore(dir.Combine("settings.json"), new ListLog()).Load();
        Assert.Null(s.WatchFolder);
        Assert.False(s.SortExistingOnStartup); // 既定 OFF
        Assert.False(s.StartWithWindows);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(dir.Combine("nested", "settings.json"), new ListLog());
        store.Save(new AppSettings { WatchFolder = @"D:\写真\スクショ", SortExistingOnStartup = true, StartWithWindows = true });

        var s = store.Load();
        Assert.Equal(@"D:\写真\スクショ", s.WatchFolder);
        Assert.True(s.SortExistingOnStartup);
        Assert.True(s.StartWithWindows);
    }

    [Fact]
    public void Corrupt_ReturnsDefaults_AndLogs()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.Combine("settings.json"), "{ not json");
        var log = new ListLog();
        var s = new SettingsStore(dir.Combine("settings.json"), log).Load();
        Assert.Null(s.WatchFolder);
        Assert.Equal(1, log.ErrorCount);
    }
}

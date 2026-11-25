using System.Reflection;

namespace ScreenshotSorter;

internal static class AppInfo
{
    /// <summary>表示用バージョン（例: "1.0.0"）。CI でタグから -p:Version= を渡して決まる。</summary>
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public static string DisplayName => $"ScreenshotSorter v{Version}";
}

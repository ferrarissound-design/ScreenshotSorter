using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ScreenshotSorter;

/// <summary>
/// Known Folder API で Windows の「スクリーンショット」フォルダの実パスを取る。
/// OneDrive へのリダイレクトや、エクスプローラー上の日本語表示名（「スクリーンショット」）でも
/// 実際のパスが返る。
/// </summary>
internal static class KnownFolders
{
    // FOLDERID_Screenshots
    private static readonly Guid ScreenshotsId = new("B7BEDE81-DF94-4682-A7D8-57A52620B86F");

    private const uint KF_FLAG_CREATE = 0x00008000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(in Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    public static string GetScreenshotsFolder()
    {
        IntPtr ptr = IntPtr.Zero;
        try
        {
            // KF_FLAG_CREATE: まだ一度もスクショを撮っていなくてフォルダが無い場合は作る
            var hr = SHGetKnownFolderPath(ScreenshotsId, KF_FLAG_CREATE, IntPtr.Zero, out ptr);
            if (hr == 0 && ptr != IntPtr.Zero)
            {
                var path = Marshal.PtrToStringUni(ptr);
                if (!string.IsNullOrEmpty(path)) return path;
            }
            App.Log.Error($"SHGetKnownFolderPath(FOLDERID_Screenshots) が失敗しました (HRESULT=0x{hr:X8})。ピクチャ\\Screenshots を使います");
        }
        catch (Exception ex)
        {
            App.Log.Error("スクリーンショットフォルダの取得に失敗しました。ピクチャ\\Screenshots を使います", ex);
        }
        finally
        {
            if (ptr != IntPtr.Zero) Marshal.FreeCoTaskMem(ptr);
        }

        // フォールバック：ピクチャ（こちらも Known Folder 経由なのでリダイレクト対応）\Screenshots
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
        Directory.CreateDirectory(fallback);
        return fallback;
    }
}

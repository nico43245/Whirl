using System.Diagnostics;
using Whirl.Core;
using static Whirl.Native;

namespace Whirl;

/// <summary>
/// Lists the windows that would appear in Alt+Tab, top of the z-order first. Only titles,
/// process ids and executable paths are read — never window contents.
/// </summary>
internal static class WindowEnumerator
{
    private static readonly Dictionary<string, string> NameCache = new(StringComparer.OrdinalIgnoreCase);

    public static List<WindowInfo> Enumerate()
    {
        var found = new List<WindowInfo>();
        var self = (uint)Environment.ProcessId;
        EnumWindows((hwnd, _) =>
        {
            if (Describe(hwnd, self) is { } info) found.Add(info);
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static WindowInfo? Describe(IntPtr hwnd, uint selfPid)
    {
        if (!IsWindowVisible(hwnd)) return null;
        var ex = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        var isAppWindow = (ex & WS_EX_APPWINDOW) != 0;
        if ((ex & WS_EX_TOOLWINDOW) != 0 && !isAppWindow) return null;
        if (GetWindow(hwnd, GW_OWNER) != IntPtr.Zero && !isAppWindow) return null;
        // Cloaked windows are suspended Store apps and windows on other virtual desktops.
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return null;

        var title = WindowTitle(hwnd);
        if (title.Length == 0) return null;
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == selfPid) return null;

        var exe = ExePath(pid) ?? "";
        var name = AppName(exe, pid);
        var key = exe;
        if (exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            // Every Store app is hosted by this one executable; tell them apart by title.
            key = "uwp:" + title;
            name = title;
        }
        else if (key.Length == 0) key = "pid:" + pid;

        return new WindowInfo(hwnd.ToInt64(), (int)pid, title, exe, name, key, IsIconic(hwnd));
    }

    private static string AppName(string exe, uint pid)
    {
        if (exe.Length == 0)
        {
            try { return Process.GetProcessById((int)pid).ProcessName; } catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return "App"; }
        }
        if (NameCache.TryGetValue(exe, out var cached)) return cached;
        string name;
        try
        {
            var description = FileVersionInfo.GetVersionInfo(exe).FileDescription;
            name = string.IsNullOrWhiteSpace(description) ? Path.GetFileNameWithoutExtension(exe) : description.Trim();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { name = Path.GetFileNameWithoutExtension(exe); }
        NameCache[exe] = name;
        return name;
    }
}

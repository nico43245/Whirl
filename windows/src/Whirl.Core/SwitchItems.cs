namespace Whirl.Core;

/// <summary>A top-level window as the enumerator sees it. <c>AppKey</c> groups windows into apps.</summary>
public sealed record WindowInfo(long Hwnd, int Pid, string Title, string ExePath, string AppName, string AppKey, bool IsMinimized);

/// <summary>An app the user pinned to the wheel, identified by its executable.</summary>
public sealed record PinnedApp(string ExePath, string Name);

/// <summary>One thing the wheel can switch to: an app, or in window mode one window.</summary>
public sealed record SwitchItem(string Id, string Title, string? Subtitle, string? ExePath, long? Hwnd, bool IsPinned, bool IsForeground)
{
    /// <summary>False for a pinned app that is not running; choosing it launches it.</summary>
    public bool IsRunning => Hwnd is not null;
}

public static class ItemOrdering
{
    private static bool SameKey(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// One item per app. Pinned apps come first in the order they were pinned (running or not),
    /// then every other app by how recently it was in front. <paramref name="windows"/> must be in z-order, top first.
    /// </summary>
    public static IReadOnlyList<SwitchItem> AppItems(IReadOnlyList<WindowInfo> windows, IReadOnlyList<PinnedApp> pinned, long foreground)
    {
        var groups = windows.GroupBy(w => w.AppKey, StringComparer.OrdinalIgnoreCase).ToList();   // keeps first-seen order
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<SwitchItem>();

        foreach (var pin in pinned)
        {
            var group = groups.FirstOrDefault(g => SameKey(g.Key, pin.ExePath));
            if (group is null)
            {
                items.Add(new SwitchItem(pin.ExePath, pin.Name, null, pin.ExePath, null, true, false));
                continue;
            }
            used.Add(group.Key);
            items.Add(ForGroup(group, pin.Name, isPinned: true, foreground));
        }
        foreach (var group in groups.Where(g => !used.Contains(g.Key)))
            items.Add(ForGroup(group, group.First().AppName, isPinned: false, foreground));
        return items;
    }

    private static SwitchItem ForGroup(IEnumerable<WindowInfo> group, string title, bool isPinned, long foreground)
    {
        var top = group.First();
        return new SwitchItem(top.AppKey, string.IsNullOrWhiteSpace(top.AppName) ? title : top.AppName, null, top.ExePath,
                              top.Hwnd, isPinned, group.Any(w => w.Hwnd == foreground));
    }

    /// <summary>One item per window, in z-order. Pins are not used in this mode.</summary>
    public static IReadOnlyList<SwitchItem> WindowItems(IReadOnlyList<WindowInfo> windows, long foreground) =>
        windows.Select(w => new SwitchItem(w.Hwnd.ToString(), string.IsNullOrWhiteSpace(w.Title) ? w.AppName : w.Title,
                                           w.AppName, w.ExePath, w.Hwnd, false, w.Hwnd == foreground)).ToList();

    /// <summary>
    /// The window a quick tap should jump to: the topmost window of the app that was in front
    /// before the current one.
    /// </summary>
    public static WindowInfo? PreviousApp(IReadOnlyList<WindowInfo> windows, long foreground)
    {
        var current = windows.FirstOrDefault(w => w.Hwnd == foreground)?.AppKey ?? windows.FirstOrDefault()?.AppKey;
        return windows.FirstOrDefault(w => current is not null && !SameKey(w.AppKey, current));
    }
}

/// <summary>Splits items into pages of eight.</summary>
public static class Pager
{
    public static int PageCount(int itemCount) => Math.Max(1, (itemCount + WheelGeometry.SliceCount - 1) / WheelGeometry.SliceCount);

    public static SwitchItem?[] Page(IReadOnlyList<SwitchItem> items, int index)
    {
        var page = new SwitchItem?[WheelGeometry.SliceCount];
        for (var slot = 0; slot < page.Length; slot++)
        {
            var i = index * WheelGeometry.SliceCount + slot;
            page[slot] = i < items.Count ? items[i] : null;
        }
        return page;
    }

    public static int Wrap(int index, int step, int pageCount) => ((index + step) % pageCount + pageCount) % pageCount;
}

/// <summary>A tap this quick, without moving the mouse, means "go back to the last app".</summary>
public static class QuickTap
{
    public static readonly TimeSpan Window = TimeSpan.FromMilliseconds(300);
    public const double MaxMovePixels = 12;

    public static bool Applies(TimeSpan elapsed, double movedPixels) => elapsed < Window && movedPixels < MaxMovePixels;
}

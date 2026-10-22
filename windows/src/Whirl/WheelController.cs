using System.Windows.Threading;
using Whirl.Core;
using static Whirl.Native;

namespace Whirl;

/// <summary>
/// Runs one "hold the shortcut, pick a slice, let go" interaction. The wheel stays up while the
/// shortcut's modifier keys are held. Letting go over a slice switches to it; over the middle (or
/// pressing Esc) cancels. A very quick tap that barely moves the mouse jumps back to the previous app.
/// </summary>
internal sealed class WheelController
{
    public static WheelController Instance { get; } = new();

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private List<WindowInfo> _windows = [];
    private long _foreground;
    private DateTime _openedAt;
    private POINT _openCursor;

    /// <summary>Replaced by the self-test to observe the choice.</summary>
    public Action<SwitchItem> Perform { get; set; } = WindowActivator.Activate;

    private WheelController()
    {
        _timer.Tick += (_, _) => Tick();
        WheelWindow.Instance.Clicked += () => Finish(commit: true, allowQuickTap: false);
    }

    /// <summary>Raised whenever the wheel closes, whichever way it happened.</summary>
    public event Action? Closed;

    public WheelModel Model => WheelWindow.Instance.Model;
    public bool IsOpen => WheelWindow.Instance.IsShowing;

    /// <summary>Returns false when there was nothing to show.</summary>
    public bool Open(bool driveManually = false)
    {
        var settings = Core.Settings.Current;
        _windows = WindowEnumerator.Enumerate();
        _foreground = GetForegroundWindow().ToInt64();
        var items = settings.Mode == SwitchMode.Windows
            ? ItemOrdering.WindowItems(_windows, _foreground)
            : ItemOrdering.AppItems(_windows, settings.Pinned, _foreground);
        if (items.Count == 0) return false;

        Model.Load(items, settings.Mode);
        _openedAt = DateTime.UtcNow;
        _openCursor = WheelWindow.Instance.ShowAtCursor();
        if (!driveManually) _timer.Start();
        return true;
    }

    public void NextPage() => Model.SwitchPage(1);
    public void PreviousPage() => Model.SwitchPage(-1);

    private void Tick()
    {
        if (!IsOpen) { _timer.Stop(); return; }
        GetCursorPos(out var cursor);
        var (x, y) = WheelWindow.Instance.ToWheelPoint(cursor);
        Model.UpdateHover(x, y);

        // Safety net: if a key-up was ever missed, the physical key state still ends the interaction.
        if (!AllHeld(HotkeyPreset.Find(Core.Settings.Current.HotkeyId).Modifiers)) Finish(commit: true);
    }

    private static bool AllHeld(Mods mods) =>
        (!mods.HasFlag(Mods.Ctrl) || IsDown(VK_CONTROL)) &&
        (!mods.HasFlag(Mods.Alt) || IsDown(VK_MENU)) &&
        (!mods.HasFlag(Mods.Shift) || IsDown(VK_SHIFT)) &&
        (!mods.HasFlag(Mods.Win) || IsDown(VK_LWIN) || IsDown(VK_RWIN));

    /// <summary>Ends the interaction. With <paramref name="commit"/>, switches to whatever is under the cursor.</summary>
    public void Finish(bool commit, bool allowQuickTap = true)
    {
        if (!IsOpen) return;
        _timer.Stop();
        WheelWindow.Instance.HideNow();
        Closed?.Invoke();
        if (!commit) return;

        var target = Model.HoveredItem;
        if (target is null && allowQuickTap && Core.Settings.Current.QuickTapSwitchesBack)
        {
            GetCursorPos(out var now);
            var moved = Math.Sqrt(Math.Pow(now.X - _openCursor.X, 2) + Math.Pow(now.Y - _openCursor.Y, 2));
            if (QuickTap.Applies(DateTime.UtcNow - _openedAt, moved) && ItemOrdering.PreviousApp(_windows, _foreground) is { } previous)
                target = new SwitchItem(previous.AppKey, previous.AppName, null, previous.ExePath, previous.Hwnd, false, false);
        }
        if (target is not null) Perform(target);
    }
}

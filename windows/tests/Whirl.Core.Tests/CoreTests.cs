using Whirl.Core;
using Xunit;

namespace Whirl.Core.Tests;

public class GeometryTests
{
    [Fact]
    public void EverySliceIsHitAtItsOwnCenter()
    {
        for (var i = 0; i < WheelGeometry.SliceCount; i++)
        {
            var (x, y) = WheelGeometry.PointForSlice(i);
            Assert.Equal(WheelHit.OfSlice(i), WheelGeometry.Hit(x, y));
        }
    }

    [Fact]
    public void SliceZeroIsAtTwelveOClockAndOrderIsClockwise()
    {
        Assert.Equal(0, WheelGeometry.SliceIndex(0, -100));
        Assert.Equal(2, WheelGeometry.SliceIndex(100, 0));
        Assert.Equal(4, WheelGeometry.SliceIndex(0, 100));
        Assert.Equal(6, WheelGeometry.SliceIndex(-100, 0));
    }

    [Fact]
    public void HubAndOutside()
    {
        const double c = WheelGeometry.Size / 2;
        Assert.Equal(WheelHit.Hub, WheelGeometry.Hit(c, c));
        Assert.Equal(WheelHit.Hub, WheelGeometry.Hit(c, c + WheelGeometry.HubRadius - 1));
        Assert.Equal(WheelHit.None, WheelGeometry.Hit(0, 0));
        Assert.Equal(WheelHit.None, WheelGeometry.Hit(c, c - WheelGeometry.OuterRadius - 100));
    }
}

public class OrderingTests
{
    private static WindowInfo Win(long hwnd, string app, string? title = null, string? key = null) =>
        new(hwnd, (int)hwnd, title ?? $"{app} window {hwnd}", $@"C:\Apps\{app}.exe", app, key ?? $@"C:\Apps\{app}.exe", false);

    [Fact]
    public void OneItemPerAppInZOrder()
    {
        var windows = new[] { Win(1, "Code"), Win(2, "Chrome"), Win(3, "Code"), Win(4, "Notepad") };
        var items = ItemOrdering.AppItems(windows, [], foreground: 1);
        Assert.Equal(["Code", "Chrome", "Notepad"], items.Select(i => i.Title));
        Assert.Equal(1, items[0].Hwnd);   // the topmost window of the app
        Assert.True(items[0].IsForeground);
        Assert.False(items[1].IsForeground);
    }

    [Fact]
    public void PinnedComeFirstInPinOrderEvenWhenNotRunning()
    {
        var windows = new[] { Win(1, "Code"), Win(2, "Chrome") };
        var pinned = new[] { new PinnedApp(@"C:\Apps\Zed.exe", "Zed"), new PinnedApp(@"c:\apps\chrome.exe", "Chrome") };
        var items = ItemOrdering.AppItems(windows, pinned, foreground: 1);
        Assert.Equal(["Zed", "Chrome", "Code"], items.Select(i => i.Title));
        Assert.False(items[0].IsRunning);
        Assert.Equal(2, items[1].Hwnd);   // matched case-insensitively
        Assert.True(items[0].IsPinned && items[1].IsPinned && !items[2].IsPinned);
    }

    [Fact]
    public void WindowModeListsEveryWindow()
    {
        var windows = new[] { Win(1, "Code", "a.cs"), Win(2, "Code", "b.cs"), Win(3, "Chrome", "") };
        var items = ItemOrdering.WindowItems(windows, foreground: 2);
        Assert.Equal(["a.cs", "b.cs", "Chrome"], items.Select(i => i.Title));
        Assert.Equal("Code", items[0].Subtitle);
        Assert.True(items[1].IsForeground);
    }

    [Fact]
    public void QuickTapGoesToThePreviousApp()
    {
        var windows = new[] { Win(1, "Code"), Win(2, "Code"), Win(3, "Chrome") };
        Assert.Equal(3, ItemOrdering.PreviousApp(windows, foreground: 1)?.Hwnd);
    }

    [Fact]
    public void QuickTapWithOnlyOneAppDoesNothing() =>
        Assert.Null(ItemOrdering.PreviousApp([Win(1, "Code"), Win(2, "Code")], foreground: 1));

    [Fact]
    public void WindowsThatShareAnExecutableCanStillBeSeparateApps()
    {
        // Store apps all run inside ApplicationFrameHost.exe; the enumerator gives each its own key.
        var windows = new[] { Win(1, "Frame", "Calculator", key: "uwp:Calculator"), Win(2, "Frame", "Photos", key: "uwp:Photos") };
        Assert.Equal(2, ItemOrdering.AppItems(windows, [], 1).Count);
    }

    [Theory]
    [InlineData(100, 3, true)]
    [InlineData(299, 11.9, true)]
    [InlineData(300, 0, false)]
    [InlineData(50, 12, false)]
    public void QuickTapRule(int ms, double moved, bool expected) =>
        Assert.Equal(expected, QuickTap.Applies(TimeSpan.FromMilliseconds(ms), moved));
}

public class PagerTests
{
    private static List<SwitchItem> Items(int n) =>
        Enumerable.Range(0, n).Select(i => new SwitchItem($"{i}", $"{i}", null, null, i, false, false)).ToList();

    [Fact]
    public void PageCount()
    {
        Assert.Equal(1, Pager.PageCount(0));
        Assert.Equal(1, Pager.PageCount(8));
        Assert.Equal(2, Pager.PageCount(9));
        Assert.Equal(3, Pager.PageCount(24));
    }

    [Fact]
    public void PagePadsWithEmptySlots()
    {
        var page = Pager.Page(Items(10), 1);
        Assert.Equal(8, page.Length);
        Assert.Equal("8", page[0]?.Id);
        Assert.Equal("9", page[1]?.Id);
        Assert.Null(page[2]);
    }

    [Fact]
    public void Wrapping()
    {
        Assert.Equal(2, Pager.Wrap(0, -1, 3));
        Assert.Equal(0, Pager.Wrap(2, 1, 3));
    }
}

public class HotkeyTests
{
    private const int Ctrl = 0xA2, Alt = 0xA4, Shift = 0xA0, Tab = 0x09, Esc = 0x1B;

    private static HotkeyMatcher Matcher(string id = "ctrl-alt-tab") => new() { Preset = HotkeyPreset.Find(id) };

    private static HotkeyMatcher HeldCtrlAlt()
    {
        var m = Matcher();
        m.OnKey(Ctrl, true);
        m.OnKey(Alt, true);
        return m;
    }

    [Fact]
    public void DefaultIsCtrlAltTab()
    {
        Assert.Equal("ctrl-alt-tab", HotkeyPreset.Default.Id);
        Assert.Equal(Mods.Ctrl | Mods.Alt, HotkeyPreset.Default.Modifiers);
        Assert.Equal(Tab, HotkeyPreset.Default.VirtualKey);
    }

    [Fact]
    public void HoldingTheModifiersAndPressingTabOpensAndSwallowsTheKey()
    {
        var m = HeldCtrlAlt();
        Assert.Equal(new(HotkeyAction.Open, true), m.OnKey(Tab, true));
        Assert.True(m.IsOpen);
        Assert.Equal(new(HotkeyAction.None, true), m.OnKey(Tab, false));   // the key-up is swallowed too
    }

    [Fact]
    public void ReleasingAModifierCommitsAndLetsTheKeyUpThrough()
    {
        var m = HeldCtrlAlt();
        m.OnKey(Tab, true);
        m.OnKey(Tab, false);
        Assert.Equal(new(HotkeyAction.Commit, false), m.OnKey(Alt, false));
        Assert.False(m.IsOpen);
        Assert.Equal(new(HotkeyAction.None, false), m.OnKey(Ctrl, false));
    }

    [Fact]
    public void PressingTabAgainTurnsPagesAndShiftGoesBack()
    {
        var m = HeldCtrlAlt();
        m.OnKey(Tab, true);
        m.OnKey(Tab, false);
        Assert.Equal(HotkeyAction.NextPage, m.OnKey(Tab, true).Action);
        m.OnKey(Tab, false);
        m.OnKey(Shift, true);
        Assert.Equal(HotkeyAction.PreviousPage, m.OnKey(Tab, true).Action);
    }

    [Fact]
    public void KeyAutoRepeatDoesNotTurnPages()
    {
        var m = HeldCtrlAlt();
        m.OnKey(Tab, true);
        Assert.Equal(new(HotkeyAction.None, true), m.OnKey(Tab, true));
        Assert.Equal(new(HotkeyAction.None, true), m.OnKey(Tab, true));
    }

    [Fact]
    public void EscapeCancelsOnlyWhileOpen()
    {
        var m = HeldCtrlAlt();
        Assert.Equal(new(HotkeyAction.None, false), m.OnKey(Esc, true));   // not ours when the wheel is closed
        m.OnKey(Esc, false);
        m.OnKey(Tab, true);
        Assert.Equal(new(HotkeyAction.Cancel, true), m.OnKey(Esc, true));
        Assert.Equal(new(HotkeyAction.None, true), m.OnKey(Esc, false));
        Assert.False(m.IsOpen);
        // Letting go afterwards must not switch anything.
        Assert.Equal(HotkeyAction.None, m.OnKey(Alt, false).Action);
    }

    [Fact]
    public void TabAloneOrWithTheWrongModifiersIsLeftAlone()
    {
        var plain = Matcher();
        Assert.Equal(new(HotkeyAction.None, false), plain.OnKey(Tab, true));
        plain.OnKey(Tab, false);

        var altOnly = Matcher();
        altOnly.OnKey(Alt, true);
        Assert.Equal(new(HotkeyAction.None, false), altOnly.OnKey(Tab, true));   // plain Alt+Tab stays Windows'

        var extra = HeldCtrlAlt();
        extra.OnKey(Shift, true);
        Assert.Equal(new(HotkeyAction.None, false), extra.OnKey(Tab, true));
    }

    [Fact]
    public void InjectedKeysAreIgnored()
    {
        var m = Matcher();
        Assert.Equal(new(HotkeyAction.None, false), m.OnKey(Ctrl, true, injected: true));
        m.OnKey(Alt, true);
        Assert.Equal(HotkeyAction.None, m.OnKey(Tab, true).Action);   // Ctrl was never really down
    }

    [Fact]
    public void LeftOrRightModifierBothCount()
    {
        var m = Matcher();
        m.OnKey(0xA3, true);   // right Ctrl
        m.OnKey(0xA5, true);   // right Alt
        Assert.Equal(HotkeyAction.Open, m.OnKey(Tab, true).Action);
    }

    [Fact]
    public void AnotherPresetWorks()
    {
        var m = Matcher("alt-grave");
        m.OnKey(Alt, true);
        Assert.Equal(HotkeyAction.Open, m.OnKey(0xC0, true).Action);
        Assert.Equal(HotkeyAction.Commit, m.OnKey(Alt, false).Action);
    }

    [Fact]
    public void ResetForgetsHeldKeys()
    {
        var m = HeldCtrlAlt();
        m.OnKey(Tab, true);
        m.Reset();
        Assert.False(m.IsOpen);
        Assert.Equal(HotkeyAction.None, m.OnKey(Tab, true).Action);
    }

    [Fact]
    public void PresetIdsAreUnique() =>
        Assert.Equal(HotkeyPreset.All.Count, HotkeyPreset.All.Select(p => p.Id).Distinct().Count());

    [Fact]
    public void UnknownPresetFallsBackToDefault() => Assert.Same(HotkeyPreset.Default, HotkeyPreset.Find("nope"));
}

public class SettingsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("whirl-tests-").FullName;

    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    [Fact]
    public void RoundTrips()
    {
        var path = Path.Combine(_dir, "settings.json");
        var settings = Settings.Load(path);
        Assert.Equal(SwitchMode.Apps, settings.Mode);
        Assert.True(settings.QuickTapSwitchesBack);

        settings.Mode = SwitchMode.Windows;
        settings.HotkeyId = "alt-grave";
        settings.TogglePin(new PinnedApp(@"C:\Apps\Code.exe", "Code"));
        settings.Save();

        var again = Settings.Load(path);
        Assert.Equal(SwitchMode.Windows, again.Mode);
        Assert.Equal("alt-grave", again.HotkeyId);
        Assert.Equal([new PinnedApp(@"C:\Apps\Code.exe", "Code")], again.Pinned);
    }

    [Fact]
    public void TogglePinTwiceRemovesIt()
    {
        var settings = Settings.Load(Path.Combine(_dir, "s.json"));
        settings.TogglePin(new PinnedApp(@"C:\A.exe", "A"));
        settings.TogglePin(new PinnedApp(@"c:\a.exe", "A"));
        Assert.Empty(settings.Pinned);
    }

    [Fact]
    public void CorruptFileFallsBackToDefaults()
    {
        var path = Path.Combine(_dir, "bad.json");
        File.WriteAllText(path, "{ not json");
        Assert.Equal("tangerine", Settings.Load(path).ThemeId);
    }
}

public class WheelModelTests
{
    private static List<SwitchItem> Items(int n) =>
        Enumerable.Range(0, n).Select(i => new SwitchItem($"{i}", $"App {i}", null, null, i, false, false)).ToList();

    [Fact]
    public void HoveringASliceSelectsItsItem()
    {
        var model = new WheelModel();
        model.Load(Items(5), SwitchMode.Apps);
        var (x, y) = WheelGeometry.PointForSlice(3);
        model.UpdateHover(x, y);
        Assert.Equal("App 3", model.HoveredItem?.Title);
    }

    [Fact]
    public void EmptySlicesCannotBeSelected()
    {
        var model = new WheelModel();
        model.Load(Items(2), SwitchMode.Apps);
        var (x, y) = WheelGeometry.PointForSlice(5);
        model.UpdateHover(x, y);
        Assert.Null(model.HoveredItem);
        Assert.Equal(WheelHit.None, model.Hit);
    }

    [Fact]
    public void TheHubIsNotAnItem()
    {
        var model = new WheelModel();
        model.Load(Items(3), SwitchMode.Apps);
        model.UpdateHover(WheelGeometry.Size / 2, WheelGeometry.Size / 2);
        Assert.Equal(WheelHit.Hub, model.Hit);
        Assert.Null(model.HoveredItem);
    }

    [Fact]
    public void PagingShowsTheNextEightAndWraps()
    {
        var model = new WheelModel();
        model.Load(Items(10), SwitchMode.Apps);
        model.SwitchPage(1);
        Assert.Equal("App 8", model.PageItems[0]?.Title);
        model.SwitchPage(1);
        Assert.Equal("App 0", model.PageItems[0]?.Title);
    }

    [Fact]
    public void OnePageNeverTurns()
    {
        var model = new WheelModel();
        model.Load(Items(4), SwitchMode.Apps);
        model.SwitchPage(1);
        Assert.Equal(0, model.PageIndex);
    }
}

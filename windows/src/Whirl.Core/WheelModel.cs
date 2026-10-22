namespace Whirl.Core;

/// <summary>Live state of the wheel: which page is showing and which slice is under the cursor.</summary>
public sealed class WheelModel
{
    public IReadOnlyList<SwitchItem> Items { get; private set; } = [];
    public int PageIndex { get; private set; }
    public SwitchMode Mode { get; private set; }
    public WheelHit Hit { get; private set; } = WheelHit.None;

    public event Action? Changed;

    public int PageCount => Pager.PageCount(Items.Count);
    public SwitchItem?[] PageItems => Pager.Page(Items, PageIndex);

    public SwitchItem? HoveredItem => Hit.Kind == HitKind.Slice ? PageItems[Hit.Slice] : null;

    public void Load(IReadOnlyList<SwitchItem> items, SwitchMode mode)
    {
        Items = items;
        Mode = mode;
        PageIndex = 0;
        Hit = WheelHit.None;
        Changed?.Invoke();
    }

    /// <summary>Wheel coordinates in DIPs, origin top-left. Empty slices are not selectable.</summary>
    public void UpdateHover(double x, double y)
    {
        var hit = WheelGeometry.Hit(x, y);
        if (hit.Kind == HitKind.Slice && PageItems[hit.Slice] is null) hit = WheelHit.None;
        if (hit == Hit) return;
        Hit = hit;
        Changed?.Invoke();
    }

    public void ClearHover()
    {
        if (Hit == WheelHit.None) return;
        Hit = WheelHit.None;
        Changed?.Invoke();
    }

    public void SwitchPage(int step)
    {
        if (PageCount <= 1) return;
        PageIndex = Pager.Wrap(PageIndex, step, PageCount);
        Hit = WheelHit.None;
        Changed?.Invoke();
    }
}

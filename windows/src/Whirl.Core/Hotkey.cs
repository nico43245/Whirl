namespace Whirl.Core;

[Flags]
public enum Mods { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Win = 8 }

/// <summary>A hold-to-show shortcut offered in Preferences. <c>VirtualKey</c> is the Windows VK code of the trigger key.</summary>
public sealed record HotkeyPreset(string Id, string Title, Mods Modifiers, int VirtualKey)
{
    public const int VkTab = 0x09, VkSpace = 0x20, VkGrave = 0xC0;

    public static readonly IReadOnlyList<HotkeyPreset> All =
    [
        new("ctrl-alt-tab", "Ctrl + Alt + Tab", Mods.Ctrl | Mods.Alt, VkTab),
        new("ctrl-alt-space", "Ctrl + Alt + Space", Mods.Ctrl | Mods.Alt, VkSpace),
        new("ctrl-alt-grave", "Ctrl + Alt + `", Mods.Ctrl | Mods.Alt, VkGrave),
        new("alt-grave", "Alt + `", Mods.Alt, VkGrave),
    ];

    public static HotkeyPreset Default => All[0];

    public static HotkeyPreset Find(string? id) => All.FirstOrDefault(p => p.Id == id) ?? Default;
}

public enum HotkeyAction { None, Open, NextPage, PreviousPage, Cancel, Commit }

/// <summary>What the keyboard hook should do with one key event.</summary>
public readonly record struct HotkeyResult(HotkeyAction Action, bool Swallow);

/// <summary>
/// The shortcut logic, separated from the Windows hook so it can be tested on any OS.
/// Feed it every key event the low-level hook sees. It only cares about modifier keys, the
/// trigger key and Esc (while the wheel is open); it keeps no record of anything else.
/// </summary>
public sealed class HotkeyMatcher
{
    private const int VkEscape = 0x1B;

    private readonly HashSet<int> _down = [];
    private bool _triggerHeld, _escapeHeld;

    public HotkeyPreset Preset { get; set; } = HotkeyPreset.Default;

    /// <summary>True from Open until the wheel is committed or cancelled.</summary>
    public bool IsOpen { get; private set; }

    public static Mods ModifierOf(int vk) => vk switch
    {
        0x11 or 0xA2 or 0xA3 => Mods.Ctrl,
        0x12 or 0xA4 or 0xA5 => Mods.Alt,
        0x10 or 0xA0 or 0xA1 => Mods.Shift,
        0x5B or 0x5C => Mods.Win,
        _ => Mods.None,
    };

    private Mods Held => _down.Aggregate(Mods.None, (m, vk) => m | ModifierOf(vk));

    /// <param name="injected">Events we sent ourselves are ignored.</param>
    public HotkeyResult OnKey(int vk, bool down, bool injected = false)
    {
        if (injected) return new(HotkeyAction.None, false);

        var modifier = ModifierOf(vk);
        if (modifier != Mods.None)
        {
            if (down) _down.Add(vk); else _down.Remove(vk);
            // Letting go of a required modifier ends the interaction. The key-up itself must still
            // reach Windows so no modifier is left stuck.
            if (!down && IsOpen && (Held & Preset.Modifiers) != Preset.Modifiers)
            {
                IsOpen = false;
                return new(HotkeyAction.Commit, false);
            }
            return new(HotkeyAction.None, false);
        }

        if (vk == VkEscape)
        {
            if (down && IsOpen)
            {
                IsOpen = false;
                _escapeHeld = true;
                return new(HotkeyAction.Cancel, true);
            }
            if (!down && _escapeHeld) { _escapeHeld = false; return new(HotkeyAction.None, true); }
            return new(HotkeyAction.None, false);
        }

        if (vk != Preset.VirtualKey) return new(HotkeyAction.None, false);

        if (!down)
        {
            if (!_triggerHeld) return new(HotkeyAction.None, false);
            _triggerHeld = false;
            return new(HotkeyAction.None, true);
        }
        if (_triggerHeld) return new(HotkeyAction.None, true);   // auto-repeat

        if (IsOpen)
        {
            _triggerHeld = true;
            return new((Held & Mods.Shift) != 0 ? HotkeyAction.PreviousPage : HotkeyAction.NextPage, true);
        }
        // Opening needs exactly the preset's modifiers, so Ctrl+Alt+Shift+Tab and friends stay untouched.
        if (Held != Preset.Modifiers) return new(HotkeyAction.None, false);
        IsOpen = true;
        _triggerHeld = true;
        return new(HotkeyAction.Open, true);
    }

    /// <summary>The wheel was closed some other way (a click, a mouse pick, lost key-up events).</summary>
    public void Close() => IsOpen = false;

    /// <summary>Forget held keys, for example after the PC was locked and the key-ups never arrived.</summary>
    public void Reset()
    {
        _down.Clear();
        _triggerHeld = _escapeHeld = IsOpen = false;
    }
}

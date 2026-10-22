using System.Runtime.InteropServices;
using System.Windows.Threading;
using Whirl.Core;
using static Whirl.Native;

namespace Whirl;

/// <summary>
/// A low-level keyboard hook that feeds <see cref="HotkeyMatcher"/>. It sees modifier keys, the
/// shortcut's trigger key and Esc while the wheel is open; nothing typed is read, stored or sent anywhere.
/// </summary>
internal sealed class GlobalHooks : IDisposable
{
    private readonly HookProc _keyboardProc;
    private IntPtr _keyboardHook;
    private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;
    private readonly HotkeyMatcher _matcher = new();

    public GlobalHooks()
    {
        _keyboardProc = KeyboardHook;   // keep the delegate alive for as long as the hook is installed
        ApplySettings();
        Core.Settings.Current.Changed += ApplySettings;
        // The wheel can also be closed by a click; keep the matcher in step.
        WheelController.Instance.Closed += _matcher.Close;
    }

    public void Start()
    {
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(null), 0);
        if (_keyboardHook == IntPtr.Zero)
            System.Windows.MessageBox.Show($"Whirl couldn't watch for its shortcut (error {Marshal.GetLastWin32Error()}). Try restarting it.", "Whirl");
    }

    public void Dispose()
    {
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
    }

    private void ApplySettings() => _matcher.Preset = HotkeyPreset.Find(Core.Settings.Current.HotkeyId);

    // Hooks must return fast: anything heavier is posted to the UI thread.
    private IntPtr KeyboardHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var down = (int)wParam is WM_KEYDOWN or WM_SYSKEYDOWN;
            var injected = (info.flags & LLKHF_INJECTED) != 0;

            var result = _matcher.OnKey((int)info.vkCode, down, injected);
            switch (result.Action)
            {
                case HotkeyAction.Open:
                    _ui.BeginInvoke(() => { if (!WheelController.Instance.Open()) _matcher.Close(); });
                    break;
                case HotkeyAction.NextPage: _ui.BeginInvoke(WheelController.Instance.NextPage); break;
                case HotkeyAction.PreviousPage: _ui.BeginInvoke(WheelController.Instance.PreviousPage); break;
                case HotkeyAction.Cancel: _ui.BeginInvoke(() => WheelController.Instance.Finish(commit: false)); break;
                case HotkeyAction.Commit:
                    if (_matcher.Preset.Modifiers.HasFlag(Mods.Alt)) WindowActivator.SendMaskKey();
                    _ui.BeginInvoke(() => WheelController.Instance.Finish(commit: true));
                    break;
            }
            if (result.Swallow) return 1;
        }
        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }
}

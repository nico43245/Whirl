using System.Diagnostics;
using Whirl.Core;
using static Whirl.Native;

namespace Whirl;

/// <summary>Brings a window to the front, or launches a pinned app that is not running.</summary>
internal static class WindowActivator
{
    public static void Activate(SwitchItem item)
    {
        if (item.Hwnd is { } hwnd && IsWindow((IntPtr)hwnd)) BringToFront((IntPtr)hwnd);
        else if (item.ExePath is { Length: > 0 } exe) Launch(exe);
    }

    private static void Launch(string exe)
    {
        try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or FileNotFoundException) { /* the app was moved or removed */ }
    }

    /// <summary>
    /// Windows only lets the app that currently owns the keyboard focus change it. Sharing the
    /// foreground thread's input queue for a moment is the standard way around that for a switcher.
    /// </summary>
    public static void BringToFront(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

        var foreground = GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
        var thisThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != thisThread && AttachThreadInput(thisThread, foregroundThread, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) AttachThreadInput(thisThread, foregroundThread, false);
        }
    }

    /// <summary>
    /// Pressing and releasing Alt on its own focuses a window's menu bar. A key that does
    /// nothing, sent in between, stops that after we swallowed the Tab.
    /// </summary>
    public static void SendMaskKey()
    {
        const ushort unassigned = 0xE8;
        const uint keyUp = 0x0002;
        var inputs = new INPUT[2];
        inputs[0].type = 1;
        inputs[0].ki = new KEYBDINPUT { wVk = unassigned };
        inputs[1].type = 1;
        inputs[1].ki = new KEYBDINPUT { wVk = unassigned, dwFlags = keyUp };
        SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }
}

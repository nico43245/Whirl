using System.Diagnostics;
using Whirl.Core;
using static Whirl.Native;

namespace Whirl;

/// <summary>
/// <c>Whirl.exe --self-test</c>: drives the real wheel (enumeration, window, hit testing, switching)
/// without a human. It opens two Notepad windows, steers the pointer onto each one's slice, "lets go",
/// and waits for Windows to report that window as the foreground window.
/// </summary>
internal static class SelfTest
{
    private static int _failures;

    public static async Task<int> RunAsync()
    {
        AttachConsole(-1);   // print into the console that started us, if any
        Console.WriteLine();

        var dir = Directory.CreateTempSubdirectory("whirl-selftest-").FullName;
        var started = new List<Process>();
        try
        {
            var names = new[] { "whirl-one.txt", "whirl-two.txt" };
            foreach (var name in names)
            {
                var path = Path.Combine(dir, name);
                File.WriteAllText(path, "Whirl self-test");
                started.Add(Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = false })!);
            }

            var windows = new Dictionary<string, long>();
            foreach (var name in names) windows[name] = await WaitForWindowAsync(name);
            Check("both test windows exist", windows.Values.All(h => h != 0));
            if (_failures > 0) return Finish();

            var controller = WheelController.Instance;
            SwitchItem? chosen = null;
            controller.Perform = item => { chosen = item; WindowActivator.Activate(item); };
            var settings = Core.Settings.Current;
            settings.Mode = SwitchMode.Windows;

            foreach (var name in new[] { names[0], names[1], names[0] })
            {
                var hwnd = windows[name];
                chosen = null;
                Check($"wheel opens for {name}", controller.Open(driveManually: true) && controller.IsOpen);

                var model = controller.Model;
                var found = false;
                for (var p = 0; p < model.PageCount && !found; p++)
                {
                    var slot = Array.FindIndex(model.PageItems, i => i?.Hwnd == hwnd);
                    if (slot >= 0)
                    {
                        var (x, y) = WheelGeometry.PointForSlice(slot);
                        model.UpdateHover(x, y);
                        found = true;
                    }
                    else model.SwitchPage(1);
                }
                Check($"{name} has a slice", found);
                Check("hovering its slice selects it", model.HoveredItem?.Hwnd == hwnd);

                controller.Finish(commit: true, allowQuickTap: false);
                Check("choice reached the switcher", chosen?.Hwnd == hwnd);
                Check($"{name} is now in front", await WaitForForegroundAsync(hwnd));
            }

            settings.Mode = SwitchMode.Apps;
            Check("apps mode lists Notepad", controller.Open(driveManually: true) &&
                  controller.Model.Items.Any(i => i.ExePath?.EndsWith("notepad.exe", StringComparison.OrdinalIgnoreCase) == true));
            controller.Finish(commit: false);
            return Finish();
        }
        finally
        {
            foreach (var p in started) { try { if (!p.HasExited) p.Kill(); } catch (InvalidOperationException) { } }
            // Store-style Notepad keeps running behind a launcher stub; close any window we opened.
            foreach (var w in WindowEnumerator.Enumerate().Where(w => w.Title.Contains("whirl-one") || w.Title.Contains("whirl-two")))
            {
                try { Process.GetProcessById(w.Pid).Kill(); } catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    private static int Finish()
    {
        Console.WriteLine(_failures == 0 ? "\nAll checks passed." : $"\n{_failures} check(s) failed.");
        return _failures == 0 ? 0 : 1;
    }

    private static async Task<long> WaitForWindowAsync(string titlePart)
    {
        for (var i = 0; i < 100; i++)
        {
            var match = WindowEnumerator.Enumerate().FirstOrDefault(w => w.Title.Contains(titlePart, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match.Hwnd;
            await Task.Delay(100);
        }
        return 0;
    }

    private static async Task<bool> WaitForForegroundAsync(long hwnd)
    {
        for (var i = 0; i < 40; i++)
        {
            if (GetForegroundWindow().ToInt64() == hwnd) return true;
            await Task.Delay(100);
        }
        return false;
    }

    private static void Check(string name, bool ok)
    {
        if (!ok) _failures++;
        Console.WriteLine($"  {(ok ? "✓" : "✗")} {name}");
    }
}

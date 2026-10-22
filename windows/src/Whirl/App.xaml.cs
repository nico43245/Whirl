using System.Windows;
using Whirl.Core;

namespace Whirl;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private TrayIcon? _tray;
    private GlobalHooks? _hooks;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // End-to-end check: Whirl.exe --self-test  (opens the real wheel and verifies the switch)
        if (e.Args.Contains("--self-test"))
        {
            Shutdown(await SelfTest.RunAsync());
            return;
        }

        _singleInstance = new Mutex(true, $"Whirl-{Environment.UserName}", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("Whirl is already running — look for its icon in the taskbar's notification area.",
                            "Whirl", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Something went wrong: {args.Exception.Message}", "Whirl");
            args.Handled = true;
        };

        _tray = new TrayIcon();
        _hooks = new GlobalHooks();
        _hooks.Start();
        _ = WheelWindow.Instance;   // build the wheel up front so the first open is instant

        if (!e.Args.Contains("--background") && !Core.Settings.Current.WelcomeShown)
        {
            Core.Settings.Current.WelcomeShown = true;
            Core.Settings.Current.Save();
            _tray.Balloon("Whirl is running",
                          $"Hold {HotkeyPreset.Find(Core.Settings.Current.HotkeyId).Title}, move to a slice, let go. Right-click the tray icon for options.");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hooks?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}

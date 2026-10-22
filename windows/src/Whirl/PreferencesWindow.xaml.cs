using System.Windows;
using Microsoft.Win32;
using Whirl.Core;

namespace Whirl;

public partial class PreferencesWindow : Window
{
    private static PreferencesWindow? _open;
    private readonly Core.Settings _settings = Core.Settings.Current;
    private readonly List<PinnedApp> _pinned;

    public static void ShowSingle()
    {
        _open ??= new PreferencesWindow();
        _open.Show();
        _open.Activate();
    }

    public PreferencesWindow()
    {
        InitializeComponent();
        Closed += (_, _) => _open = null;

        HotkeyPicker.ItemsSource = HotkeyPreset.All;
        HotkeyPicker.SelectedValue = _settings.HotkeyId;
        ModeApps.IsChecked = _settings.Mode == SwitchMode.Apps;
        ModeWindows.IsChecked = _settings.Mode == SwitchMode.Windows;
        QuickTap.IsChecked = _settings.QuickTapSwitchesBack;
        StartWithWindows.IsChecked = StartupRegistration.IsEnabled;
        _pinned = [.. _settings.Pinned];
        PinnedList.ItemsSource = _pinned;
        ThemePicker.ItemsSource = Theme.All.Select(t => new { t.Id, Label = $"{t.Emoji}  {t.Name}" }).ToList();
        ThemePicker.SelectedValue = _settings.ThemeId;
    }

    private void AddApp(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Pin an app to the wheel", Filter = "Apps (*.exe)|*.exe" };
        if (dialog.ShowDialog(this) != true) return;
        if (_pinned.Any(p => string.Equals(p.ExePath, dialog.FileName, StringComparison.OrdinalIgnoreCase))) return;
        var name = System.Diagnostics.FileVersionInfo.GetVersionInfo(dialog.FileName).FileDescription;
        _pinned.Add(new PinnedApp(dialog.FileName, string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(dialog.FileName) : name.Trim()));
        PinnedList.Items.Refresh();
    }

    private void RemoveApp(object sender, RoutedEventArgs e)
    {
        if (PinnedList.SelectedItem is not PinnedApp app) return;
        _pinned.Remove(app);
        PinnedList.Items.Refresh();
    }

    private void Done(object sender, RoutedEventArgs e)
    {
        if (HotkeyPicker.SelectedValue is string hotkey) _settings.HotkeyId = hotkey;
        _settings.Mode = ModeWindows.IsChecked == true ? SwitchMode.Windows : SwitchMode.Apps;
        _settings.QuickTapSwitchesBack = QuickTap.IsChecked == true;
        _settings.Pinned = _pinned;
        if (ThemePicker.SelectedValue is string theme) _settings.ThemeId = theme;
        _settings.Save();
        try { StartupRegistration.IsEnabled = StartWithWindows.IsChecked == true; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show("Couldn't change the start-with-Windows setting.", "Whirl");
        }
        Close();
    }
}

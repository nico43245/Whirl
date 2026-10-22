using System.Windows;
using Whirl.Core;
using Forms = System.Windows.Forms;

namespace Whirl;

/// <summary>The notification-area icon and its menu.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayIcon()
    {
        var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Whirl.ico"))!.Stream;
        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream),
            Text = $"Whirl — hold {HotkeyPreset.Find(Core.Settings.Current.HotkeyId).Title}",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip(),
        };
        _icon.ContextMenuStrip.Opening += (_, _) => Build();
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) PreferencesWindow.ShowSingle(); };
        Core.Settings.Current.Changed += () => _icon.Text = $"Whirl — hold {HotkeyPreset.Find(Core.Settings.Current.HotkeyId).Title}";
        Build();
    }

    public void Balloon(string title, string text) => _icon.ShowBalloonTip(6000, title, text, Forms.ToolTipIcon.Info);

    /// <summary>Rebuilt every time it opens so the pinned list and mode are current.</summary>
    private void Build()
    {
        var settings = Core.Settings.Current;
        var menu = _icon.ContextMenuStrip!;
        menu.Items.Clear();
        menu.Items.Add(new Forms.ToolStripMenuItem($"Hold {HotkeyPreset.Find(settings.HotkeyId).Title}, move to a slice, let go") { Enabled = false });
        menu.Items.Add(new Forms.ToolStripSeparator());

        var show = new Forms.ToolStripMenuItem("Show");
        foreach (var (mode, title) in new[] { (SwitchMode.Apps, "Apps"), (SwitchMode.Windows, "Windows") })
        {
            var chosen = mode;
            show.DropDownItems.Add(new Forms.ToolStripMenuItem(title, null, (_, _) => { settings.Mode = chosen; settings.Save(); }) { Checked = settings.Mode == mode });
        }
        menu.Items.Add(show);

        var pin = new Forms.ToolStripMenuItem("Pin to Wheel");
        var pinned = settings.Pinned.Select(p => p.ExePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ItemOrdering.AppItems(WindowEnumerator.Enumerate(), [], 0).Where(i => !string.IsNullOrEmpty(i.ExePath)).OrderBy(i => i.Title))
        {
            var app = new PinnedApp(item.ExePath!, item.Title);
            pin.DropDownItems.Add(new Forms.ToolStripMenuItem(item.Title, null, (_, _) => { settings.TogglePin(app); settings.Save(); })
            {
                Checked = pinned.Contains(app.ExePath),
            });
        }
        menu.Items.Add(pin);

        var themes = new Forms.ToolStripMenuItem("Theme");
        foreach (var theme in Theme.All)
        {
            var id = theme.Id;
            themes.DropDownItems.Add(new Forms.ToolStripMenuItem($"{theme.Emoji}  {theme.Name}", null, (_, _) =>
            {
                settings.ThemeId = id;
                settings.Save();
            }) { Checked = settings.ThemeId == id });
        }
        menu.Items.Add(themes);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Preferences…", null, (_, _) => PreferencesWindow.ShowSingle());
        menu.Items.Add("Quit Whirl", null, (_, _) => Application.Current.Shutdown());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Whirl.Core;

public enum SwitchMode { Apps, Windows }

/// <summary>User preferences, stored as JSON in %APPDATA%\Whirl\settings.json. Nothing else is ever stored.</summary>
public sealed class Settings
{
    // Computed, not stored: static initializers run in order and Load() below needs this path.
    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Whirl");

    public static Settings Current { get; private set; } = Load(DefaultPath);

    private static string DefaultPath => Path.Combine(Folder, "settings.json");

    [JsonIgnore] public string? FilePath { get; private set; }

    public string ThemeId { get; set; } = "tangerine";
    public string HotkeyId { get; set; } = HotkeyPreset.Default.Id;
    [JsonConverter(typeof(JsonStringEnumConverter<SwitchMode>))]
    public SwitchMode Mode { get; set; } = SwitchMode.Apps;
    /// <summary>Release the shortcut within a fraction of a second, without moving, to jump to the previous app.</summary>
    public bool QuickTapSwitchesBack { get; set; } = true;
    public List<PinnedApp> Pinned { get; set; } = [];
    public bool WelcomeShown { get; set; }

    public event Action? Changed;

    public void TogglePin(PinnedApp app)
    {
        var existing = Pinned.FindIndex(p => string.Equals(p.ExePath, app.ExePath, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) Pinned.RemoveAt(existing); else Pinned.Add(app);
    }

    public void Save()
    {
        try
        {
            var path = FilePath ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException) { /* settings are a convenience; never crash over them */ }
        Changed?.Invoke();
    }

    public static Settings Load(string path)
    {
        Settings result;
        try
        {
            result = File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings() : new Settings();
        }
        catch (Exception e) when (e is IOException or JsonException) { result = new Settings(); }
        result.FilePath = path;
        return result;
    }

    /// <summary>For tests: use throwaway settings.</summary>
    public static void UseForTesting(Settings settings) => Current = settings;
}

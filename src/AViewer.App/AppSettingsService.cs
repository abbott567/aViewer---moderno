using System.IO;
using System.Text.Json;

namespace AViewer.App;

internal sealed class AppSettingsService
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AViewerModern");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "app-settings.json");
    private Settings _settings = Load();

    public bool AlwaysOnTop
    {
        get => _settings.AlwaysOnTop;
        set { _settings.AlwaysOnTop = value; Save(); }
    }

    public bool ShowRelationships
    {
        get => _settings.ShowRelationships;
        set { _settings.ShowRelationships = value; Save(); }
    }

    public bool IncludeArrowNavigation
    {
        get => _settings.IncludeArrowNavigation;
        set { _settings.IncludeArrowNavigation = value; Save(); }
    }

    public bool ShowUnavailableProperties
    {
        get => _settings.ShowUnavailableProperties;
        set { _settings.ShowUnavailableProperties = value; Save(); }
    }

    private static Settings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings()
                : new Settings();
        }
        catch { return new Settings(); }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public sealed class Settings
    {
        public Settings() { }
        public bool AlwaysOnTop { get; set; }
        public bool ShowRelationships { get; set; } = true;
        public bool IncludeArrowNavigation { get; set; }
        public bool ShowUnavailableProperties { get; set; }
    }
}

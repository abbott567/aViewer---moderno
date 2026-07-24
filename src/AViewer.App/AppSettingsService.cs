using System.IO;
using System.Text.Json;

namespace AViewer.App;

public sealed class AppSettingsService
{
    private readonly string _settingsPath;
    private AppSettings _settings = new();

    public AppSettingsService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AViewerModern");
        _settingsPath = Path.Combine(directory, "app-settings.json");
        Load();
    }

    public bool AlwaysOnTop
    {
        get => _settings.AlwaysOnTop;
        set
        {
            if (_settings.AlwaysOnTop == value)
            {
                return;
            }

            _settings.AlwaysOnTop = value;
            Save();
        }
    }

    public bool ShowRelationships
    {
        get => _settings.ShowRelationships;
        set
        {
            if (_settings.ShowRelationships == value)
            {
                return;
            }

            _settings.ShowRelationships = value;
            Save();
        }
    }


    public bool IncludeArrowNavigation
    {
        get => _settings.IncludeArrowNavigation;
        set
        {
            if (_settings.IncludeArrowNavigation == value)
            {
                return;
            }

            _settings.IncludeArrowNavigation = value;
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            _settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath))
                ?? new AppSettings();
        }
        catch (IOException)
        {
            _settings = new AppSettings();
        }
        catch (JsonException)
        {
            _settings = new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            _settings = new AppSettings();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                _settingsPath,
                JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException)
        {
            // The setting still applies for this session if persistence fails.
        }
        catch (UnauthorizedAccessException)
        {
            // The setting still applies for this session if persistence fails.
        }
    }

    private sealed class AppSettings
    {
        public bool AlwaysOnTop { get; set; }
        public bool ShowRelationships { get; set; }
        public bool IncludeArrowNavigation { get; set; } = true;
    }
}

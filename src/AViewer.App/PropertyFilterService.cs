using System.IO;
using System.Text.Json;
using AViewer.Core.Models;

namespace AViewer.App;

public sealed class PropertyFilterService
{
    private const string Separator = "\u001f";
    private readonly string _settingsPath;
    private readonly HashSet<string> _hiddenKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PropertyDescriptor> _knownProperties = new(StringComparer.Ordinal);

    public PropertyFilterService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AViewerModern");
        _settingsPath = Path.Combine(directory, "property-filter.json");
        Load();
    }

    public IReadOnlyList<PropertyChoice> GetChoices(IEnumerable<AccessibilityProperty> currentProperties)
    {
        Register(currentProperties);

        return _knownProperties.Values
            .OrderBy(property => property.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
            .Select(property => new PropertyChoice
            {
                Group = property.Group,
                Name = property.Name,
                IsSelected = !_hiddenKeys.Contains(CreateKey(property.Group, property.Name))
            })
            .ToList();
    }

    public IReadOnlyList<AccessibilityProperty> Filter(IEnumerable<AccessibilityProperty> properties)
    {
        var propertyList = properties.ToList();
        Register(propertyList);

        return propertyList
            .Where(property => !_hiddenKeys.Contains(CreateKey(property.Group, property.Name)))
            .ToList();
    }

    public void Apply(IEnumerable<PropertyChoice> choices)
    {
        _hiddenKeys.Clear();

        foreach (var choice in choices)
        {
            var key = CreateKey(choice.Group, choice.Name);
            _knownProperties[key] = new PropertyDescriptor(choice.Group, choice.Name);

            if (!choice.IsSelected)
            {
                _hiddenKeys.Add(key);
            }
        }

        Save();
    }

    private void Register(IEnumerable<AccessibilityProperty> properties)
    {
        var changed = false;

        foreach (var property in properties)
        {
            var key = CreateKey(property.Group, property.Name);
            if (_knownProperties.ContainsKey(key))
            {
                continue;
            }

            _knownProperties[key] = new PropertyDescriptor(property.Group, property.Name);
            changed = true;
        }

        if (changed)
        {
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

            var state = JsonSerializer.Deserialize<PropertyFilterState>(File.ReadAllText(_settingsPath));
            if (state is null)
            {
                return;
            }

            foreach (var key in state.HiddenKeys)
            {
                _hiddenKeys.Add(key);
            }

            foreach (var property in state.KnownProperties)
            {
                _knownProperties[CreateKey(property.Group, property.Name)] = property;
            }
        }
        catch (IOException)
        {
            // A corrupt or inaccessible preference file must not prevent startup.
        }
        catch (JsonException)
        {
            // Ignore invalid settings and rebuild the catalogue from inspected data.
        }
        catch (UnauthorizedAccessException)
        {
            // Continue with all properties visible when preferences cannot be read.
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

            var state = new PropertyFilterState
            {
                HiddenKeys = _hiddenKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                KnownProperties = _knownProperties.Values
                    .OrderBy(property => property.Group, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            File.WriteAllText(
                _settingsPath,
                JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException)
        {
            // Filtering still works for the current session if persistence fails.
        }
        catch (UnauthorizedAccessException)
        {
            // Filtering still works for the current session if persistence fails.
        }
    }

    private static string CreateKey(string group, string name) => $"{group}{Separator}{name}";

    private sealed class PropertyFilterState
    {
        public List<string> HiddenKeys { get; init; } = [];
        public List<PropertyDescriptor> KnownProperties { get; init; } = [];
    }

    public sealed record PropertyDescriptor(string Group, string Name);
}

public sealed class PropertyChoice
{
    public required string Group { get; init; }
    public required string Name { get; init; }
    public bool IsSelected { get; set; }
}

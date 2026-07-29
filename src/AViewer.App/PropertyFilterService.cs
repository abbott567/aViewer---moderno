using System.IO;
using System.Text.Json;
using AViewer.Core.Models;

namespace AViewer.App;

public sealed class PropertyChoice
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public bool IsSelected { get; set; }
}

internal sealed class PropertyFilterService
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AViewerModern");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "property-filter.json");
    private HashSet<string> _hidden = Load();

    public IReadOnlyList<PropertyChoice> GetChoices(IEnumerable<AccessibilityProperty> properties) =>
        properties
            .Select(KeyFor)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(key => new PropertyChoice
            {
                Key = key,
                Label = key.Replace("|", " — ", StringComparison.Ordinal),
                IsSelected = !_hidden.Contains(key)
            })
            .ToList();

    public IReadOnlyList<AccessibilityProperty> Filter(IEnumerable<AccessibilityProperty> properties) =>
        properties.Where(property => !_hidden.Contains(KeyFor(property))).ToList();

    public void Apply(IEnumerable<PropertyChoice> choices)
    {
        _hidden = choices.Where(choice => !choice.IsSelected).Select(choice => choice.Key).ToHashSet(StringComparer.Ordinal);
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_hidden.OrderBy(value => value), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static HashSet<string> Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? (JsonSerializer.Deserialize<string[]>(File.ReadAllText(FilePath)) ?? []).ToHashSet(StringComparer.Ordinal)
                : [];
        }
        catch { return []; }
    }

    private static string KeyFor(AccessibilityProperty property) => $"{property.Group}|{property.Name}";
}

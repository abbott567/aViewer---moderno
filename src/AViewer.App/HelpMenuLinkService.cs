using System.IO;
using System.Text.Json;

namespace AViewer.App;

internal sealed class ConfiguredHelpMenuEntry
{
    public ConfiguredHelpMenuEntry() { }
    public string? Label { get; init; }
    public string? Url { get; init; }
    public bool IsSeparator { get; init; }
}

internal static class HelpMenuLinkService
{
    public static IReadOnlyList<ConfiguredHelpMenuEntry> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "HelpMenuLinks.json");
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<List<ConfiguredHelpMenuEntry>>(File.ReadAllText(path), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? []
                : [];
        }
        catch { return []; }
    }

    public static bool IsAllowedUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

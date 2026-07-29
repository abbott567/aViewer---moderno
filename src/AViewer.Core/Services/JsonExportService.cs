using System.Text.Json;
using System.Text.Json.Serialization;
using AViewer.Core.Models;

namespace AViewer.Core.Services;

public static class JsonExportService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(AccessibilityNode node) => JsonSerializer.Serialize(node, Options);
}

using System.Net;
using System.Text;
using AViewer.Core.Models;

namespace AViewer.Core.Services;

public static class HtmlExportService
{
    public static string SerializeElement(AccessibilityNode node) => SerializeNode(node, includeChildren: false, 0);

    public static string SerializeSubtree(AccessibilityNode node) => SerializeNode(node, includeChildren: true, 0);

    private static string SerializeNode(AccessibilityNode node, bool includeChildren, int depth)
    {
        var indent = new string(' ', depth * 2);
        var attributes = ReadAttributes(node);
        var tag = ResolveTag(node, attributes);
        var builder = new StringBuilder();

        builder.Append(indent).Append('<').Append(tag);
        AppendAttribute(builder, "id", Value(attributes, "id"));
        AppendAttribute(builder, "class", Value(attributes, "class"));
        AppendAttribute(builder, "role", FirstNonEmpty(Value(attributes, "xml-roles"), Property(node, "UIA", "Aria role")));
        AppendAriaProperties(builder, Property(node, "UIA", "Aria properties"));
        AppendAttribute(builder, "aria-label", node.Name);
        builder.Append('>');

        if (includeChildren && node.Children.Count > 0)
        {
            builder.AppendLine();
            foreach (var child in node.Children)
            {
                builder.AppendLine(SerializeNode(child, true, depth + 1));
            }
            builder.Append(indent);
        }
        else if (!string.IsNullOrWhiteSpace(node.Name))
        {
            builder.Append(WebUtility.HtmlEncode(node.Name));
        }

        builder.Append("</").Append(tag).Append('>');
        return builder.ToString();
    }

    private static Dictionary<string, string> ReadAttributes(AccessibilityNode node)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var raw = Property(node, "IA2", "Attributes");
        if (string.IsNullOrWhiteSpace(raw)) return result;

        foreach (var item in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = item.IndexOf(':');
            if (separator <= 0) continue;
            result[item[..separator].Trim()] = item[(separator + 1)..].Trim();
        }

        return result;
    }

    private static string ResolveTag(AccessibilityNode node, IReadOnlyDictionary<string, string> attributes)
    {
        var explicitTag = FirstNonEmpty(Value(attributes, "tag"), Value(attributes, "html-tag"));
        if (!string.IsNullOrWhiteSpace(explicitTag) && explicitTag.All(character => char.IsLetterOrDigit(character) || character == '-'))
        {
            return explicitTag.ToLowerInvariant();
        }

        var role = node.ControlType.ToLowerInvariant();
        return role switch
        {
            var value when value.Contains("heading") => "h2",
            var value when value.Contains("button") => "button",
            var value when value.Contains("link") => "a",
            var value when value.Contains("check") => "input",
            var value when value.Contains("radio") => "input",
            var value when value.Contains("textbox") || value.Contains("edit") => "input",
            var value when value.Contains("table") => "table",
            var value when value.Contains("row") => "tr",
            var value when value.Contains("column header") || value.Contains("row header") => "th",
            var value when value.Contains("cell") => "td",
            var value when value.Contains("list item") => "li",
            var value when value.Contains("list") => "ul",
            var value when value.Contains("image") => "img",
            var value when value.Contains("paragraph") => "p",
            _ => "div"
        };
    }

    private static void AppendAriaProperties(StringBuilder builder, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        foreach (var item in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = item.IndexOf('=');
            if (separator <= 0) continue;
            var name = item[..separator].Trim();
            var value = item[(separator + 1)..].Trim();
            if (!name.StartsWith("aria-", StringComparison.OrdinalIgnoreCase)) continue;
            AppendAttribute(builder, name.ToLowerInvariant(), value);
        }
    }

    private static void AppendAttribute(StringBuilder builder, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        builder.Append(' ').Append(name).Append("=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
    }

    private static string Property(AccessibilityNode node, string group, string name) =>
        node.Properties.FirstOrDefault(property =>
            property.Group.Equals(group, StringComparison.OrdinalIgnoreCase) &&
            property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

    private static string Value(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : string.Empty;

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

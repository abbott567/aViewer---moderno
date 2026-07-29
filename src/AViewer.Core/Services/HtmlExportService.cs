using System.Net;
using System.Text;
using AViewer.Core.Models;

namespace AViewer.Core.Services;

public static class HtmlExportService
{
    public static string SerializeElement(AccessibilityNode node) => Build(node, false, 0);
    public static string SerializeSubtree(AccessibilityNode node) => Build(node, true, 0);

    private static string Build(AccessibilityNode node, bool includeChildren, int depth)
    {
        var indent = new string(' ', depth * 2);
        var tag = GuessTag(node);
        var name = WebUtility.HtmlEncode(node.Name);
        var role = WebUtility.HtmlEncode(node.ControlType.ToLowerInvariant());
        var id = WebUtility.HtmlEncode(node.AutomationId);
        var attributes = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(id)) attributes.Append($" id=\"{id}\"");
        if (!NativeRoleMatches(tag, role)) attributes.Append($" role=\"{role}\"");

        if (!includeChildren || node.Children.Count == 0)
        {
            return $"{indent}<{tag}{attributes}>{name}</{tag}>";
        }

        var lines = new List<string> { $"{indent}<{tag}{attributes}>" };
        if (!string.IsNullOrWhiteSpace(name)) lines.Add($"{indent}  {name}");
        lines.AddRange(node.Children.Select(child => Build(child, true, depth + 1)));
        lines.Add($"{indent}</{tag}>");
        return string.Join(Environment.NewLine, lines);
    }

    private static string GuessTag(AccessibilityNode node) => node.ControlType.ToLowerInvariant() switch
    {
        "button" or "push button" => "button",
        "edit" or "document" or "text" => "div",
        "hyperlink" or "link" => "a",
        "check box" or "checkbox" => "input",
        "radio button" => "input",
        "list" => "ul",
        "list item" => "li",
        "table" => "table",
        "row" => "tr",
        "cell" => "td",
        "heading" => "h2",
        _ => "div"
    };

    private static bool NativeRoleMatches(string tag, string role) =>
        (tag == "button" && role.Contains("button", StringComparison.Ordinal)) ||
        (tag == "a" && role.Contains("link", StringComparison.Ordinal));
}

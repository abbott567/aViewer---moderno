using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using AViewer.Core.Models;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Patterns;
using FlaUI.Core.Patterns.Infrastructure;
using FlaUI.UIA3;

namespace AViewer.Core.Services;

public sealed class Uia3Inspector : IAccessibilityInspector
{
    private readonly UIA3Automation _automation = new();
    private readonly Ia2Inspector _ia2Inspector = new();

    public AccessibilityInspectionSnapshot InspectPoint(int x, int y, int maxDepth = 3)
    {
        var element = _automation.FromPoint(new Point(x, y));
        var depth = Math.Max(0, maxDepth);
        var uiaRoot = element is null ? null : Map(element, 0, depth);
        var legacyTrees = _ia2Inspector.InspectTreesPoint(x, y, depth);
        return new AccessibilityInspectionSnapshot(
            uiaRoot,
            legacyTrees.MsaaRoot,
            legacyTrees.Ia2Root);
    }

    public AccessibilityInspectionSnapshot InspectFocused(int maxDepth = 3)
    {
        var element = _automation.FocusedElement();
        if (element is null)
        {
            return new AccessibilityInspectionSnapshot(null, null, null);
        }

        var depth = Math.Max(0, maxDepth);
        var uiaRoot = Map(element, 0, depth);
        var centreX = (int)Math.Round(uiaRoot.BoundingX + (uiaRoot.BoundingWidth / 2));
        var centreY = (int)Math.Round(uiaRoot.BoundingY + (uiaRoot.BoundingHeight / 2));
        var legacyTrees = _ia2Inspector.InspectTreesPoint(centreX, centreY, depth);
        return new AccessibilityInspectionSnapshot(
            uiaRoot,
            legacyTrees.MsaaRoot,
            legacyTrees.Ia2Root);
    }

    public AccessibilityInspectionSnapshot InspectParent(AccessibilityNode node, string api, int maxDepth = 3)
    {
        var x = (int)Math.Round(node.BoundingX + (node.BoundingWidth / 2));
        var y = (int)Math.Round(node.BoundingY + (node.BoundingHeight / 2));
        var depth = Math.Max(0, maxDepth);

        AutomationElement? pointElement = null;
        AutomationElement? uiaParent = null;
        try
        {
            pointElement = _automation.FromPoint(new Point(x, y));
            var matching = FindMatchingAncestor(pointElement, node);
            uiaParent = matching?.Parent;
        }
        catch
        {
            uiaParent = null;
        }

        var legacyTrees = _ia2Inspector.InspectParentTreesPoint(x, y, depth);
        return new AccessibilityInspectionSnapshot(
            uiaParent is null ? null : Map(uiaParent, 0, depth),
            legacyTrees.MsaaRoot,
            legacyTrees.Ia2Root);
    }

    private static AutomationElement? FindMatchingAncestor(AutomationElement? element, AccessibilityNode node)
    {
        var current = element;
        for (var index = 0; current is not null && index < 20; index++)
        {
            try
            {
                var p = current.Properties;
                var rect = Read(() => p.BoundingRectangle.ValueOrDefault, Rectangle.Empty);
                var name = Text(() => p.Name.ValueOrDefault);
                if (Math.Abs(rect.X - node.BoundingX) < 2 &&
                    Math.Abs(rect.Y - node.BoundingY) < 2 &&
                    Math.Abs(rect.Width - node.BoundingWidth) < 2 &&
                    Math.Abs(rect.Height - node.BoundingHeight) < 2 &&
                    (string.IsNullOrWhiteSpace(node.Name) || string.Equals(name, node.Name, StringComparison.Ordinal)))
                {
                    return current;
                }
                current = current.Parent;
            }
            catch
            {
                return element;
            }
        }
        return element;
    }

    private AccessibilityNode Map(AutomationElement element, int depth, int maxDepth)
    {
        var p = element.Properties;
        var rect = Read(() => p.BoundingRectangle.ValueOrDefault, Rectangle.Empty);
        var processId = Read(() => p.ProcessId.ValueOrDefault, 0);
        var patterns = string.Join(", ", element.GetSupportedPatterns().Select(pattern => pattern.Name ?? string.Empty));
        var runtimeIds = Read(() => p.RuntimeId.ValueOrDefault, null) ?? Array.Empty<int>();
        var runtimeId = string.Join(".", runtimeIds);

        var node = new AccessibilityNode
        {
            Api = "UIA",
            Id = string.IsNullOrWhiteSpace(runtimeId) ? Guid.NewGuid().ToString("N") : runtimeId,
            Name = Text(() => p.Name.ValueOrDefault),
            ControlType = Read(() => p.ControlType.ValueOrDefault, ControlType.Custom).ToString(),
            Framework = Text(() => p.FrameworkId.ValueOrDefault),
            AutomationId = Text(() => p.AutomationId.ValueOrDefault),
            ClassName = Text(() => p.ClassName.ValueOrDefault),
            ProcessId = processId,
            ProcessName = GetProcessName(processId),
            BoundingRectangle = $"{rect.X:0},{rect.Y:0} {rect.Width:0}x{rect.Height:0}",
            BoundingX = rect.X,
            BoundingY = rect.Y,
            BoundingWidth = rect.Width,
            BoundingHeight = rect.Height,
            IsEnabled = Read(() => p.IsEnabled.ValueOrDefault, false),
            IsKeyboardFocusable = Read(() => p.IsKeyboardFocusable.ValueOrDefault, false),
            HasKeyboardFocus = Read(() => p.HasKeyboardFocus.ValueOrDefault, false),
            Properties =
            [
                new("UIA", "Name", Text(() => p.Name.ValueOrDefault)),
                new("UIA", "Control type", Read(() => p.ControlType.ValueOrDefault, ControlType.Custom).ToString()),
                new("UIA", "Localized control type", Text(() => p.LocalizedControlType.ValueOrDefault)),
                new("UIA", "Automation ID", Text(() => p.AutomationId.ValueOrDefault)),
                new("UIA", "Class name", Text(() => p.ClassName.ValueOrDefault)),
                new("UIA", "Framework", Text(() => p.FrameworkId.ValueOrDefault)),
                new("UIA", "Help text", Text(() => p.HelpText.ValueOrDefault)),
                new("UIA", "Access key", Text(() => p.AccessKey.ValueOrDefault)),
                new("UIA", "Accelerator key", Text(() => p.AcceleratorKey.ValueOrDefault)),
                new("UIA", "Item status", Text(() => p.ItemStatus.ValueOrDefault)),
                new("UIA", "Item type", Text(() => p.ItemType.ValueOrDefault)),
                new("UIA", "Aria role", Text(() => p.AriaRole.ValueOrDefault)),
                new("UIA", "Aria properties", Text(() => p.AriaProperties.ValueOrDefault)),
                new("UIA", "Bounding rectangle", $"{rect.X:0},{rect.Y:0} {rect.Width:0}x{rect.Height:0}"),
                new("UIA", "Enabled", Read(() => p.IsEnabled.ValueOrDefault, false).ToString()),
                new("UIA", "Focusable", Read(() => p.IsKeyboardFocusable.ValueOrDefault, false).ToString()),
                new("UIA", "Focused", Read(() => p.HasKeyboardFocus.ValueOrDefault, false).ToString()),
                new("UIA", "Offscreen", Read(() => p.IsOffscreen.ValueOrDefault, false).ToString()),
                new("UIA", "Password", Read(() => p.IsPassword.ValueOrDefault, false).ToString()),
                new("UIA", "Supported patterns", patterns)
            ]
        };

        AddTableProperties(element, node);
        AddRelationships(element, node);

        if (depth < maxDepth)
        {
            foreach (var child in SafeChildren(element))
            {
                node.Children.Add(Map(child, depth + 1, maxDepth));
            }
        }

        return node;
    }

    private static void AddTableProperties(AutomationElement element, AccessibilityNode node)
    {
        if (TryPattern(element.Patterns.Grid, out IGridPattern? grid))
        {
            node.Properties.Add(new("UIA Table", "Row count", Read(() => grid.RowCount.ValueOrDefault, 0).ToString()));
            node.Properties.Add(new("UIA Table", "Column count", Read(() => grid.ColumnCount.ValueOrDefault, 0).ToString()));
        }

        if (TryPattern(element.Patterns.Table, out ITablePattern? table))
        {
            var rowHeaders = Read(() => table.RowHeaders.ValueOrDefault, null) ?? Array.Empty<AutomationElement>();
            var columnHeaders = Read(() => table.ColumnHeaders.ValueOrDefault, null) ?? Array.Empty<AutomationElement>();
            node.Properties.Add(new("UIA Table", "Row or column major", Read(() => table.RowOrColumnMajor.ValueOrDefault, RowOrColumnMajor.Indeterminate).ToString()));
            node.Properties.Add(new("UIA Table", "Row headers", DescribeElements(rowHeaders)));
            node.Properties.Add(new("UIA Table", "Column headers", DescribeElements(columnHeaders)));
        }

        if (TryPattern(element.Patterns.GridItem, out IGridItemPattern? gridItem))
        {
            node.Properties.Add(new("UIA Table item", "Row", Read(() => gridItem.Row.ValueOrDefault, -1).ToString()));
            node.Properties.Add(new("UIA Table item", "Column", Read(() => gridItem.Column.ValueOrDefault, -1).ToString()));
            node.Properties.Add(new("UIA Table item", "Row span", Read(() => gridItem.RowSpan.ValueOrDefault, 1).ToString()));
            node.Properties.Add(new("UIA Table item", "Column span", Read(() => gridItem.ColumnSpan.ValueOrDefault, 1).ToString()));
            var containingGrid = Read(() => gridItem.ContainingGrid.ValueOrDefault, null);
            node.Properties.Add(new("UIA Table item", "Containing grid", DescribeElement(containingGrid)));
        }

        if (TryPattern(element.Patterns.TableItem, out ITableItemPattern? tableItem))
        {
            var rowHeaders = Read(() => tableItem.RowHeaderItems.ValueOrDefault, null) ?? Array.Empty<AutomationElement>();
            var columnHeaders = Read(() => tableItem.ColumnHeaderItems.ValueOrDefault, null) ?? Array.Empty<AutomationElement>();
            node.Properties.Add(new("UIA Table item", "Row header items", DescribeElements(rowHeaders)));
            node.Properties.Add(new("UIA Table item", "Column header items", DescribeElements(columnHeaders)));
        }
    }

    private static void AddRelationships(AutomationElement element, AccessibilityNode node)
    {
        var p = element.Properties;

        AddRelationship(node, "Labeled by", "UIA LabeledBy / aria-labelledby", Read(() => p.LabeledBy.ValueOrDefault, null));
        AddRelationships(node, "Described by", "UIA DescribedBy / aria-describedby", Read(() => p.DescribedBy.ValueOrDefault, null));
        AddRelationships(node, "Controller for", "UIA ControllerFor / aria-controls", Read(() => p.ControllerFor.ValueOrDefault, null));
        AddRelationships(node, "Flows to", "UIA FlowsTo / aria-flowto", Read(() => p.FlowsTo.ValueOrDefault, null));
        AddRelationships(node, "Flows from", "UIA FlowsFrom", Read(() => p.FlowsFrom.ValueOrDefault, null));

        if (TryPattern(element.Patterns.TableItem, out ITableItemPattern? tableItem))
        {
            AddRelationships(node, "Row header", "UIA TableItem row header / HTML headers", Read(() => tableItem.RowHeaderItems.ValueOrDefault, null));
            AddRelationships(node, "Column header", "UIA TableItem column header / HTML headers", Read(() => tableItem.ColumnHeaderItems.ValueOrDefault, null));
        }


        foreach (var group in node.Relationships.GroupBy(relationship => relationship.Type))
        {
            node.Properties.Add(new(
                "UIA Relationships",
                group.Key,
                string.Join("; ", group.Select(relationship => FormatRelationshipTarget(relationship)))));
        }
    }

    private static bool TryPattern<T>(IAutomationPattern<T> automationPattern, [NotNullWhen(true)] out T? pattern)
        where T : class, IPattern
    {
        try { return automationPattern.TryGetPattern(out pattern); }
        catch { pattern = null; return false; }
    }

    private static void AddRelationship(
        AccessibilityNode node,
        string type,
        string source,
        AutomationElement? target)
    {
        if (target is null) return;
        var relationship = MapRelationship(type, source, target);
        if (relationship is not null) node.Relationships.Add(relationship);
    }

    private static void AddRelationships(
        AccessibilityNode node,
        string type,
        string source,
        IEnumerable<AutomationElement>? targets)
    {
        if (targets is null) return;
        foreach (var target in targets)
        {
            AddRelationship(node, type, source, target);
        }
    }

    private static AccessibilityRelationship? MapRelationship(string type, string source, AutomationElement target)
    {
        try
        {
            var p = target.Properties;
            var rect = Read(() => p.BoundingRectangle.ValueOrDefault, Rectangle.Empty);
            var runtimeIds = Read(() => p.RuntimeId.ValueOrDefault, null) ?? Array.Empty<int>();
            var id = string.Join(".", runtimeIds);
            return new AccessibilityRelationship(
                type,
                source,
                id,
                Text(() => p.Name.ValueOrDefault),
                Read(() => p.ControlType.ValueOrDefault, ControlType.Custom).ToString(),
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height);
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeElements(IEnumerable<AutomationElement> elements)
    {
        var descriptions = elements.Select(DescribeElement).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return descriptions.Length == 0 ? "None" : string.Join("; ", descriptions);
    }

    private static string DescribeElement(AutomationElement? element)
    {
        if (element is null) return "None";
        try
        {
            var name = Text(() => element.Properties.Name.ValueOrDefault);
            var type = Read(() => element.Properties.ControlType.ValueOrDefault, ControlType.Custom).ToString();
            return string.IsNullOrWhiteSpace(name) ? type : $"{type}: {name}";
        }
        catch
        {
            return "Unavailable";
        }
    }

    private static string FormatRelationshipTarget(AccessibilityRelationship relationship) =>
        string.IsNullOrWhiteSpace(relationship.TargetName)
            ? relationship.TargetControlType
            : $"{relationship.TargetControlType}: {relationship.TargetName}";

    private static IEnumerable<AutomationElement> SafeChildren(AutomationElement element)
    {
        try { return element.FindAllChildren(); }
        catch { return []; }
    }

    private static T Read<T>(Func<T> read, T fallback)
    {
        try { return read(); }
        catch { return fallback; }
    }

    private static string Text(Func<string?> read)
    {
        try { return read() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string GetProcessName(int id)
    {
        if (id <= 0) return string.Empty;
        try { return Process.GetProcessById(id).ProcessName; }
        catch { return string.Empty; }
    }

    public void Dispose() => _automation.Dispose();
}

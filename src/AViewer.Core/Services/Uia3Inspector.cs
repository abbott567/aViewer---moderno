using System.Diagnostics;
using System.Windows.Automation;
using AViewer.Core.Models;

namespace AViewer.Core.Services;

public sealed class Uia3Inspector : IAccessibilityInspector
{
    private readonly Ia2Inspector _legacyInspector = new();

    public AccessibilityInspectionSnapshot InspectPoint(int x, int y, int maxDepth = 3)
    {
        AutomationElement? element;
        try { element = AutomationElement.FromPoint(new System.Windows.Point(x, y)); }
        catch { element = null; }

        var legacy = _legacyInspector.InspectTreesPoint(x, y, maxDepth);
        return new AccessibilityInspectionSnapshot(
            element is null ? null : Map(element, 0, NormalizeDepth(maxDepth)),
            legacy.MsaaRoot,
            legacy.Ia2Root);
    }

    public AccessibilityInspectionSnapshot InspectFocused(int maxDepth = 3)
    {
        AutomationElement? element;
        try { element = AutomationElement.FocusedElement; }
        catch { element = null; }

        if (element is null)
        {
            return new AccessibilityInspectionSnapshot(null, null, null);
        }

        var rectangle = Read(() => element.Current.BoundingRectangle, System.Windows.Rect.Empty);
        var x = (int)Math.Round(rectangle.Left + (rectangle.Width / 2));
        var y = (int)Math.Round(rectangle.Top + (rectangle.Height / 2));
        var legacy = _legacyInspector.InspectTreesPoint(x, y, maxDepth);
        return new AccessibilityInspectionSnapshot(
            Map(element, 0, NormalizeDepth(maxDepth)),
            legacy.MsaaRoot,
            legacy.Ia2Root);
    }

    public AccessibilityInspectionSnapshot InspectParent(
        AccessibilityNode selected,
        string api,
        int maxDepth = 3)
    {
        var x = (int)Math.Round(selected.BoundingX + (selected.BoundingWidth / 2));
        var y = (int)Math.Round(selected.BoundingY + (selected.BoundingHeight / 2));
        var legacy = _legacyInspector.InspectParentTreesPoint(x, y, maxDepth);

        AutomationElement? parent = null;
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            parent = TreeWalker.ControlViewWalker.GetParent(element);
        }
        catch { }

        return new AccessibilityInspectionSnapshot(
            parent is null ? null : Map(parent, 0, NormalizeDepth(maxDepth)),
            legacy.MsaaRoot,
            legacy.Ia2Root);
    }

    public AccessibilityInspectionSnapshot InspectComplete(
        AccessibilityNode? selected,
        int maxDepth = 64)
    {
        AutomationElement? element = null;
        var x = 0;
        var y = 0;

        if (selected is not null && selected.BoundingWidth > 0 && selected.BoundingHeight > 0)
        {
            x = (int)Math.Round(selected.BoundingX + (selected.BoundingWidth / 2));
            y = (int)Math.Round(selected.BoundingY + (selected.BoundingHeight / 2));
            try { element = AutomationElement.FromPoint(new System.Windows.Point(x, y)); }
            catch { element = null; }
        }

        if (element is null)
        {
            try { element = AutomationElement.FocusedElement; }
            catch { element = null; }
            if (element is not null)
            {
                var rectangle = Read(() => element.Current.BoundingRectangle, System.Windows.Rect.Empty);
                x = (int)Math.Round(rectangle.Left + (rectangle.Width / 2));
                y = (int)Math.Round(rectangle.Top + (rectangle.Height / 2));
            }
        }

        if (element is null)
        {
            return new AccessibilityInspectionSnapshot(null, null, null);
        }

        var root = FindCompleteRoot(element);
        var legacy = _legacyInspector.InspectCompleteTreesPoint(x, y, maxDepth);
        return new AccessibilityInspectionSnapshot(
            root is null ? null : Map(root, 0, NormalizeDepth(maxDepth)),
            legacy.MsaaRoot,
            legacy.Ia2Root);
    }

    private static int NormalizeDepth(int maxDepth) => maxDepth < 0 ? int.MaxValue : maxDepth;

    private static AutomationElement? FindCompleteRoot(AutomationElement element)
    {
        var processId = Read(() => element.Current.ProcessId, 0);
        var current = element;
        AutomationElement? documentRoot = IsDocument(current) ? current : null;
        AutomationElement? applicationRoot = current;

        while (true)
        {
            AutomationElement? parent;
            try { parent = TreeWalker.ControlViewWalker.GetParent(current); }
            catch { parent = null; }
            if (parent is null || ReferenceEquals(parent, AutomationElement.RootElement)) break;

            var parentProcessId = Read(() => parent.Current.ProcessId, 0);
            if (parentProcessId == 0 ||
                (processId > 0 && parentProcessId > 0 && parentProcessId != processId)) break;

            applicationRoot = parent;
            if (IsDocument(parent)) documentRoot = parent;
            current = parent;
        }

        return documentRoot ?? applicationRoot;
    }

    private static bool IsDocument(AutomationElement element) =>
        Read(() => element.Current.ControlType == ControlType.Document, false);

    private static AccessibilityNode Map(AutomationElement element, int depth, int maxDepth)
    {
        var name = Read(() => element.Current.Name, string.Empty);
        var controlType = Read(() => element.Current.ControlType.ProgrammaticName, "ControlType.Custom")
            .Replace("ControlType.", string.Empty, StringComparison.Ordinal);
        var rectangle = Read(() => element.Current.BoundingRectangle, System.Windows.Rect.Empty);
        var processId = Read(() => element.Current.ProcessId, 0);
        var runtimeId = Read(() => element.GetRuntimeId(), []);
        var id = runtimeId.Length == 0 ? Guid.NewGuid().ToString("N") : string.Join(".", runtimeId);

        var node = new AccessibilityNode
        {
            Api = "UIA",
            Id = id,
            Name = name,
            ControlType = controlType,
            Framework = Read(() => element.Current.FrameworkId, string.Empty),
            AutomationId = Read(() => element.Current.AutomationId, string.Empty),
            ClassName = Read(() => element.Current.ClassName, string.Empty),
            ProcessId = processId,
            ProcessName = ProcessName(processId),
            BoundingRectangle = $"{rectangle.X:0},{rectangle.Y:0} {rectangle.Width:0}x{rectangle.Height:0}",
            BoundingX = rectangle.X,
            BoundingY = rectangle.Y,
            BoundingWidth = rectangle.Width,
            BoundingHeight = rectangle.Height,
            IsEnabled = Read(() => element.Current.IsEnabled, false),
            IsKeyboardFocusable = Read(() => element.Current.IsKeyboardFocusable, false),
            HasKeyboardFocus = Read(() => element.Current.HasKeyboardFocus, false),
            Properties = ReadProperties(element)
        };

        AddRelationship(node, "Labeled by", "UIA LabeledBy / aria-labelledby", ReadElement(element, AutomationElement.LabeledByProperty));
        AddRelationships(node, "Described by", "UIA DescribedBy / aria-describedby", ReadElements(element, AutomationProperty.LookupById(30105)));
        AddRelationships(node, "Controller for", "UIA ControllerFor / aria-controls", ReadElements(element, AutomationProperty.LookupById(30104)));
        AddRelationships(node, "Flows to", "UIA FlowsTo / aria-flowto", ReadElements(element, AutomationProperty.LookupById(30106)));
        AddRelationships(node, "Flows from", "UIA FlowsFrom", ReadElements(element, AutomationProperty.LookupById(30148)));
        AddRelationships(
            node,
            "Column header",
            "UIA TableItem.ColumnHeaderItems",
            ReadElements(element, TableItemPattern.ColumnHeaderItemsProperty));
        AddRelationships(
            node,
            "Row header",
            "UIA TableItem.RowHeaderItems",
            ReadElements(element, TableItemPattern.RowHeaderItemsProperty));
        AddTableItemProperties(node, element);

        foreach (var group in node.Relationships.GroupBy(relationship => relationship.Type))
        {
            node.Properties.Add(new(
                "UIA Relationships",
                group.Key,
                string.Join("; ", group.Select(relationship =>
                    string.IsNullOrWhiteSpace(relationship.TargetName)
                        ? relationship.TargetControlType
                        : $"{relationship.TargetControlType}: {relationship.TargetName}"))));
        }

        if (depth < maxDepth)
        {
            foreach (var child in Children(element))
            {
                node.Children.Add(Map(child, depth + 1, maxDepth));
            }
        }
        return node;
    }

    private static List<AccessibilityProperty> ReadProperties(AutomationElement element)
    {
        var rectangle = Read(() => element.Current.BoundingRectangle, System.Windows.Rect.Empty);
        return
        [
            new("UIA", "Name", Read(() => element.Current.Name, string.Empty)),
            new("UIA", "Control type", Read(() => element.Current.ControlType.ProgrammaticName, string.Empty).Replace("ControlType.", string.Empty)),
            new("UIA", "Localized control type", Read(() => element.Current.LocalizedControlType, string.Empty)),
            new("UIA", "Automation ID", Read(() => element.Current.AutomationId, string.Empty)),
            new("UIA", "Class name", Read(() => element.Current.ClassName, string.Empty)),
            new("UIA", "Framework", Read(() => element.Current.FrameworkId, string.Empty)),
            new("UIA", "Help text", Read(() => element.Current.HelpText, string.Empty)),
            new("UIA", "Access key", Read(() => element.Current.AccessKey, string.Empty)),
            new("UIA", "Accelerator key", Read(() => element.Current.AcceleratorKey, string.Empty)),
            new("UIA", "Item status", Read(() => element.Current.ItemStatus, string.Empty)),
            new("UIA", "Item type", Read(() => element.Current.ItemType, string.Empty)),
            new("UIA", "Bounding rectangle", $"{rectangle.X:0},{rectangle.Y:0} {rectangle.Width:0}x{rectangle.Height:0}"),
            new("UIA", "Enabled", Read(() => element.Current.IsEnabled, false).ToString()),
            new("UIA", "Focusable", Read(() => element.Current.IsKeyboardFocusable, false).ToString()),
            new("UIA", "Focused", Read(() => element.Current.HasKeyboardFocus, false).ToString()),
            new("UIA", "Offscreen", Read(() => element.Current.IsOffscreen, false).ToString()),
            new("UIA", "Password", Read(() => element.Current.IsPassword, false).ToString())
        ];
    }

    private static void AddRelationship(
        AccessibilityNode node,
        string type,
        string source,
        AutomationElement? target)
    {
        if (target is null) return;
        var relationship = MapRelationship(type, source, target);
        if (relationship is not null && !node.Relationships.Any(existing =>
                string.Equals(existing.Type, relationship.Type, StringComparison.OrdinalIgnoreCase) &&
                SameRelationshipTarget(existing, relationship)))
        {
            node.Relationships.Add(relationship);
        }
    }

    private static void AddRelationships(
        AccessibilityNode node,
        string type,
        string source,
        IEnumerable<AutomationElement> targets)
    {
        foreach (var target in targets) AddRelationship(node, type, source, target);
    }

    private static AccessibilityRelationship? MapRelationship(
        string type,
        string source,
        AutomationElement target)
    {
        try
        {
            var rectangle = target.Current.BoundingRectangle;
            var runtimeId = target.GetRuntimeId();
            return new AccessibilityRelationship(
                type,
                source,
                runtimeId.Length == 0 ? string.Empty : string.Join(".", runtimeId),
                target.Current.Name ?? string.Empty,
                target.Current.ControlType.ProgrammaticName.Replace("ControlType.", string.Empty),
                rectangle.X,
                rectangle.Y,
                rectangle.Width,
                rectangle.Height);
        }
        catch { return null; }
    }

    private static AutomationElement? ReadElement(AutomationElement source, AutomationProperty property)
    {
        try
        {
            var value = source.GetCurrentPropertyValue(property, true);
            return value == AutomationElement.NotSupported ? null : value as AutomationElement;
        }
        catch { return null; }
    }

    private static IEnumerable<AutomationElement> ReadElements(AutomationElement source, AutomationProperty property)
    {
        try
        {
            var value = source.GetCurrentPropertyValue(property, true);
            return value switch
            {
                AutomationElement[] elements => elements,
                AutomationElement element => [element],
                _ => []
            };
        }
        catch { return []; }
    }

    private static void AddTableItemProperties(
        AccessibilityNode node,
        AutomationElement element)
    {
        try
        {
            if (!element.TryGetCurrentPattern(TableItemPattern.Pattern, out var patternObject) ||
                patternObject is not TableItemPattern pattern)
            {
                return;
            }

            var current = pattern.Current;
            AddRelationships(
                node,
                "Column header",
                "UIA TableItemPattern.GetColumnHeaderItems",
                current.GetColumnHeaderItems());
            AddRelationships(
                node,
                "Row header",
                "UIA TableItemPattern.GetRowHeaderItems",
                current.GetRowHeaderItems());
            node.Properties.Add(new("UIA Table item", "Row index", current.Row.ToString()));
            node.Properties.Add(new("UIA Table item", "Column index", current.Column.ToString()));
            node.Properties.Add(new("UIA Table item", "Row span", current.RowSpan.ToString()));
            node.Properties.Add(new("UIA Table item", "Column span", current.ColumnSpan.ToString()));

            var grid = current.ContainingGrid;
            if (grid is not null)
            {
                var gridName = Read(() => grid.Current.Name, string.Empty);
                var gridType = Read(
                    () => grid.Current.ControlType.ProgrammaticName,
                    "ControlType.Table").Replace("ControlType.", string.Empty, StringComparison.Ordinal);
                node.Properties.Add(new(
                    "UIA Table item",
                    "Containing grid",
                    string.IsNullOrWhiteSpace(gridName) ? gridType : $"{gridType}: {gridName}"));
            }
        }
        catch
        {
            // Providers may withdraw a pattern while the accessibility tree is changing.
        }
    }

    private static bool SameRelationshipTarget(
        AccessibilityRelationship first,
        AccessibilityRelationship second)
    {
        if (!string.IsNullOrWhiteSpace(first.TargetId) &&
            !string.IsNullOrWhiteSpace(second.TargetId) &&
            string.Equals(first.TargetId, second.TargetId, StringComparison.Ordinal))
        {
            return true;
        }

        const double tolerance = 2;
        return Math.Abs(first.TargetX - second.TargetX) <= tolerance &&
               Math.Abs(first.TargetY - second.TargetY) <= tolerance &&
               Math.Abs(first.TargetWidth - second.TargetWidth) <= tolerance &&
               Math.Abs(first.TargetHeight - second.TargetHeight) <= tolerance;
    }

    private static IEnumerable<AutomationElement> Children(AutomationElement element)
    {
        AutomationElement? child;
        try { child = TreeWalker.ControlViewWalker.GetFirstChild(element); }
        catch { yield break; }
        while (child is not null)
        {
            yield return child;
            try { child = TreeWalker.ControlViewWalker.GetNextSibling(child); }
            catch { yield break; }
        }
    }

    private static T Read<T>(Func<T> read, T fallback)
    {
        try { return read(); }
        catch { return fallback; }
    }

    private static string ProcessName(int id)
    {
        if (id <= 0) return string.Empty;
        try { return Process.GetProcessById(id).ProcessName; }
        catch { return string.Empty; }
    }

    public void Dispose()
    {
        // System.Windows.Automation does not require an owned automation object.
    }
}

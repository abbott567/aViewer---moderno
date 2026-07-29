using AViewer.Core.Models;

namespace AViewer.App;

internal static class ApiPropertyCatalog
{
    public static IReadOnlyList<AccessibilityProperty> All { get; } =
    [
        new("UIA", "Name", string.Empty),
        new("UIA", "Control type", string.Empty),
        new("UIA", "Localized control type", string.Empty),
        new("UIA", "Automation ID", string.Empty),
        new("UIA", "Class name", string.Empty),
        new("UIA", "Framework", string.Empty),
        new("UIA", "Help text", string.Empty),
        new("UIA", "Access key", string.Empty),
        new("UIA", "Accelerator key", string.Empty),
        new("UIA", "Item status", string.Empty),
        new("UIA", "Item type", string.Empty),
        new("UIA", "Bounding rectangle", string.Empty),
        new("UIA", "Enabled", string.Empty),
        new("UIA", "Focusable", string.Empty),
        new("UIA", "Focused", string.Empty),
        new("UIA", "Offscreen", string.Empty),
        new("UIA", "Password", string.Empty),
        new("UIA Table item", "Row index", string.Empty),
        new("UIA Table item", "Column index", string.Empty),
        new("UIA Table item", "Row span", string.Empty),
        new("UIA Table item", "Column span", string.Empty),
        new("UIA Table item", "Containing grid", string.Empty),

        new("MSAA", "Name", string.Empty),
        new("MSAA", "Role", string.Empty),
        new("MSAA", "State", string.Empty),
        new("MSAA", "Value", string.Empty),
        new("MSAA", "Description", string.Empty),
        new("MSAA", "Keyboard shortcut", string.Empty),
        new("MSAA", "Default action", string.Empty),

        new("IA2", "Name", string.Empty),
        new("IA2", "Role", string.Empty),
        new("IA2", "State", string.Empty),
        new("IA2", "Available", string.Empty),
        new("IA2", "Acquisition", string.Empty),
        new("IA2", "Unique ID", string.Empty),
        new("IA2", "Attributes", string.Empty),
        new("IA2", "Extended role", string.Empty),
        new("IA2", "Localized extended role", string.Empty),
        new("IA2", "States", string.Empty),
        new("IA2 Table cell", "Available", string.Empty),
        new("IA2 Table cell", "Row index", string.Empty),
        new("IA2 Table cell", "Column index", string.Empty),
        new("IA2 Table cell", "Row span", string.Empty),
        new("IA2 Table cell", "Column span", string.Empty),

        new("Relationships", "Labeled by", string.Empty),
        new("Relationships", "Labelled by", string.Empty),
        new("Relationships", "Label for", string.Empty),
        new("Relationships", "Described by", string.Empty),
        new("Relationships", "Description for", string.Empty),
        new("Relationships", "Controller for", string.Empty),
        new("Relationships", "Controlled by", string.Empty),
        new("Relationships", "Flows to", string.Empty),
        new("Relationships", "Flows from", string.Empty),
        new("Relationships", "Details", string.Empty),
        new("Relationships", "Error message", string.Empty),
        new("Relationships", "Member of", string.Empty),
        new("Relationships", "Popup for", string.Empty),
        new("Relationships", "Row header", string.Empty),
        new("Relationships", "Column header", string.Empty),
        new("Relationships", "labelledBy", string.Empty),
        new("Relationships", "labelFor", string.Empty),
        new("Relationships", "describedBy", string.Empty),
        new("Relationships", "descriptionFor", string.Empty),
        new("Relationships", "controllerFor", string.Empty),
        new("Relationships", "controlledBy", string.Empty),
        new("Relationships", "flowsTo", string.Empty),
        new("Relationships", "flowsFrom", string.Empty),
        new("Relationships", "errorMessage", string.Empty),
        new("Relationships", "memberOf", string.Empty),
        new("Relationships", "popupFor", string.Empty),
        new("Relationships", "rowHeader", string.Empty),
        new("Relationships", "columnHeader", string.Empty)
    ];

    public static IEnumerable<AccessibilityProperty> ForApi(string api)
    {
        return All.Where(property => api.ToUpperInvariant() switch
        {
            "UIA" => property.Group is "UIA" or "UIA Table item" or "Relationships",
            "MSAA" => property.Group is "MSAA" or "Relationships",
            "IA2" => property.Group is "MSAA" or "IA2" or "IA2 Table cell" or "Relationships",
            _ => true
        });
    }
}

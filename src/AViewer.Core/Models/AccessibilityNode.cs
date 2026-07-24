namespace AViewer.Core.Models;

public sealed record AccessibilityProperty(string Group, string Name, string Value);

public sealed record AccessibilityRelationship(
    string Type,
    string Source,
    string TargetId,
    string TargetName,
    string TargetControlType,
    double TargetX,
    double TargetY,
    double TargetWidth,
    double TargetHeight);

public sealed class AccessibilityNode
{
    public string Api { get; init; } = "UIA";
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ControlType { get; init; }
    public string Framework { get; init; } = string.Empty;
    public string AutomationId { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public int ProcessId { get; init; }
    public string BoundingRectangle { get; init; } = string.Empty;
    public double BoundingX { get; init; }
    public double BoundingY { get; init; }
    public double BoundingWidth { get; init; }
    public double BoundingHeight { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsKeyboardFocusable { get; init; }
    public bool HasKeyboardFocus { get; init; }
    public List<AccessibilityProperty> Properties { get; init; } = [];
    public List<AccessibilityRelationship> Relationships { get; init; } = [];
    public List<AccessibilityNode> Children { get; init; } = [];
}

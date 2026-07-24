namespace AViewer.Core.Models;

internal sealed record Ia2InspectionResult(
    IReadOnlyList<AccessibilityProperty> Properties,
    IReadOnlyList<AccessibilityRelationship> Relationships);

using AViewer.Core.Models;

namespace AViewer.Core.Services;

public interface IAccessibilityInspector : IDisposable
{
    AccessibilityInspectionSnapshot InspectPoint(int x, int y, int maxDepth = 3);
    AccessibilityInspectionSnapshot InspectFocused(int maxDepth = 3);
    AccessibilityInspectionSnapshot InspectParent(AccessibilityNode selected, string api, int maxDepth = 3);
    AccessibilityInspectionSnapshot InspectComplete(AccessibilityNode? selected, int maxDepth = 64);
}

using AViewer.Core.Models;

namespace AViewer.Core.Services;

public interface IAccessibilityInspector : IDisposable
{
    AccessibilityInspectionSnapshot InspectPoint(int x, int y, int maxDepth = 3);
    AccessibilityInspectionSnapshot InspectFocused(int maxDepth = 3);
}

namespace AViewer.Core.Models;

public sealed record AccessibilityInspectionSnapshot(
    AccessibilityNode? UiaRoot,
    AccessibilityNode? MsaaRoot,
    AccessibilityNode? Ia2Root)
{
    public AccessibilityNode? RootFor(string api) => api switch
    {
        "MSAA" => MsaaRoot,
        "IA2" => Ia2Root,
        _ => UiaRoot
    };
}

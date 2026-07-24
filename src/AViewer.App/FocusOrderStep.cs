using AViewer.Core.Models;

namespace AViewer.App;

public sealed record FocusOrderStep(
    int Sequence,
    FocusNavigationKey? NavigationKey,
    AccessibilityNode Element);

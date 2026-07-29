using System.Windows;
using System.Windows.Media;

namespace AViewer.App;

internal static class AViewerOverlayPalette
{
    public static Brush OverlayOutlineBrush =>
        SystemParameters.HighContrast ? SystemColors.WindowBrush : Brushes.Black;

    public static Brush OverlayTextBrush =>
        SystemParameters.HighContrast ? SystemColors.HighlightTextBrush : Brushes.Black;

    public static Brush SequentialNavigationBrush =>
        SystemParameters.HighContrast ? SystemColors.HighlightBrush : Brushes.Gold;

    public static Brush CompositeNavigationBrush =>
        SystemParameters.HighContrast ? SystemColors.HighlightBrush : Brushes.DeepSkyBlue;

    public static Brush CurrentFocusBrush =>
        SystemParameters.HighContrast ? SystemColors.HighlightBrush : Brushes.LimeGreen;

    public static Brush RelationshipSourceBrush =>
        SystemParameters.HighContrast ? SystemColors.HighlightBrush : Brushes.OrangeRed;

    public static Brush RelationshipTargetBrush =>
        SystemParameters.HighContrast ? SystemColors.HighlightBrush : Brushes.DeepSkyBlue;
}

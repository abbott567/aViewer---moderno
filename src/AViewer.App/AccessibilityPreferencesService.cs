using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace AViewer.App;

public sealed class AccessibilityPreferencesService : IDisposable
{
    public event EventHandler? PreferencesChanged;

    public AccessibilityPreferencesService()
    {
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    public bool HighContrast => SystemParameters.HighContrast;

    public bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;


    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        PreferencesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
    }
}

internal static class AccessibilityVisualPalette
{
    public static Brush InspectionBrush => SystemParameters.HighContrast
        ? SystemColors.HighlightBrush
        : Brushes.DodgerBlue;

    public static Brush CurrentFocusBrush => SystemParameters.HighContrast
        ? SystemColors.HighlightBrush
        : Brushes.LimeGreen;

    public static Brush SequentialNavigationBrush => SystemParameters.HighContrast
        ? SystemColors.WindowTextBrush
        : Brushes.Gold;

    public static Brush CompositeNavigationBrush => SystemParameters.HighContrast
        ? SystemColors.HotTrackBrush
        : Brushes.DeepSkyBlue;

    public static Brush RelationshipSourceBrush => SystemParameters.HighContrast
        ? SystemColors.HighlightBrush
        : Brushes.Red;

    public static Brush RelationshipTargetBrush => SystemParameters.HighContrast
        ? SystemColors.HotTrackBrush
        : Brushes.DeepSkyBlue;

    public static Brush OverlayOutlineBrush => SystemParameters.HighContrast
        ? SystemColors.WindowBrush
        : Brushes.Black;

    public static Brush LabelBackgroundBrush => SystemColors.WindowBrush;

    public static Brush LabelForegroundBrush => SystemColors.WindowTextBrush;

    public static Brush LabelBorderBrush => SystemParameters.HighContrast
        ? SystemColors.HighlightBrush
        : Brushes.White;
}

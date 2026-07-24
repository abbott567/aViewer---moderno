using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using AViewer.Core.Models;

namespace AViewer.App;

public partial class RelationshipOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int SmXvirtualscreen = 76;
    private const int SmYvirtualscreen = 77;
    private const int SmCxvirtualscreen = 78;
    private const int SmCyvirtualscreen = 79;

    public RelationshipOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    public void ShowRelationships(AccessibilityNode node)
    {
        OverlayCanvas.Children.Clear();

        var relationships = node.Relationships
            .Where(IsDrawable)
            .ToArray();

        if (relationships.Length == 0 || node.BoundingWidth <= 0 || node.BoundingHeight <= 0)
        {
            HideOverlay();
            return;
        }

        var virtualLeft = GetSystemMetrics(SmXvirtualscreen);
        var virtualTop = GetSystemMetrics(SmYvirtualscreen);
        var virtualWidth = GetSystemMetrics(SmCxvirtualscreen);
        var virtualHeight = GetSystemMetrics(SmCyvirtualscreen);

        Left = virtualLeft;
        Top = virtualTop;
        Width = virtualWidth;
        Height = virtualHeight;

        DrawRectangle(
            node.BoundingX - virtualLeft,
            node.BoundingY - virtualTop,
            node.BoundingWidth,
            node.BoundingHeight,
            AccessibilityVisualPalette.RelationshipSourceBrush,
            3);

        var sourceX = node.BoundingX - virtualLeft + (node.BoundingWidth / 2);
        var sourceY = node.BoundingY - virtualTop + (node.BoundingHeight / 2);

        foreach (var relationship in relationships)
        {
            var targetX = relationship.TargetX - virtualLeft;
            var targetY = relationship.TargetY - virtualTop;
            DrawRectangle(
                targetX,
                targetY,
                relationship.TargetWidth,
                relationship.TargetHeight,
                AccessibilityVisualPalette.RelationshipTargetBrush,
                3);

            var targetCentreX = targetX + (relationship.TargetWidth / 2);
            var targetCentreY = targetY + (relationship.TargetHeight / 2);
            DrawArrow(sourceX, sourceY, targetCentreX, targetCentreY);
            DrawLabel(
                relationship.Type,
                (sourceX + targetCentreX) / 2,
                (sourceY + targetCentreY) / 2);
        }

        if (!IsVisible)
        {
            Show();
        }
    }

    public void HideOverlay()
    {
        OverlayCanvas.Children.Clear();
        if (IsVisible) Hide();
    }

    private static bool IsDrawable(AccessibilityRelationship relationship) =>
        relationship.TargetWidth > 0 &&
        relationship.TargetHeight > 0 &&
        !double.IsNaN(relationship.TargetX) &&
        !double.IsNaN(relationship.TargetY);

    private void DrawRectangle(double x, double y, double width, double height, Brush brush, double thickness)
    {
        var rectangle = new Rectangle
        {
            Width = Math.Max(1, width),
            Height = Math.Max(1, height),
            Stroke = brush,
            StrokeThickness = thickness,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(rectangle, x);
        Canvas.SetTop(rectangle, y);
        OverlayCanvas.Children.Add(rectangle);
    }

    private void DrawArrow(double x1, double y1, double x2, double y2)
    {
        // Draw the black outline first, then the coloured line on top.
        // A 5 px outer stroke around a 3 px inner stroke leaves a 1 px border.
        AddArrowSegment(x1, y1, x2, y2, AccessibilityVisualPalette.OverlayOutlineBrush, 5);
        AddArrowSegment(x1, y1, x2, y2, AccessibilityVisualPalette.SequentialNavigationBrush, 3);

        var angle = Math.Atan2(y2 - y1, x2 - x1);
        const double arrowLength = 12;
        const double arrowAngle = Math.PI / 7;
        AddOutlinedArrowHeadLine(x2, y2, angle + Math.PI - arrowAngle, arrowLength);
        AddOutlinedArrowHeadLine(x2, y2, angle + Math.PI + arrowAngle, arrowLength);
    }

    private void AddOutlinedArrowHeadLine(double x, double y, double angle, double length)
    {
        var endX = x + (Math.Cos(angle) * length);
        var endY = y + (Math.Sin(angle) * length);

        AddArrowSegment(x, y, endX, endY, AccessibilityVisualPalette.OverlayOutlineBrush, 5);
        AddArrowSegment(x, y, endX, endY, AccessibilityVisualPalette.SequentialNavigationBrush, 3);
    }

    private void AddArrowSegment(
        double x1,
        double y1,
        double x2,
        double y2,
        Brush stroke,
        double thickness)
    {
        OverlayCanvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        });
    }

    private void DrawLabel(string text, double x, double y)
    {
        var label = new Border
        {
            Background = AccessibilityVisualPalette.LabelBackgroundBrush,
            BorderBrush = AccessibilityVisualPalette.LabelBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5, 2, 5, 2),
            Child = new TextBlock
            {
                Text = text,
                Foreground = AccessibilityVisualPalette.LabelForegroundBrush,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 220
            },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, x + 6);
        Canvas.SetTop(label, y + 6);
        OverlayCanvas.Children.Add(label);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        styles |= WsExTransparent | WsExToolWindow | WsExNoActivate;
        _ = SetWindowLongPtr(handle, GwlExStyle, new nint(styles));
    }

    private static nint GetWindowLongPtr(nint handle, int index) =>
        nint.Size == 8 ? GetWindowLongPtr64(handle, index) : new nint(GetWindowLong32(handle, index));

    private static nint SetWindowLongPtr(nint handle, int index, nint value) =>
        nint.Size == 8 ? SetWindowLongPtr64(handle, index, value) : new nint(SetWindowLong32(handle, index, value.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(nint handle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern nint GetWindowLongPtr64(nint handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(nint handle, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern nint SetWindowLongPtr64(nint handle, int index, nint value);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}

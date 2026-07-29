using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AViewer.App;

public partial class FocusOrderOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int SmXvirtualscreen = 76;
    private const int SmYvirtualscreen = 77;
    private const int SmCxvirtualscreen = 78;
    private const int SmCyvirtualscreen = 79;
    private const double ConnectorGap = 8;

    public FocusOrderOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    public void ShowPath(IReadOnlyList<FocusOrderStep> steps)
    {
        OverlayCanvas.Children.Clear();
        var drawable = steps.Where(step => IsDrawable(step.Element)).ToArray();
        if (drawable.Length == 0)
        {
            HideOverlay();
            return;
        }

        var virtualLeft = GetSystemMetrics(SmXvirtualscreen);
        var virtualTop = GetSystemMetrics(SmYvirtualscreen);
        Left = virtualLeft;
        Top = virtualTop;
        Width = GetSystemMetrics(SmCxvirtualscreen);
        Height = GetSystemMetrics(SmCyvirtualscreen);

        for (var index = 0; index < drawable.Length; index++)
        {
            var step = drawable[index];
            var element = step.Element;
            var rect = new Rect(
                element.BoundingX - virtualLeft,
                element.BoundingY - virtualTop,
                element.BoundingWidth,
                element.BoundingHeight);
            var isCurrent = index == drawable.Length - 1;

            DrawElementBox(rect.X, rect.Y, rect.Width, rect.Height, isCurrent);
            DrawSequenceBadge(step.Sequence, rect);

            if (index > 0)
            {
                DrawTransition(drawable[index - 1], step, virtualLeft, virtualTop);
            }
        }

        if (!IsVisible)
        {
            Show();
        }
    }

    public void HideOverlay()
    {
        OverlayCanvas.Children.Clear();
        if (IsVisible)
        {
            Hide();
        }
    }

    private void DrawTransition(
        FocusOrderStep previous,
        FocusOrderStep current,
        double virtualLeft,
        double virtualTop)
    {
        var previousRect = ToRect(previous.Element, virtualLeft, virtualTop);
        var currentRect = ToRect(current.Element, virtualLeft, virtualTop);
        var (start, end) = GetConnectorEndpoints(previousRect, currentRect, ConnectorGap);
        var key = current.NavigationKey ?? FocusNavigationKey.Tab;
        var innerBrush = IsArrowKey(key)
            ? AccessibilityVisualPalette.CompositeNavigationBrush
            : AccessibilityVisualPalette.SequentialNavigationBrush;

        DrawOutlinedArrow(start.X, start.Y, end.X, end.Y, innerBrush);
        DrawLabelOutsideElements(KeyLabel(key), start, end, previousRect, currentRect, innerBrush);
    }

    private void DrawElementBox(
        double x,
        double y,
        double width,
        double height,
        bool isCurrent)
    {
        var rectangle = new Rectangle
        {
            Width = Math.Max(1, width),
            Height = Math.Max(1, height),
            Stroke = isCurrent
                ? AccessibilityVisualPalette.CurrentFocusBrush
                : AccessibilityVisualPalette.SequentialNavigationBrush,
            StrokeThickness = isCurrent ? 4 : 3,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(rectangle, x);
        Canvas.SetTop(rectangle, y);
        OverlayCanvas.Children.Add(rectangle);
    }

    private void DrawSequenceBadge(int sequence, Rect elementRect)
    {
        var badge = new Border
        {
            Background = AccessibilityVisualPalette.LabelBackgroundBrush,
            BorderBrush = AccessibilityVisualPalette.LabelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            MinWidth = 22,
            MinHeight = 22,
            Padding = new Thickness(5, 1, 5, 1),
            Child = new TextBlock
            {
                Text = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Foreground = AccessibilityVisualPalette.LabelForegroundBrush,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            IsHitTestVisible = false
        };

        badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = badge.DesiredSize;
        var position = FindOutsideBadgePosition(elementRect, size);
        Canvas.SetLeft(badge, position.X);
        Canvas.SetTop(badge, position.Y);
        OverlayCanvas.Children.Add(badge);
    }

    private Point FindOutsideBadgePosition(Rect elementRect, Size size)
    {
        const double gap = 5;
        var candidates = new[]
        {
            new Point(elementRect.Left - size.Width - gap, elementRect.Top - size.Height - gap),
            new Point(elementRect.Right + gap, elementRect.Top - size.Height - gap),
            new Point(elementRect.Left - size.Width - gap, elementRect.Bottom + gap),
            new Point(elementRect.Right + gap, elementRect.Bottom + gap),
            new Point(elementRect.Left, elementRect.Top - size.Height - gap),
            new Point(elementRect.Left, elementRect.Bottom + gap)
        };

        foreach (var point in candidates)
        {
            var rect = new Rect(point, size);
            if (rect.Left >= 2 && rect.Top >= 2 && rect.Right <= Width - 2 && rect.Bottom <= Height - 2)
            {
                return point;
            }
        }

        var fallback = new Rect(
            elementRect.Right + gap,
            elementRect.Top,
            size.Width,
            size.Height);
        return ClampToCanvas(fallback).TopLeft;
    }

    private void DrawOutlinedArrow(
        double x1,
        double y1,
        double x2,
        double y2,
        Brush innerBrush)
    {
        AddLine(x1, y1, x2, y2, AccessibilityVisualPalette.OverlayOutlineBrush, 5);
        AddLine(x1, y1, x2, y2, innerBrush, 3);

        var angle = Math.Atan2(y2 - y1, x2 - x1);
        const double length = 12;
        const double spread = Math.PI / 7;
        DrawArrowHeadSegment(x2, y2, angle + Math.PI - spread, length, innerBrush);
        DrawArrowHeadSegment(x2, y2, angle + Math.PI + spread, length, innerBrush);
    }

    private void DrawArrowHeadSegment(
        double x,
        double y,
        double angle,
        double length,
        Brush innerBrush)
    {
        var endX = x + (Math.Cos(angle) * length);
        var endY = y + (Math.Sin(angle) * length);
        AddLine(x, y, endX, endY, AccessibilityVisualPalette.OverlayOutlineBrush, 5);
        AddLine(x, y, endX, endY, innerBrush, 3);
    }

    private void AddLine(
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

    private void DrawLabelOutsideElements(
        string text,
        Point start,
        Point end,
        Rect sourceRect,
        Rect targetRect,
        Brush borderBrush)
    {
        var label = new Border
        {
            Background = AccessibilityVisualPalette.LabelBackgroundBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(2),
            Padding = new Thickness(5, 2, 5, 2),
            Child = new TextBlock
            {
                Text = text,
                Foreground = AccessibilityVisualPalette.LabelForegroundBrush,
                FontWeight = FontWeights.SemiBold
            },
            IsHitTestVisible = false
        };

        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = label.DesiredSize;
        var position = FindLabelPlacement(start, end, size, sourceRect, targetRect);
        Canvas.SetLeft(label, position.X);
        Canvas.SetTop(label, position.Y);
        OverlayCanvas.Children.Add(label);
    }

    private Point FindLabelPlacement(
        Point start,
        Point end,
        Size labelSize,
        Rect sourceRect,
        Rect targetRect)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Max(1, Math.Sqrt((dx * dx) + (dy * dy)));
        var perpendicular = new Vector(-dy / length, dx / length);
        var fractions = new[] { 0.5, 0.38, 0.62 };
        var offsets = new[] { 14.0, -14.0, 28.0, -28.0, 42.0, -42.0 };
        var protectedSource = sourceRect;
        var protectedTarget = targetRect;
        protectedSource.Inflate(6, 6);
        protectedTarget.Inflate(6, 6);

        foreach (var fraction in fractions)
        {
            var anchor = new Point(start.X + (dx * fraction), start.Y + (dy * fraction));
            foreach (var offset in offsets)
            {
                var candidate = new Rect(
                    anchor.X + (perpendicular.X * offset) - (labelSize.Width / 2),
                    anchor.Y + (perpendicular.Y * offset) - (labelSize.Height / 2),
                    labelSize.Width,
                    labelSize.Height);
                candidate = ClampToCanvas(candidate);

                if (!candidate.IntersectsWith(protectedSource) &&
                    !candidate.IntersectsWith(protectedTarget))
                {
                    return candidate.TopLeft;
                }
            }
        }

        return ClampToCanvas(new Rect(
            ((start.X + end.X) / 2) + 12,
            ((start.Y + end.Y) / 2) + 12,
            labelSize.Width,
            labelSize.Height)).TopLeft;
    }

    private Rect ClampToCanvas(Rect rect)
    {
        var maxX = Math.Max(0, Width - rect.Width - 2);
        var maxY = Math.Max(0, Height - rect.Height - 2);
        return new Rect(
            Math.Clamp(rect.X, 2, maxX),
            Math.Clamp(rect.Y, 2, maxY),
            rect.Width,
            rect.Height);
    }

    private static (Point Start, Point End) GetConnectorEndpoints(
        Rect sourceRect,
        Rect targetRect,
        double gap)
    {
        var sourceCentre = Centre(sourceRect);
        var targetCentre = Centre(targetRect);
        var vector = targetCentre - sourceCentre;
        var length = vector.Length;

        if (length < 0.001)
        {
            return (
                new Point(sourceRect.Right + gap, sourceCentre.Y),
                new Point(targetRect.Right + gap + 1, targetCentre.Y));
        }

        vector.Normalize();
        var sourceBoundary = BoundaryPoint(sourceRect, targetCentre);
        var targetBoundary = BoundaryPoint(targetRect, sourceCentre);
        return (sourceBoundary + (vector * gap), targetBoundary - (vector * gap));
    }

    private static Point BoundaryPoint(Rect rect, Point toward)
    {
        var centre = Centre(rect);
        var dx = toward.X - centre.X;
        var dy = toward.Y - centre.Y;
        var halfWidth = Math.Max(0.5, rect.Width / 2);
        var halfHeight = Math.Max(0.5, rect.Height / 2);
        var scaleX = Math.Abs(dx) < 0.001 ? double.PositiveInfinity : halfWidth / Math.Abs(dx);
        var scaleY = Math.Abs(dy) < 0.001 ? double.PositiveInfinity : halfHeight / Math.Abs(dy);
        var scale = Math.Min(scaleX, scaleY);
        return new Point(centre.X + (dx * scale), centre.Y + (dy * scale));
    }

    private static Point Centre(Rect rect) =>
        new(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));

    private static Rect ToRect(
        AViewer.Core.Models.AccessibilityNode element,
        double virtualLeft,
        double virtualTop) =>
        new(
            element.BoundingX - virtualLeft,
            element.BoundingY - virtualTop,
            element.BoundingWidth,
            element.BoundingHeight);

    private static bool IsDrawable(AViewer.Core.Models.AccessibilityNode element) =>
        element.BoundingWidth > 0 &&
        element.BoundingHeight > 0 &&
        !double.IsNaN(element.BoundingX) &&
        !double.IsNaN(element.BoundingY);

    private static bool IsArrowKey(FocusNavigationKey key) =>
        key is FocusNavigationKey.ArrowLeft or
            FocusNavigationKey.ArrowRight or
            FocusNavigationKey.ArrowUp or
            FocusNavigationKey.ArrowDown;

    private static string KeyLabel(FocusNavigationKey key) => key switch
    {
        FocusNavigationKey.ShiftTab => "Shift+Tab",
        FocusNavigationKey.ArrowLeft => "Left arrow",
        FocusNavigationKey.ArrowRight => "Right arrow",
        FocusNavigationKey.ArrowUp => "Up arrow",
        FocusNavigationKey.ArrowDown => "Down arrow",
        _ => "Tab"
    };

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
        nint.Size == 8
            ? SetWindowLongPtr64(handle, index, value)
            : new nint(SetWindowLong32(handle, index, value.ToInt32()));

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

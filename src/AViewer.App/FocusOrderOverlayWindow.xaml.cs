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

    public FocusOrderOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    public void ShowPath(IReadOnlyList<FocusOrderStep> steps)
    {
        OverlayCanvas.Children.Clear();

        var drawable = steps
            .Where(step => IsDrawable(step.Element))
            .ToArray();

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

        var rectangles = drawable
            .Select(step => ToRect(step.Element, virtualLeft, virtualTop))
            .ToArray();

        for (var index = 0; index < rectangles.Length; index++)
        {
            DrawElementBox(
                rectangles[index],
                index == rectangles.Length - 1,
                drawable[index].Sequence);
        }

        for (var index = 1; index < rectangles.Length; index++)
        {
            DrawTransition(rectangles[index - 1], rectangles[index]);
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

    private void DrawTransition(Rect previousRect, Rect currentRect)
    {
        if (!OverlayArrowRenderer.TryBuildOrthogonalRoute(
                previousRect,
                currentRect,
                out var route))
        {
            return;
        }

        OverlayArrowRenderer.DrawArrow(
            OverlayCanvas,
            route,
            AViewerOverlayPalette.SequentialNavigationBrush);
    }

    private void DrawElementBox(Rect rect, bool isCurrent, int stopNumber)
    {
        var brush = isCurrent
            ? AViewerOverlayPalette.CurrentFocusBrush
            : AViewerOverlayPalette.SequentialNavigationBrush;
        var rectangle = new Rectangle
        {
            Width = Math.Max(1, rect.Width),
            Height = Math.Max(1, rect.Height),
            Stroke = brush,
            StrokeThickness = isCurrent ? 4 : 3,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(rectangle, rect.X);
        Canvas.SetTop(rectangle, rect.Y);
        OverlayCanvas.Children.Add(rectangle);
        DrawStopNumber(rect, stopNumber, brush);
    }

    private void DrawStopNumber(Rect rect, int stopNumber, Brush brush)
    {
        const double gap = 4;
        const double minimumSize = 24;
        var text = new TextBlock
        {
            Text = stopNumber.ToString(),
            FontWeight = FontWeights.Bold,
            Foreground = AViewerOverlayPalette.OverlayTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            IsHitTestVisible = false
        };
        var badge = new Border
        {
            MinWidth = minimumSize,
            Height = minimumSize,
            Padding = new Thickness(5, 0, 5, 0),
            Background = brush,
            BorderBrush = AViewerOverlayPalette.OverlayOutlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(minimumSize / 2),
            Child = text,
            IsHitTestVisible = false
        };

        badge.Measure(new Size(double.PositiveInfinity, minimumSize));
        var badgeWidth = Math.Max(minimumSize, badge.DesiredSize.Width);
        var x = Math.Max(0, Math.Min(rect.Left, Width - badgeWidth));
        var y = rect.Top - minimumSize - gap;
        if (y < 0)
        {
            y = Math.Min(Math.Max(0, Height - minimumSize), rect.Bottom + gap);
        }

        Canvas.SetLeft(badge, x);
        Canvas.SetTop(badge, y);
        OverlayCanvas.Children.Add(badge);
    }

    private static Rect ToRect(
        AViewer.Core.Models.AccessibilityNode element,
        double virtualLeft,
        double virtualTop) =>
        new(
            element.BoundingX - virtualLeft,
            element.BoundingY - virtualTop,
            element.BoundingWidth,
            element.BoundingHeight);

    private static bool IsDrawable(
        AViewer.Core.Models.AccessibilityNode element) =>
        element.BoundingWidth > 0 &&
        element.BoundingHeight > 0 &&
        !double.IsNaN(element.BoundingX) &&
        !double.IsNaN(element.BoundingY);

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        styles |= WsExTransparent | WsExToolWindow | WsExNoActivate;
        _ = SetWindowLongPtr(handle, GwlExStyle, new nint(styles));
    }

    private static nint GetWindowLongPtr(nint handle, int index) =>
        nint.Size == 8
            ? GetWindowLongPtr64(handle, index)
            : new nint(GetWindowLong32(handle, index));

    private static nint SetWindowLongPtr(
        nint handle,
        int index,
        nint value) =>
        nint.Size == 8
            ? SetWindowLongPtr64(handle, index, value)
            : new nint(SetWindowLong32(handle, index, value.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(nint handle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern nint GetWindowLongPtr64(nint handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(
        nint handle,
        int index,
        int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern nint SetWindowLongPtr64(
        nint handle,
        int index,
        nint value);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}

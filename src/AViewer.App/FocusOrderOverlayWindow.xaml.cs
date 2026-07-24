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
            var x = element.BoundingX - virtualLeft;
            var y = element.BoundingY - virtualTop;
            var isCurrent = index == drawable.Length - 1;

            DrawElementBox(x, y, element.BoundingWidth, element.BoundingHeight, isCurrent);
            DrawSequenceBadge(step.Sequence, x, y);

            if (index == 0)
            {
                continue;
            }

            var previous = drawable[index - 1];
            DrawTransition(previous, step, virtualLeft, virtualTop);
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
        var previousElement = previous.Element;
        var currentElement = current.Element;
        var x1 = previousElement.BoundingX - virtualLeft + (previousElement.BoundingWidth / 2);
        var y1 = previousElement.BoundingY - virtualTop + (previousElement.BoundingHeight / 2);
        var x2 = currentElement.BoundingX - virtualLeft + (currentElement.BoundingWidth / 2);
        var y2 = currentElement.BoundingY - virtualTop + (currentElement.BoundingHeight / 2);
        var key = current.NavigationKey ?? FocusNavigationKey.Tab;
        var innerBrush = IsArrowKey(key)
            ? AccessibilityVisualPalette.CompositeNavigationBrush
            : AccessibilityVisualPalette.SequentialNavigationBrush;

        DrawOutlinedArrow(x1, y1, x2, y2, innerBrush);
        DrawLabel(KeyLabel(key), (x1 + x2) / 2, (y1 + y2) / 2, innerBrush);
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

    private void DrawSequenceBadge(int sequence, double x, double y)
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
        Canvas.SetLeft(badge, x - 10);
        Canvas.SetTop(badge, y - 12);
        OverlayCanvas.Children.Add(badge);
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

    private void DrawLabel(string text, double x, double y, Brush borderBrush)
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
        Canvas.SetLeft(label, x + 6);
        Canvas.SetTop(label, y + 6);
        OverlayCanvas.Children.Add(label);
    }

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

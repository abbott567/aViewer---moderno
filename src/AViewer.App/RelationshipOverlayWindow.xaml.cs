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
    private const double BoundsTolerance = 2;

    public RelationshipOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    public void ShowRelationships(AccessibilityNode node)
    {
        OverlayCanvas.Children.Clear();

        if (!HasDrawableBounds(node))
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

        var sourceRect = new Rect(
            node.BoundingX - virtualLeft,
            node.BoundingY - virtualTop,
            node.BoundingWidth,
            node.BoundingHeight);

        var targets = DeduplicateTargets(node.Relationships.Where(IsDrawable))
            .Select(relationship => new TargetInfo(
                relationship.TargetId,
                new Rect(
                    relationship.TargetX - virtualLeft,
                    relationship.TargetY - virtualTop,
                    relationship.TargetWidth,
                    relationship.TargetHeight)))
            .Where(target => !IsSourceTarget(node, sourceRect, target))
            .ToArray();

        // Do not create a relationship-source-only overlay. If no separate target
        // can be drawn, leave the ordinary inspected-element ring to represent the
        // selected element rather than implying that the relationship was rendered.
        if (targets.Length == 0)
        {
            HideOverlay();
            return;
        }

        DrawRectangle(
            sourceRect,
            AViewerOverlayPalette.RelationshipSourceBrush,
            3);

        // Every target used by an arrow is outlined first. This keeps the visual
        // contract simple: source border + target border + connector for each target.
        foreach (var target in targets)
        {
            DrawRectangle(
                target.Rect,
                AViewerOverlayPalette.RelationshipTargetBrush,
                3);
        }

        DrawRelationshipArrows(sourceRect, targets);

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

    private static IEnumerable<AccessibilityRelationship> DeduplicateTargets(
        IEnumerable<AccessibilityRelationship> relationships)
    {
        var unique = new List<AccessibilityRelationship>();

        foreach (var relationship in relationships)
        {
            var existingIndex = unique.FindIndex(existing =>
                SameTarget(existing, relationship));

            if (existingIndex < 0)
            {
                unique.Add(relationship);
                continue;
            }

            // Cross-API merging can produce more than one rectangle for the same
            // target. Keep the larger usable rectangle rather than whichever API
            // happened to be merged first.
            if (Area(relationship) > Area(unique[existingIndex]))
            {
                unique[existingIndex] = relationship;
            }
        }

        return unique;
    }

    private static bool SameTarget(
        AccessibilityRelationship first,
        AccessibilityRelationship second)
    {
        if (!string.IsNullOrWhiteSpace(first.TargetId) &&
            !string.IsNullOrWhiteSpace(second.TargetId) &&
            string.Equals(
                first.TargetId,
                second.TargetId,
                StringComparison.Ordinal))
        {
            return true;
        }

        const double tolerance = 4;
        return Math.Abs(first.TargetX - second.TargetX) <= tolerance &&
               Math.Abs(first.TargetY - second.TargetY) <= tolerance &&
               Math.Abs(first.TargetWidth - second.TargetWidth) <= tolerance &&
               Math.Abs(first.TargetHeight - second.TargetHeight) <= tolerance;
    }

    private static double Area(AccessibilityRelationship relationship) =>
        relationship.TargetWidth * relationship.TargetHeight;

    private static bool IsDrawable(AccessibilityRelationship relationship) =>
        relationship.TargetWidth > 0 &&
        relationship.TargetHeight > 0 &&
        IsFinite(relationship.TargetX) &&
        IsFinite(relationship.TargetY) &&
        IsFinite(relationship.TargetWidth) &&
        IsFinite(relationship.TargetHeight);

    private static bool HasDrawableBounds(AccessibilityNode node) =>
        node.BoundingWidth > 0 &&
        node.BoundingHeight > 0 &&
        IsFinite(node.BoundingX) &&
        IsFinite(node.BoundingY) &&
        IsFinite(node.BoundingWidth) &&
        IsFinite(node.BoundingHeight);

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool IsSourceTarget(
        AccessibilityNode source,
        Rect sourceRect,
        TargetInfo target)
    {
        if (!string.IsNullOrWhiteSpace(source.Id) &&
            !string.IsNullOrWhiteSpace(target.TargetId) &&
            string.Equals(source.Id, target.TargetId, StringComparison.Ordinal))
        {
            return true;
        }

        return ApproximatelySameRect(sourceRect, target.Rect);
    }

    private static bool ApproximatelySameRect(Rect first, Rect second) =>
        Math.Abs(first.X - second.X) <= BoundsTolerance &&
        Math.Abs(first.Y - second.Y) <= BoundsTolerance &&
        Math.Abs(first.Width - second.Width) <= BoundsTolerance &&
        Math.Abs(first.Height - second.Height) <= BoundsTolerance;

    private void DrawRelationshipArrows(
        Rect sourceRect,
        IReadOnlyList<TargetInfo> targets)
    {
        foreach (var target in targets)
        {
            if (!OverlayArrowRenderer.TryBuildOrthogonalRoute(
                    sourceRect,
                    target.Rect,
                    out var route))
            {
                continue;
            }

            OverlayArrowRenderer.DrawArrow(
                OverlayCanvas,
                route,
                AViewerOverlayPalette.SequentialNavigationBrush);
        }
    }

    private void DrawRectangle(Rect rect, Brush brush, double thickness)
    {
        var rectangle = new Rectangle
        {
            Width = Math.Max(1, rect.Width),
            Height = Math.Max(1, rect.Height),
            Stroke = brush,
            StrokeThickness = thickness,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(rectangle, rect.X);
        Canvas.SetTop(rectangle, rect.Y);
        OverlayCanvas.Children.Add(rectangle);
    }

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

    private sealed record TargetInfo(string TargetId, Rect Rect);
}

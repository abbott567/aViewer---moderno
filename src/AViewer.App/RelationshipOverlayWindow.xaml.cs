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
    private const double ConnectorGap = 10;
    private const double BranchSpacing = 12;

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
            .GroupBy(relationship => NormalizeRelationshipType(relationship.Type), StringComparer.OrdinalIgnoreCase)
            .Select(group => new RelationshipGroup(
                group.Key,
                DeduplicateTargets(group).ToArray()))
            .Where(group => group.Targets.Length > 0)
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

        var sourceRect = new Rect(
            node.BoundingX - virtualLeft,
            node.BoundingY - virtualTop,
            node.BoundingWidth,
            node.BoundingHeight);

        DrawRectangle(
            sourceRect,
            AccessibilityVisualPalette.RelationshipSourceBrush,
            3);

        var occupiedLabels = new List<Rect>();

        foreach (var group in relationships)
        {
            var targets = group.Targets
                .Select(relationship => new TargetInfo(
                    relationship,
                    new Rect(
                        relationship.TargetX - virtualLeft,
                        relationship.TargetY - virtualTop,
                        relationship.TargetWidth,
                        relationship.TargetHeight)))
                .ToArray();

            foreach (var target in targets)
            {
                DrawRectangle(
                    target.Rect,
                    AccessibilityVisualPalette.RelationshipTargetBrush,
                    3);
            }

            DrawRelationshipGroup(group.Type, sourceRect, targets, occupiedLabels);
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

    private static string NormalizeRelationshipType(string? type) =>
        string.IsNullOrWhiteSpace(type) ? "Relationship" : type.Trim();

    private static IEnumerable<AccessibilityRelationship> DeduplicateTargets(
        IEnumerable<AccessibilityRelationship> relationships)
    {
        var unique = new List<AccessibilityRelationship>();

        foreach (var relationship in relationships)
        {
            if (!unique.Any(existing => SameTarget(existing, relationship)))
            {
                unique.Add(relationship);
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
            string.Equals(first.TargetId, second.TargetId, StringComparison.Ordinal))
        {
            return true;
        }

        const double tolerance = 4;
        return Math.Abs(first.TargetX - second.TargetX) <= tolerance &&
               Math.Abs(first.TargetY - second.TargetY) <= tolerance &&
               Math.Abs(first.TargetWidth - second.TargetWidth) <= tolerance &&
               Math.Abs(first.TargetHeight - second.TargetHeight) <= tolerance;
    }

    private static bool IsDrawable(AccessibilityRelationship relationship) =>
        relationship.TargetWidth > 0 &&
        relationship.TargetHeight > 0 &&
        !double.IsNaN(relationship.TargetX) &&
        !double.IsNaN(relationship.TargetY);

    private void DrawRelationshipGroup(
        string relationshipType,
        Rect sourceRect,
        IReadOnlyList<TargetInfo> targets,
        ICollection<Rect> occupiedLabels)
    {
        var side = ChooseSourceSide(sourceRect, targets.Select(target => target.Rect));
        var sourceAnchor = SourceBoundaryPoint(sourceRect, side);
        var sourceExit = MoveOutward(sourceAnchor, side, ConnectorGap);

        var orderedTargets = OrderTargetsForSide(targets, side).ToArray();
        var branchOrigin = MoveOutward(
            sourceExit,
            side,
            Math.Max(18, (orderedTargets.Length - 1) * BranchSpacing / 2));

        for (var index = 0; index < orderedTargets.Length; index++)
        {
            var target = orderedTargets[index];
            var targetAnchor = BoundaryPoint(target.Rect, branchOrigin);
            var targetEnd = MoveTowardOutside(targetAnchor, branchOrigin, ConnectorGap);
            var laneOffset = (index - ((orderedTargets.Length - 1) / 2.0)) * BranchSpacing;
            var route = BuildElbowRoute(
                sourceExit,
                branchOrigin,
                targetEnd,
                side,
                laneOffset,
                sourceRect);

            DrawModernArrow(route);
        }

        DrawSingleGroupLabel(
            relationshipType,
            sourceExit,
            branchOrigin,
            sourceRect,
            targets.Select(target => target.Rect),
            occupiedLabels);
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

    private void DrawModernArrow(IReadOnlyList<Point> points)
    {
        if (points.Count < 2)
        {
            return;
        }

        var outline = new Polyline
        {
            Points = new PointCollection(points),
            Stroke = AccessibilityVisualPalette.OverlayOutlineBrush,
            StrokeThickness = 4,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        };

        var line = new Polyline
        {
            Points = new PointCollection(points),
            Stroke = AccessibilityVisualPalette.SequentialNavigationBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        };

        OverlayCanvas.Children.Add(outline);
        OverlayCanvas.Children.Add(line);

        var tip = points[^1];
        var previous = points[^2];
        DrawFilledArrowHead(previous, tip);
    }

    private void DrawFilledArrowHead(Point previous, Point tip)
    {
        var direction = tip - previous;
        if (direction.Length < 0.001)
        {
            return;
        }

        direction.Normalize();
        var perpendicular = new Vector(-direction.Y, direction.X);
        const double length = 12;
        const double halfWidth = 5;
        var baseCentre = tip - (direction * length);

        var polygon = new Polygon
        {
            Points = new PointCollection
            {
                tip,
                baseCentre + (perpendicular * halfWidth),
                baseCentre - (perpendicular * halfWidth)
            },
            Fill = AccessibilityVisualPalette.SequentialNavigationBrush,
            Stroke = AccessibilityVisualPalette.OverlayOutlineBrush,
            StrokeThickness = 1,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };

        OverlayCanvas.Children.Add(polygon);
    }

    private void DrawSingleGroupLabel(
        string text,
        Point sourceExit,
        Point branchOrigin,
        Rect sourceRect,
        IEnumerable<Rect> targetRects,
        ICollection<Rect> occupiedLabels)
    {
        var label = CreateLabel(text);
        label.Measure(new Size(220, double.PositiveInfinity));
        var size = label.DesiredSize;
        var protectedRects = targetRects.Append(sourceRect).ToArray();
        var labelRect = FindGroupLabelPlacement(
            sourceExit,
            branchOrigin,
            size,
            protectedRects,
            occupiedLabels);

        Canvas.SetLeft(label, labelRect.X);
        Canvas.SetTop(label, labelRect.Y);
        OverlayCanvas.Children.Add(label);
        occupiedLabels.Add(labelRect);
    }

    private static Border CreateLabel(string text) => new()
    {
        Background = AccessibilityVisualPalette.LabelBackgroundBrush,
        BorderBrush = AccessibilityVisualPalette.LabelBorderBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(6, 2, 6, 2),
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

    private Rect FindGroupLabelPlacement(
        Point sourceExit,
        Point branchOrigin,
        Size size,
        IReadOnlyCollection<Rect> protectedRects,
        IEnumerable<Rect> occupiedLabels)
    {
        var segment = branchOrigin - sourceExit;
        var length = Math.Max(1, segment.Length);
        var perpendicular = new Vector(-segment.Y / length, segment.X / length);
        var midpoint = new Point(
            (sourceExit.X + branchOrigin.X) / 2,
            (sourceExit.Y + branchOrigin.Y) / 2);

        var offsets = new[] { 14.0, -14.0, 28.0, -28.0, 42.0, -42.0 };

        foreach (var offset in offsets)
        {
            var candidate = new Rect(
                midpoint.X + (perpendicular.X * offset) - (size.Width / 2),
                midpoint.Y + (perpendicular.Y * offset) - (size.Height / 2),
                size.Width,
                size.Height);

            candidate = ClampToCanvas(candidate);
            if (!protectedRects.Any(rect => Inflate(rect, 6).IntersectsWith(candidate)) &&
                !occupiedLabels.Any(rect => Inflate(rect, 4).IntersectsWith(candidate)))
            {
                return candidate;
            }
        }

        return ClampToCanvas(new Rect(
            branchOrigin.X + 10,
            branchOrigin.Y + 10,
            size.Width,
            size.Height));
    }

    private static IReadOnlyList<Point> BuildElbowRoute(
        Point sourceExit,
        Point branchOrigin,
        Point targetEnd,
        SourceSide side,
        double laneOffset,
        Rect sourceRect)
    {
        var points = new List<Point> { sourceExit };
        var lane = branchOrigin;

        if (side is SourceSide.Left or SourceSide.Right)
        {
            lane.Y += laneOffset;
            var outerX = side == SourceSide.Left
                ? Math.Min(lane.X, sourceRect.Left - ConnectorGap - Math.Abs(laneOffset))
                : Math.Max(lane.X, sourceRect.Right + ConnectorGap + Math.Abs(laneOffset));

            points.Add(new Point(outerX, sourceExit.Y));
            points.Add(new Point(outerX, lane.Y));
            points.Add(new Point(outerX, targetEnd.Y));
        }
        else
        {
            lane.X += laneOffset;
            var outerY = side == SourceSide.Top
                ? Math.Min(lane.Y, sourceRect.Top - ConnectorGap - Math.Abs(laneOffset))
                : Math.Max(lane.Y, sourceRect.Bottom + ConnectorGap + Math.Abs(laneOffset));

            points.Add(new Point(sourceExit.X, outerY));
            points.Add(new Point(lane.X, outerY));
            points.Add(new Point(targetEnd.X, outerY));
        }

        points.Add(targetEnd);
        return RemoveConsecutiveDuplicates(points);
    }

    private static IReadOnlyList<Point> RemoveConsecutiveDuplicates(IEnumerable<Point> points)
    {
        var result = new List<Point>();

        foreach (var point in points)
        {
            if (result.Count == 0 || (point - result[^1]).Length > 0.5)
            {
                result.Add(point);
            }
        }

        return result;
    }

    private static IEnumerable<TargetInfo> OrderTargetsForSide(
        IEnumerable<TargetInfo> targets,
        SourceSide side) =>
        side is SourceSide.Left or SourceSide.Right
            ? targets.OrderBy(target => Centre(target.Rect).Y)
            : targets.OrderBy(target => Centre(target.Rect).X);

    private static SourceSide ChooseSourceSide(Rect sourceRect, IEnumerable<Rect> targets)
    {
        var targetCentres = targets.Select(Centre).ToArray();
        var average = new Point(
            targetCentres.Average(point => point.X),
            targetCentres.Average(point => point.Y));
        var sourceCentre = Centre(sourceRect);
        var dx = average.X - sourceCentre.X;
        var dy = average.Y - sourceCentre.Y;

        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx < 0 ? SourceSide.Left : SourceSide.Right;
        }

        return dy < 0 ? SourceSide.Top : SourceSide.Bottom;
    }

    private static Point SourceBoundaryPoint(Rect rect, SourceSide side) => side switch
    {
        SourceSide.Left => new Point(rect.Left, rect.Top + (rect.Height / 2)),
        SourceSide.Right => new Point(rect.Right, rect.Top + (rect.Height / 2)),
        SourceSide.Top => new Point(rect.Left + (rect.Width / 2), rect.Top),
        _ => new Point(rect.Left + (rect.Width / 2), rect.Bottom)
    };

    private static Point MoveOutward(Point point, SourceSide side, double distance) => side switch
    {
        SourceSide.Left => new Point(point.X - distance, point.Y),
        SourceSide.Right => new Point(point.X + distance, point.Y),
        SourceSide.Top => new Point(point.X, point.Y - distance),
        _ => new Point(point.X, point.Y + distance)
    };

    private static Point MoveTowardOutside(Point targetBoundary, Point toward, double gap)
    {
        var vector = toward - targetBoundary;
        if (vector.Length < 0.001)
        {
            return targetBoundary;
        }

        vector.Normalize();
        return targetBoundary + (vector * gap);
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

    private static Rect Inflate(Rect rect, double amount)
    {
        rect.Inflate(amount, amount);
        return rect;
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

    private sealed record RelationshipGroup(
        string Type,
        AccessibilityRelationship[] Targets);

    private sealed record TargetInfo(
        AccessibilityRelationship Relationship,
        Rect Rect);

    private enum SourceSide
    {
        Left,
        Right,
        Top,
        Bottom
    }
}

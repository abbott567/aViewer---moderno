using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AViewer.App;

internal static class OverlayArrowRenderer
{
    private const double OutlineThickness = 5;
    private const double InnerThickness = 3;
    private const double ArrowHeadLength = 16;
    private const double ArrowHeadHalfWidth = 8;
    private const double InnerArrowHeadLength = 12.5;
    private const double InnerArrowHeadHalfWidth = 5.25;
    private const double EndpointInset = 4;
    private const double MinimumFacingGap = 8;
    private const double OutsideRouteOffset = 12;

    public static void DrawArrow(
        Canvas canvas,
        IReadOnlyList<Point> points,
        Brush innerBrush)
    {
        if (points.Count < 2)
        {
            return;
        }

        var distinctPoints = RemoveConsecutiveDuplicates(points);
        if (distinctPoints.Count < 2)
        {
            return;
        }

        canvas.Children.Add(new Polyline
        {
            Points = new PointCollection(distinctPoints),
            Stroke = AViewerOverlayPalette.OverlayOutlineBrush,
            StrokeThickness = OutlineThickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Flat,
            StrokeEndLineCap = PenLineCap.Flat,
            IsHitTestVisible = false
        });

        canvas.Children.Add(new Polyline
        {
            Points = new PointCollection(distinctPoints),
            Stroke = innerBrush,
            StrokeThickness = InnerThickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Flat,
            StrokeEndLineCap = PenLineCap.Flat,
            IsHitTestVisible = false
        });

        var tip = distinctPoints[^1];
        var previous = distinctPoints[^2];
        DrawFilledArrowHead(canvas, previous, tip, innerBrush);
    }

    /// <summary>
    /// Builds a side-aware orthogonal route that remains in open space outside
    /// the source and target interiors. A target below the source leaves from
    /// the source bottom border and enters the target top border; the equivalent
    /// facing-border rule is used for above, left and right targets.
    /// </summary>
    public static bool TryBuildOrthogonalRoute(
        Rect sourceRect,
        Rect targetRect,
        out IReadOnlyList<Point> route)
    {
        route = Array.Empty<Point>();

        if (!IsDrawable(sourceRect) || !IsDrawable(targetRect) ||
            ApproximatelySameRect(sourceRect, targetRect))
        {
            return false;
        }

        var sourceCentre = Centre(sourceRect);
        var targetCentre = Centre(targetRect);

        // Vertical placement takes precedence. This means a target wholly below
        // the source always starts at the source bottom border, even when it is
        // also offset horizontally.
        if (targetRect.Top >= sourceRect.Bottom)
        {
            var start = new Point(
                ClampToHorizontalEdge(sourceRect, targetCentre.X),
                sourceRect.Bottom);
            var end = new Point(
                ClampToHorizontalEdge(targetRect, sourceCentre.X),
                targetRect.Top);
            var gap = targetRect.Top - sourceRect.Bottom;
            if (gap < MinimumFacingGap)
            {
                route = BuildOutsideVerticalRoute(sourceRect, targetRect, below: true);
                return route.Count >= 2;
            }

            var laneY = sourceRect.Bottom + (gap / 2);
            route = CreateRoute(
                start,
                new Point(start.X, laneY),
                new Point(end.X, laneY),
                end);
            return route.Count >= 2;
        }

        if (targetRect.Bottom <= sourceRect.Top)
        {
            var start = new Point(
                ClampToHorizontalEdge(sourceRect, targetCentre.X),
                sourceRect.Top);
            var end = new Point(
                ClampToHorizontalEdge(targetRect, sourceCentre.X),
                targetRect.Bottom);
            var gap = sourceRect.Top - targetRect.Bottom;
            if (gap < MinimumFacingGap)
            {
                route = BuildOutsideVerticalRoute(sourceRect, targetRect, below: false);
                return route.Count >= 2;
            }

            var laneY = targetRect.Bottom + (gap / 2);
            route = CreateRoute(
                start,
                new Point(start.X, laneY),
                new Point(end.X, laneY),
                end);
            return route.Count >= 2;
        }

        if (targetRect.Left >= sourceRect.Right)
        {
            var start = new Point(
                sourceRect.Right,
                ClampToVerticalEdge(sourceRect, targetCentre.Y));
            var end = new Point(
                targetRect.Left,
                ClampToVerticalEdge(targetRect, sourceCentre.Y));
            var gap = targetRect.Left - sourceRect.Right;
            if (gap < MinimumFacingGap)
            {
                route = BuildOutsideHorizontalRoute(sourceRect, targetRect, toRight: true);
                return route.Count >= 2;
            }

            var laneX = sourceRect.Right + (gap / 2);
            route = CreateRoute(
                start,
                new Point(laneX, start.Y),
                new Point(laneX, end.Y),
                end);
            return route.Count >= 2;
        }

        if (targetRect.Right <= sourceRect.Left)
        {
            var start = new Point(
                sourceRect.Left,
                ClampToVerticalEdge(sourceRect, targetCentre.Y));
            var end = new Point(
                targetRect.Right,
                ClampToVerticalEdge(targetRect, sourceCentre.Y));
            var gap = sourceRect.Left - targetRect.Right;
            if (gap < MinimumFacingGap)
            {
                route = BuildOutsideHorizontalRoute(sourceRect, targetRect, toRight: false);
                return route.Count >= 2;
            }

            var laneX = targetRect.Right + (gap / 2);
            route = CreateRoute(
                start,
                new Point(laneX, start.Y),
                new Point(laneX, end.Y),
                end);
            return route.Count >= 2;
        }

        // There is no honest line-only route between rectangles whose interiors
        // overlap. Drawing one would necessarily cover source or target content.
        return false;
    }

    public static bool TryGetBoundaryEndpoints(
        Rect sourceRect,
        Rect targetRect,
        out Point start,
        out Point end)
    {
        if (TryBuildOrthogonalRoute(sourceRect, targetRect, out var route) &&
            route.Count >= 2)
        {
            start = route[0];
            end = route[^1];
            return true;
        }

        start = default;
        end = default;
        return false;
    }

    public static Point BoundaryPoint(Rect rect, Point toward)
    {
        var centre = Centre(rect);
        var dx = toward.X - centre.X;
        var dy = toward.Y - centre.Y;

        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
        {
            return new Point(rect.Right, centre.Y);
        }

        var halfWidth = Math.Max(0.5, rect.Width / 2);
        var halfHeight = Math.Max(0.5, rect.Height / 2);
        var scaleX = Math.Abs(dx) < 0.001
            ? double.PositiveInfinity
            : halfWidth / Math.Abs(dx);
        var scaleY = Math.Abs(dy) < 0.001
            ? double.PositiveInfinity
            : halfHeight / Math.Abs(dy);
        var scale = Math.Min(scaleX, scaleY);

        return new Point(
            centre.X + (dx * scale),
            centre.Y + (dy * scale));
    }

    public static Point Centre(Rect rect) =>
        new(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));

    private static IReadOnlyList<Point> CreateRoute(params Point[] points) =>
        RemoveConsecutiveDuplicates(points);

    private static IReadOnlyList<Point> BuildOutsideVerticalRoute(
        Rect sourceRect,
        Rect targetRect,
        bool below)
    {
        var unionLeft = Math.Min(sourceRect.Left, targetRect.Left);
        var unionRight = Math.Max(sourceRect.Right, targetRect.Right);
        var rightDistance = (unionRight - sourceRect.Right) +
                            (unionRight - targetRect.Right);
        var leftDistance = (sourceRect.Left - unionLeft) +
                           (targetRect.Left - unionLeft);
        var useRight = rightDistance <= leftDistance;
        var laneX = useRight
            ? unionRight + OutsideRouteOffset
            : unionLeft - OutsideRouteOffset;
        var sourceX = useRight ? sourceRect.Right : sourceRect.Left;
        var targetX = useRight ? targetRect.Right : targetRect.Left;
        var start = new Point(
            sourceX,
            below ? sourceRect.Bottom : sourceRect.Top);
        var end = new Point(
            targetX,
            below ? targetRect.Top : targetRect.Bottom);

        return CreateRoute(
            start,
            new Point(laneX, start.Y),
            new Point(laneX, end.Y),
            end);
    }

    private static IReadOnlyList<Point> BuildOutsideHorizontalRoute(
        Rect sourceRect,
        Rect targetRect,
        bool toRight)
    {
        var unionTop = Math.Min(sourceRect.Top, targetRect.Top);
        var unionBottom = Math.Max(sourceRect.Bottom, targetRect.Bottom);
        var topDistance = (sourceRect.Top - unionTop) +
                          (targetRect.Top - unionTop);
        var bottomDistance = (unionBottom - sourceRect.Bottom) +
                             (unionBottom - targetRect.Bottom);
        var useTop = topDistance <= bottomDistance;
        var laneY = useTop
            ? unionTop - OutsideRouteOffset
            : unionBottom + OutsideRouteOffset;
        var sourceY = useTop ? sourceRect.Top : sourceRect.Bottom;
        var targetY = useTop ? targetRect.Top : targetRect.Bottom;
        var start = new Point(
            toRight ? sourceRect.Right : sourceRect.Left,
            sourceY);
        var end = new Point(
            toRight ? targetRect.Left : targetRect.Right,
            targetY);

        return CreateRoute(
            start,
            new Point(start.X, laneY),
            new Point(end.X, laneY),
            end);
    }

    private static double ClampToHorizontalEdge(Rect rect, double value)
    {
        var inset = Math.Min(EndpointInset, rect.Width / 2);
        return Math.Clamp(value, rect.Left + inset, rect.Right - inset);
    }

    private static double ClampToVerticalEdge(Rect rect, double value)
    {
        var inset = Math.Min(EndpointInset, rect.Height / 2);
        return Math.Clamp(value, rect.Top + inset, rect.Bottom - inset);
    }

    private static bool IsDrawable(Rect rect) =>
        rect.Width > 0 &&
        rect.Height > 0 &&
        IsFinite(rect.X) &&
        IsFinite(rect.Y) &&
        IsFinite(rect.Width) &&
        IsFinite(rect.Height);

    private static bool ApproximatelySameRect(Rect first, Rect second)
    {
        const double tolerance = 0.5;
        return Math.Abs(first.X - second.X) <= tolerance &&
               Math.Abs(first.Y - second.Y) <= tolerance &&
               Math.Abs(first.Width - second.Width) <= tolerance &&
               Math.Abs(first.Height - second.Height) <= tolerance;
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static void DrawFilledArrowHead(
        Canvas canvas,
        Point previous,
        Point tip,
        Brush innerBrush)
    {
        var direction = tip - previous;
        if (direction.Length < 0.001)
        {
            return;
        }

        var finalSegmentLength = direction.Length;
        direction.Normalize();
        var perpendicular = new Vector(-direction.Y, direction.X);

        // Scale to the available final segment so the arrowhead remains entirely
        // in the space before the target border rather than covering its content.
        var scale = Math.Clamp(finalSegmentLength / ArrowHeadLength, 0.2, 1.0);
        var outlineLength = ArrowHeadLength * scale;
        var outlineHalfWidth = ArrowHeadHalfWidth * scale;
        var innerLength = InnerArrowHeadLength * scale;
        var innerHalfWidth = InnerArrowHeadHalfWidth * scale;

        var outlineBaseCentre = tip - (direction * outlineLength);
        canvas.Children.Add(new Polygon
        {
            Points = new PointCollection
            {
                tip,
                outlineBaseCentre + (perpendicular * outlineHalfWidth),
                outlineBaseCentre - (perpendicular * outlineHalfWidth)
            },
            Fill = AViewerOverlayPalette.OverlayOutlineBrush,
            Stroke = null,
            IsHitTestVisible = false
        });

        var innerBaseCentre = tip - (direction * innerLength);
        canvas.Children.Add(new Polygon
        {
            Points = new PointCollection
            {
                tip,
                innerBaseCentre + (perpendicular * innerHalfWidth),
                innerBaseCentre - (perpendicular * innerHalfWidth)
            },
            Fill = innerBrush,
            Stroke = null,
            IsHitTestVisible = false
        });
    }

    private static IReadOnlyList<Point> RemoveConsecutiveDuplicates(
        IReadOnlyList<Point> points)
    {
        var result = new List<Point>(points.Count);

        foreach (var point in points)
        {
            if (result.Count == 0 || (point - result[^1]).Length > 0.5)
            {
                result.Add(point);
            }
        }

        return result;
    }
}

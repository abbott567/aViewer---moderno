import AppKit
import Foundation

/// Builds connector geometry for the overlays.
///
/// A direct port of the Windows build's arrow renderer. It works unchanged
/// because overlay canvases are flipped to match accessibility coordinates:
/// in both worlds `minY` is the top edge and y grows downwards.
enum OverlayArrowRenderer {

    private static let outlineThickness: CGFloat = 5
    private static let innerThickness: CGFloat = 3
    private static let arrowHeadLength: CGFloat = 16
    private static let arrowHeadHalfWidth: CGFloat = 8
    private static let innerArrowHeadLength: CGFloat = 12.5
    private static let innerArrowHeadHalfWidth: CGFloat = 5.25
    private static let endpointInset: CGFloat = 4
    private static let minimumFacingGap: CGFloat = 8
    private static let outsideRouteOffset: CGFloat = 12

    // MARK: - Drawing

    /// Outlined polyline plus a filled head at the final point.
    static func arrowShapes(points: [CGPoint], innerColor: NSColor) -> [OverlayShape] {
        let route = removeConsecutiveDuplicates(points)
        guard route.count >= 2 else { return [] }

        var shapes: [OverlayShape] = [
            .polyline(route, color: OverlayPalette.outline,
                      lineWidth: outlineThickness, roundCaps: false),
            .polyline(route, color: innerColor,
                      lineWidth: innerThickness, roundCaps: false)
        ]
        shapes.append(contentsOf: arrowHeadShapes(
            from: route[route.count - 2], to: route[route.count - 1], innerColor: innerColor))
        return shapes
    }

    /// The simpler straight connector used by the focus-order path, where the
    /// two open-headed strokes read better at a glance than a filled triangle.
    static func straightArrowShapes(
        from start: CGPoint,
        to end: CGPoint,
        innerColor: NSColor
    ) -> [OverlayShape] {
        var shapes: [OverlayShape] = [
            .polyline([start, end], color: OverlayPalette.outline,
                      lineWidth: outlineThickness, roundCaps: true),
            .polyline([start, end], color: innerColor,
                      lineWidth: innerThickness, roundCaps: true)
        ]

        let angle = atan2(end.y - start.y, end.x - start.x)
        let length: CGFloat = 12
        let spread = CGFloat.pi / 7
        for barb in [angle + .pi - spread, angle + .pi + spread] {
            let tip = CGPoint(
                x: end.x + cos(barb) * length,
                y: end.y + sin(barb) * length)
            shapes.append(.polyline([end, tip], color: OverlayPalette.outline,
                                    lineWidth: outlineThickness, roundCaps: true))
            shapes.append(.polyline([end, tip], color: innerColor,
                                    lineWidth: innerThickness, roundCaps: true))
        }
        return shapes
    }

    /// Scales the head to the final segment so it stays in the space before the
    /// target border instead of covering the target's own content.
    private static func arrowHeadShapes(
        from previous: CGPoint,
        to tip: CGPoint,
        innerColor: NSColor
    ) -> [OverlayShape] {
        var direction = CGPoint(x: tip.x - previous.x, y: tip.y - previous.y)
        let segmentLength = hypot(direction.x, direction.y)
        guard segmentLength >= 0.001 else { return [] }
        direction = CGPoint(x: direction.x / segmentLength, y: direction.y / segmentLength)
        let perpendicular = CGPoint(x: -direction.y, y: direction.x)

        let scale = min(max(segmentLength / arrowHeadLength, 0.2), 1.0)
        let outlineLength = arrowHeadLength * scale
        let outlineHalfWidth = arrowHeadHalfWidth * scale
        let innerLength = innerArrowHeadLength * scale
        let innerHalfWidth = innerArrowHeadHalfWidth * scale

        func triangle(length: CGFloat, halfWidth: CGFloat) -> [CGPoint] {
            let base = CGPoint(
                x: tip.x - direction.x * length,
                y: tip.y - direction.y * length)
            return [
                tip,
                CGPoint(x: base.x + perpendicular.x * halfWidth,
                        y: base.y + perpendicular.y * halfWidth),
                CGPoint(x: base.x - perpendicular.x * halfWidth,
                        y: base.y - perpendicular.y * halfWidth)
            ]
        }

        return [
            .polygon(triangle(length: outlineLength, halfWidth: outlineHalfWidth),
                     fill: OverlayPalette.outline, stroke: nil, lineWidth: 0),
            .polygon(triangle(length: innerLength, halfWidth: innerHalfWidth),
                     fill: innerColor, stroke: nil, lineWidth: 0)
        ]
    }

    // MARK: - Routing

    /// Builds a side-aware orthogonal route that stays in open space outside
    /// the source and target interiors. A target below the source leaves the
    /// source bottom border and enters the target top border; the equivalent
    /// facing-border rule applies above, left and right.
    ///
    /// Returns nil when the two rectangles overlap: there is no honest
    /// line-only route between them that would not cover their content.
    static func orthogonalRoute(source: CGRect, target: CGRect) -> [CGPoint]? {
        guard isDrawable(source), isDrawable(target),
              !approximatelyEqual(source, target) else { return nil }

        let sourceCentre = centre(source)
        let targetCentre = centre(target)

        // Vertical placement takes precedence, so a target wholly below the
        // source always starts at the source's bottom border even when it is
        // also offset horizontally.
        if target.minY >= source.maxY {
            let gap = target.minY - source.maxY
            if gap < minimumFacingGap {
                return outsideVerticalRoute(source: source, target: target, below: true)
            }
            let start = CGPoint(x: clampHorizontal(source, targetCentre.x), y: source.maxY)
            let end = CGPoint(x: clampHorizontal(target, sourceCentre.x), y: target.minY)
            let lane = source.maxY + gap / 2
            return removeConsecutiveDuplicates([
                start, CGPoint(x: start.x, y: lane), CGPoint(x: end.x, y: lane), end
            ])
        }

        if target.maxY <= source.minY {
            let gap = source.minY - target.maxY
            if gap < minimumFacingGap {
                return outsideVerticalRoute(source: source, target: target, below: false)
            }
            let start = CGPoint(x: clampHorizontal(source, targetCentre.x), y: source.minY)
            let end = CGPoint(x: clampHorizontal(target, sourceCentre.x), y: target.maxY)
            let lane = target.maxY + gap / 2
            return removeConsecutiveDuplicates([
                start, CGPoint(x: start.x, y: lane), CGPoint(x: end.x, y: lane), end
            ])
        }

        if target.minX >= source.maxX {
            let gap = target.minX - source.maxX
            if gap < minimumFacingGap {
                return outsideHorizontalRoute(source: source, target: target, toRight: true)
            }
            let start = CGPoint(x: source.maxX, y: clampVertical(source, targetCentre.y))
            let end = CGPoint(x: target.minX, y: clampVertical(target, sourceCentre.y))
            let lane = source.maxX + gap / 2
            return removeConsecutiveDuplicates([
                start, CGPoint(x: lane, y: start.y), CGPoint(x: lane, y: end.y), end
            ])
        }

        if target.maxX <= source.minX {
            let gap = source.minX - target.maxX
            if gap < minimumFacingGap {
                return outsideHorizontalRoute(source: source, target: target, toRight: false)
            }
            let start = CGPoint(x: source.minX, y: clampVertical(source, targetCentre.y))
            let end = CGPoint(x: target.maxX, y: clampVertical(target, sourceCentre.y))
            let lane = target.maxX + gap / 2
            return removeConsecutiveDuplicates([
                start, CGPoint(x: lane, y: start.y), CGPoint(x: lane, y: end.y), end
            ])
        }

        return nil
    }

    private static func outsideVerticalRoute(
        source: CGRect,
        target: CGRect,
        below: Bool
    ) -> [CGPoint] {
        let unionLeft = min(source.minX, target.minX)
        let unionRight = max(source.maxX, target.maxX)
        let rightDistance = (unionRight - source.maxX) + (unionRight - target.maxX)
        let leftDistance = (source.minX - unionLeft) + (target.minX - unionLeft)
        let useRight = rightDistance <= leftDistance
        let lane = useRight ? unionRight + outsideRouteOffset : unionLeft - outsideRouteOffset

        let start = CGPoint(
            x: useRight ? source.maxX : source.minX,
            y: below ? source.maxY : source.minY)
        let end = CGPoint(
            x: useRight ? target.maxX : target.minX,
            y: below ? target.minY : target.maxY)

        return removeConsecutiveDuplicates([
            start, CGPoint(x: lane, y: start.y), CGPoint(x: lane, y: end.y), end
        ])
    }

    private static func outsideHorizontalRoute(
        source: CGRect,
        target: CGRect,
        toRight: Bool
    ) -> [CGPoint] {
        let unionTop = min(source.minY, target.minY)
        let unionBottom = max(source.maxY, target.maxY)
        let topDistance = (source.minY - unionTop) + (target.minY - unionTop)
        let bottomDistance = (unionBottom - source.maxY) + (unionBottom - target.maxY)
        let useTop = topDistance <= bottomDistance
        let lane = useTop ? unionTop - outsideRouteOffset : unionBottom + outsideRouteOffset

        let start = CGPoint(
            x: toRight ? source.maxX : source.minX,
            y: useTop ? source.minY : source.maxY)
        let end = CGPoint(
            x: toRight ? target.minX : target.maxX,
            y: useTop ? target.minY : target.maxY)

        return removeConsecutiveDuplicates([
            start, CGPoint(x: start.x, y: lane), CGPoint(x: end.x, y: lane), end
        ])
    }

    // MARK: - Geometry helpers

    static func centre(_ rect: CGRect) -> CGPoint {
        CGPoint(x: rect.midX, y: rect.midY)
    }

    /// Where a ray from the rectangle's centre towards `point` crosses its border.
    static func boundaryPoint(_ rect: CGRect, toward point: CGPoint) -> CGPoint {
        let middle = centre(rect)
        let dx = point.x - middle.x
        let dy = point.y - middle.y
        if abs(dx) < 0.001 && abs(dy) < 0.001 {
            return CGPoint(x: rect.maxX, y: middle.y)
        }
        let halfWidth = max(0.5, rect.width / 2)
        let halfHeight = max(0.5, rect.height / 2)
        let scaleX = abs(dx) < 0.001 ? CGFloat.infinity : halfWidth / abs(dx)
        let scaleY = abs(dy) < 0.001 ? CGFloat.infinity : halfHeight / abs(dy)
        let scale = min(scaleX, scaleY)
        return CGPoint(x: middle.x + dx * scale, y: middle.y + dy * scale)
    }

    private static func clampHorizontal(_ rect: CGRect, _ value: CGFloat) -> CGFloat {
        let inset = min(endpointInset, rect.width / 2)
        return min(max(value, rect.minX + inset), rect.maxX - inset)
    }

    private static func clampVertical(_ rect: CGRect, _ value: CGFloat) -> CGFloat {
        let inset = min(endpointInset, rect.height / 2)
        return min(max(value, rect.minY + inset), rect.maxY - inset)
    }

    private static func isDrawable(_ rect: CGRect) -> Bool {
        rect.width > 0 && rect.height > 0
            && rect.origin.x.isFinite && rect.origin.y.isFinite
            && rect.width.isFinite && rect.height.isFinite
    }

    private static func approximatelyEqual(_ first: CGRect, _ second: CGRect) -> Bool {
        let tolerance: CGFloat = 0.5
        return abs(first.origin.x - second.origin.x) <= tolerance
            && abs(first.origin.y - second.origin.y) <= tolerance
            && abs(first.width - second.width) <= tolerance
            && abs(first.height - second.height) <= tolerance
    }

    static func removeConsecutiveDuplicates(_ points: [CGPoint]) -> [CGPoint] {
        var result: [CGPoint] = []
        for point in points {
            if let last = result.last, hypot(point.x - last.x, point.y - last.y) <= 0.5 {
                continue
            }
            result.append(point)
        }
        return result
    }
}

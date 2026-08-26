import AppKit

/// Draws the recorded focus path across every display.
///
/// Colour meanings are shared with the Windows build: gold for Tab and
/// Shift+Tab transitions, blue for arrow-key transitions inside a composite
/// widget, and a green outline on the most recent stop.
final class FocusOrderOverlayWindow: OverlayWindow {

    private static let connectorGap: CGFloat = 8

    private var canvasSize: CGSize = .zero

    func show(path steps: [FocusOrderStep]) {
        let drawable = steps.filter { $0.element.isDrawable }
        guard !drawable.isEmpty else {
            hideOverlay()
            return
        }

        let origin = coverAllScreens()
        canvasSize = frame.size

        var shapes: [OverlayShape] = []
        var occupiedLabels: [CGRect] = []

        for (index, step) in drawable.enumerated() {
            let rect = localRect(step.element.frame, origin: origin)
            let isCurrent = index == drawable.count - 1

            shapes.append(.rectangle(
                rect,
                color: isCurrent
                    ? OverlayPalette.currentFocus
                    : OverlayPalette.sequentialNavigation,
                lineWidth: isCurrent ? 4 : 3))

            shapes.append(contentsOf: badgeShapes(
                sequence: step.sequence, elementRect: rect, occupied: &occupiedLabels))

            if index > 0 {
                shapes.append(contentsOf: transitionShapes(
                    from: localRect(drawable[index - 1].element.frame, origin: origin),
                    to: rect,
                    key: step.navigationKey,
                    occupied: &occupiedLabels))
            }
        }

        canvas.shapes = shapes
        present()
    }

    // MARK: - Pieces

    private func badgeShapes(
        sequence: Int,
        elementRect: CGRect,
        occupied: inout [CGRect]
    ) -> [OverlayShape] {
        let text = "\(sequence)"
        let size = OverlayCanvasView.labelSize(text)
        let position = badgePosition(elementRect: elementRect, size: size)
        occupied.append(CGRect(origin: position, size: size))
        return [.label(
            text,
            origin: position,
            border: OverlayPalette.labelBorder,
            cornerRadius: min(size.height / 2, 10))]
    }

    /// Prefers the corners just outside the element so the badge never covers
    /// the content it is numbering.
    private func badgePosition(elementRect: CGRect, size: CGSize) -> CGPoint {
        let gap: CGFloat = 5
        let candidates = [
            CGPoint(x: elementRect.minX - size.width - gap, y: elementRect.minY - size.height - gap),
            CGPoint(x: elementRect.maxX + gap, y: elementRect.minY - size.height - gap),
            CGPoint(x: elementRect.minX - size.width - gap, y: elementRect.maxY + gap),
            CGPoint(x: elementRect.maxX + gap, y: elementRect.maxY + gap),
            CGPoint(x: elementRect.minX, y: elementRect.minY - size.height - gap),
            CGPoint(x: elementRect.minX, y: elementRect.maxY + gap)
        ]

        for candidate in candidates {
            let rect = CGRect(origin: candidate, size: size)
            if rect.minX >= 2, rect.minY >= 2,
               rect.maxX <= canvasSize.width - 2, rect.maxY <= canvasSize.height - 2 {
                return candidate
            }
        }

        return clampToCanvas(CGRect(
            x: elementRect.maxX + gap, y: elementRect.minY,
            width: size.width, height: size.height)).origin
    }

    private func transitionShapes(
        from previous: CGRect,
        to current: CGRect,
        key: FocusNavigationKey,
        occupied: inout [CGRect]
    ) -> [OverlayShape] {
        let (start, end) = connectorEndpoints(
            source: previous, target: current, gap: FocusOrderOverlayWindow.connectorGap)
        let colour = key.isArrow
            ? OverlayPalette.compositeNavigation
            : OverlayPalette.sequentialNavigation

        var shapes = OverlayArrowRenderer.straightArrowShapes(
            from: start, to: end, innerColor: colour)

        let text = key.label
        let size = OverlayCanvasView.labelSize(text)
        let position = labelPlacement(
            start: start, end: end, size: size,
            protected: [previous, current], occupied: occupied)
        occupied.append(CGRect(origin: position, size: size))
        shapes.append(.label(text, origin: position, border: colour, cornerRadius: 3))
        return shapes
    }

    /// Searches perpendicular offsets along the connector for a spot that
    /// clears both elements and any label already placed.
    private func labelPlacement(
        start: CGPoint,
        end: CGPoint,
        size: CGSize,
        protected: [CGRect],
        occupied: [CGRect]
    ) -> CGPoint {
        let dx = end.x - start.x
        let dy = end.y - start.y
        let length = max(1, hypot(dx, dy))
        let perpendicular = CGPoint(x: -dy / length, y: dx / length)
        let protectedRects = protected.map { $0.insetBy(dx: -6, dy: -6) }

        for fraction in [0.5, 0.38, 0.62] as [CGFloat] {
            let anchor = CGPoint(x: start.x + dx * fraction, y: start.y + dy * fraction)
            for offset in [14.0, -14.0, 28.0, -28.0, 42.0, -42.0] as [CGFloat] {
                let candidate = clampToCanvas(CGRect(
                    x: anchor.x + perpendicular.x * offset - size.width / 2,
                    y: anchor.y + perpendicular.y * offset - size.height / 2,
                    width: size.width,
                    height: size.height))

                let clashes = protectedRects.contains { $0.intersects(candidate) }
                    || occupied.contains { $0.insetBy(dx: -4, dy: -4).intersects(candidate) }
                if !clashes { return candidate.origin }
            }
        }

        return clampToCanvas(CGRect(
            x: (start.x + end.x) / 2 + 12,
            y: (start.y + end.y) / 2 + 12,
            width: size.width,
            height: size.height)).origin
    }

    private func connectorEndpoints(
        source: CGRect,
        target: CGRect,
        gap: CGFloat
    ) -> (CGPoint, CGPoint) {
        let sourceCentre = OverlayArrowRenderer.centre(source)
        let targetCentre = OverlayArrowRenderer.centre(target)
        let dx = targetCentre.x - sourceCentre.x
        let dy = targetCentre.y - sourceCentre.y
        let length = hypot(dx, dy)

        guard length >= 0.001 else {
            return (
                CGPoint(x: source.maxX + gap, y: sourceCentre.y),
                CGPoint(x: target.maxX + gap + 1, y: targetCentre.y))
        }

        let unit = CGPoint(x: dx / length, y: dy / length)
        let sourceBoundary = OverlayArrowRenderer.boundaryPoint(source, toward: targetCentre)
        let targetBoundary = OverlayArrowRenderer.boundaryPoint(target, toward: sourceCentre)
        return (
            CGPoint(x: sourceBoundary.x + unit.x * gap, y: sourceBoundary.y + unit.y * gap),
            CGPoint(x: targetBoundary.x - unit.x * gap, y: targetBoundary.y - unit.y * gap))
    }

    // MARK: - Helpers

    private func localRect(_ frame: CGRect, origin: CGPoint) -> CGRect {
        CGRect(
            x: frame.origin.x - origin.x,
            y: frame.origin.y - origin.y,
            width: frame.width,
            height: frame.height)
    }

    private func clampToCanvas(_ rect: CGRect) -> CGRect {
        let maxX = max(0, canvasSize.width - rect.width - 2)
        let maxY = max(0, canvasSize.height - rect.height - 2)
        return CGRect(
            x: min(max(rect.origin.x, 2), maxX),
            y: min(max(rect.origin.y, 2), maxY),
            width: rect.width,
            height: rect.height)
    }
}

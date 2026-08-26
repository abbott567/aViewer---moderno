import AppKit

/// Draws the selected element's relationships as labelled connectors.
///
/// Relationships of the same type share one label and fan out from a single
/// exit point on the source, so a multi-target `aria-labelledby` reads as one
/// relationship with several targets rather than as several unrelated arrows.
final class RelationshipOverlayWindow: OverlayWindow {

    private static let connectorGap: CGFloat = 10
    private static let branchSpacing: CGFloat = 12

    private enum SourceSide { case left, right, top, bottom }

    private var canvasSize: CGSize = .zero

    func show(relationshipsOf node: AccessibilityNode) {
        let groups = groupedRelationships(node)
        guard !groups.isEmpty, node.isDrawable else {
            hideOverlay()
            return
        }

        let origin = coverAllScreens()
        canvasSize = frame.size

        let sourceRect = localRect(node.frame, origin: origin)
        var shapes: [OverlayShape] = [
            .rectangle(sourceRect, color: OverlayPalette.relationshipSource, lineWidth: 3)
        ]
        var occupiedLabels: [CGRect] = []

        for group in groups {
            let targetRects = group.targets.map { localRect($0.targetFrame, origin: origin) }
            for rect in targetRects {
                shapes.append(.rectangle(
                    rect, color: OverlayPalette.relationshipTarget, lineWidth: 3))
            }
            shapes.append(contentsOf: groupShapes(
                type: group.type,
                sourceRect: sourceRect,
                targetRects: targetRects,
                occupied: &occupiedLabels))
        }

        canvas.shapes = shapes
        present()
    }

    // MARK: - Grouping

    private struct RelationshipGroup {
        let type: String
        let targets: [AccessibilityRelationship]
    }

    private func groupedRelationships(_ node: AccessibilityNode) -> [RelationshipGroup] {
        let drawable = node.relationships.filter {
            $0.targetFrame.width > 0 && $0.targetFrame.height > 0
                && $0.targetFrame.origin.x.isFinite && $0.targetFrame.origin.y.isFinite
        }

        var order: [String] = []
        var buckets: [String: [AccessibilityRelationship]] = [:]
        for relationship in drawable {
            let type = relationship.type.trimmingCharacters(in: .whitespaces)
            let key = type.isEmpty ? "Relationship" : type
            if buckets[key] == nil { order.append(key) }
            buckets[key, default: []].append(relationship)
        }

        return order.compactMap { key in
            let unique = deduplicate(buckets[key] ?? [])
            return unique.isEmpty ? nil : RelationshipGroup(type: key, targets: unique)
        }
    }

    /// The same target can arrive from more than one attribute; keep it once.
    private func deduplicate(
        _ relationships: [AccessibilityRelationship]
    ) -> [AccessibilityRelationship] {
        var unique: [AccessibilityRelationship] = []
        for relationship in relationships
        where !unique.contains(where: { sameTarget($0, relationship) }) {
            unique.append(relationship)
        }
        return unique
    }

    private func sameTarget(
        _ first: AccessibilityRelationship,
        _ second: AccessibilityRelationship
    ) -> Bool {
        if !first.targetId.isEmpty, first.targetId == second.targetId { return true }
        let tolerance: CGFloat = 4
        return abs(first.targetFrame.origin.x - second.targetFrame.origin.x) <= tolerance
            && abs(first.targetFrame.origin.y - second.targetFrame.origin.y) <= tolerance
            && abs(first.targetFrame.width - second.targetFrame.width) <= tolerance
            && abs(first.targetFrame.height - second.targetFrame.height) <= tolerance
    }

    // MARK: - Drawing

    private func groupShapes(
        type: String,
        sourceRect: CGRect,
        targetRects: [CGRect],
        occupied: inout [CGRect]
    ) -> [OverlayShape] {
        let side = chooseSourceSide(sourceRect: sourceRect, targets: targetRects)
        let sourceAnchor = boundaryPoint(sourceRect, side: side)
        let sourceExit = move(sourceAnchor, side: side, by: RelationshipOverlayWindow.connectorGap)

        let ordered = orderTargets(targetRects, side: side)
        let branchOrigin = move(
            sourceExit,
            side: side,
            by: max(18, CGFloat(ordered.count - 1) * RelationshipOverlayWindow.branchSpacing / 2))

        var shapes: [OverlayShape] = []
        for (index, target) in ordered.enumerated() {
            let targetAnchor = OverlayArrowRenderer.boundaryPoint(target, toward: branchOrigin)
            let targetEnd = moveToward(
                targetAnchor, towards: branchOrigin, by: RelationshipOverlayWindow.connectorGap)
            let laneOffset = (CGFloat(index) - CGFloat(ordered.count - 1) / 2)
                * RelationshipOverlayWindow.branchSpacing
            let route = elbowRoute(
                sourceExit: sourceExit,
                branchOrigin: branchOrigin,
                targetEnd: targetEnd,
                side: side,
                laneOffset: laneOffset,
                sourceRect: sourceRect)
            shapes.append(contentsOf: OverlayArrowRenderer.arrowShapes(
                points: route, innerColor: OverlayPalette.sequentialNavigation))
        }

        let size = OverlayCanvasView.labelSize(type)
        let position = groupLabelPlacement(
            sourceExit: sourceExit,
            branchOrigin: branchOrigin,
            size: size,
            protected: targetRects + [sourceRect],
            occupied: occupied)
        occupied.append(CGRect(origin: position, size: size))
        shapes.append(.label(
            type, origin: position, border: OverlayPalette.labelBorder, cornerRadius: 3))
        return shapes
    }

    /// Routes out of the chosen source side, along a shared lane clear of the
    /// source, then in to the target.
    private func elbowRoute(
        sourceExit: CGPoint,
        branchOrigin: CGPoint,
        targetEnd: CGPoint,
        side: SourceSide,
        laneOffset: CGFloat,
        sourceRect: CGRect
    ) -> [CGPoint] {
        var points = [sourceExit]
        let gap = RelationshipOverlayWindow.connectorGap

        switch side {
        case .left, .right:
            let laneY = branchOrigin.y + laneOffset
            let outerX = side == .left
                ? min(branchOrigin.x, sourceRect.minX - gap - abs(laneOffset))
                : max(branchOrigin.x, sourceRect.maxX + gap + abs(laneOffset))
            points.append(CGPoint(x: outerX, y: sourceExit.y))
            points.append(CGPoint(x: outerX, y: laneY))
            points.append(CGPoint(x: outerX, y: targetEnd.y))
        case .top, .bottom:
            let laneX = branchOrigin.x + laneOffset
            let outerY = side == .top
                ? min(branchOrigin.y, sourceRect.minY - gap - abs(laneOffset))
                : max(branchOrigin.y, sourceRect.maxY + gap + abs(laneOffset))
            points.append(CGPoint(x: sourceExit.x, y: outerY))
            points.append(CGPoint(x: laneX, y: outerY))
            points.append(CGPoint(x: targetEnd.x, y: outerY))
        }

        points.append(targetEnd)
        return OverlayArrowRenderer.removeConsecutiveDuplicates(points)
    }

    private func groupLabelPlacement(
        sourceExit: CGPoint,
        branchOrigin: CGPoint,
        size: CGSize,
        protected: [CGRect],
        occupied: [CGRect]
    ) -> CGPoint {
        let dx = branchOrigin.x - sourceExit.x
        let dy = branchOrigin.y - sourceExit.y
        let length = max(1, hypot(dx, dy))
        let perpendicular = CGPoint(x: -dy / length, y: dx / length)
        let midpoint = CGPoint(
            x: (sourceExit.x + branchOrigin.x) / 2,
            y: (sourceExit.y + branchOrigin.y) / 2)

        for offset in [14.0, -14.0, 28.0, -28.0, 42.0, -42.0] as [CGFloat] {
            let candidate = clampToCanvas(CGRect(
                x: midpoint.x + perpendicular.x * offset - size.width / 2,
                y: midpoint.y + perpendicular.y * offset - size.height / 2,
                width: size.width,
                height: size.height))

            let clashes = protected.contains { $0.insetBy(dx: -6, dy: -6).intersects(candidate) }
                || occupied.contains { $0.insetBy(dx: -4, dy: -4).intersects(candidate) }
            if !clashes { return candidate.origin }
        }

        return clampToCanvas(CGRect(
            x: branchOrigin.x + 10, y: branchOrigin.y + 10,
            width: size.width, height: size.height)).origin
    }

    // MARK: - Side selection

    private func chooseSourceSide(sourceRect: CGRect, targets: [CGRect]) -> SourceSide {
        guard !targets.isEmpty else { return .right }
        let averageX = targets.map(\.midX).reduce(0, +) / CGFloat(targets.count)
        let averageY = targets.map(\.midY).reduce(0, +) / CGFloat(targets.count)
        let dx = averageX - sourceRect.midX
        let dy = averageY - sourceRect.midY

        if abs(dx) >= abs(dy) { return dx < 0 ? .left : .right }
        return dy < 0 ? .top : .bottom
    }

    private func orderTargets(_ targets: [CGRect], side: SourceSide) -> [CGRect] {
        switch side {
        case .left, .right: return targets.sorted { $0.midY < $1.midY }
        case .top, .bottom: return targets.sorted { $0.midX < $1.midX }
        }
    }

    private func boundaryPoint(_ rect: CGRect, side: SourceSide) -> CGPoint {
        switch side {
        case .left: return CGPoint(x: rect.minX, y: rect.midY)
        case .right: return CGPoint(x: rect.maxX, y: rect.midY)
        case .top: return CGPoint(x: rect.midX, y: rect.minY)
        case .bottom: return CGPoint(x: rect.midX, y: rect.maxY)
        }
    }

    private func move(_ point: CGPoint, side: SourceSide, by distance: CGFloat) -> CGPoint {
        switch side {
        case .left: return CGPoint(x: point.x - distance, y: point.y)
        case .right: return CGPoint(x: point.x + distance, y: point.y)
        case .top: return CGPoint(x: point.x, y: point.y - distance)
        case .bottom: return CGPoint(x: point.x, y: point.y + distance)
        }
    }

    private func moveToward(
        _ point: CGPoint,
        towards destination: CGPoint,
        by gap: CGFloat
    ) -> CGPoint {
        let dx = destination.x - point.x
        let dy = destination.y - point.y
        let length = hypot(dx, dy)
        guard length >= 0.001 else { return point }
        return CGPoint(x: point.x + dx / length * gap, y: point.y + dy / length * gap)
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

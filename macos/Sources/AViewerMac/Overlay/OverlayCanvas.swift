import AppKit

/// A single primitive on an overlay.
///
/// Coordinates are in the canvas's own space, which is flipped (y grows
/// downwards) to match accessibility screen coordinates. Keeping the canvas
/// flipped is what allows the routing and placement geometry to be a direct
/// port of the Windows build rather than a mirror image of it.
enum OverlayShape {
    case rectangle(CGRect, color: NSColor, lineWidth: CGFloat)
    case polyline([CGPoint], color: NSColor, lineWidth: CGFloat, roundCaps: Bool)
    case polygon([CGPoint], fill: NSColor, stroke: NSColor?, lineWidth: CGFloat)
    case label(String, origin: CGPoint, border: NSColor, cornerRadius: CGFloat)
}

/// Renders a flat list of shapes. There is no hit testing and no interaction:
/// overlays are strictly observational.
final class OverlayCanvasView: NSView {

    static let labelFont = NSFont.systemFont(ofSize: 12, weight: .semibold)
    static let labelPadding = NSSize(width: 6, height: 3)

    var shapes: [OverlayShape] = [] {
        didSet { needsDisplay = true }
    }

    override var isFlipped: Bool { true }

    override func hitTest(_ point: NSPoint) -> NSView? { nil }

    override func draw(_ dirtyRect: NSRect) {
        for shape in shapes {
            switch shape {
            case let .rectangle(rect, color, lineWidth):
                drawRectangle(rect, color: color, lineWidth: lineWidth)
            case let .polyline(points, color, lineWidth, roundCaps):
                drawPolyline(points, color: color, lineWidth: lineWidth, roundCaps: roundCaps)
            case let .polygon(points, fill, stroke, lineWidth):
                drawPolygon(points, fill: fill, stroke: stroke, lineWidth: lineWidth)
            case let .label(text, origin, border, cornerRadius):
                drawLabel(text, origin: origin, border: border, cornerRadius: cornerRadius)
            }
        }
    }

    // MARK: - Primitives

    private func drawRectangle(_ rect: CGRect, color: NSColor, lineWidth: CGFloat) {
        guard rect.width > 0, rect.height > 0 else { return }
        let path = NSBezierPath(rect: rect.insetBy(dx: lineWidth / 2, dy: lineWidth / 2))
        path.lineWidth = lineWidth
        color.setStroke()
        path.stroke()
    }

    private func drawPolyline(
        _ points: [CGPoint],
        color: NSColor,
        lineWidth: CGFloat,
        roundCaps: Bool
    ) {
        guard points.count >= 2 else { return }
        let path = NSBezierPath()
        path.move(to: points[0])
        for point in points.dropFirst() { path.line(to: point) }
        path.lineWidth = lineWidth
        path.lineJoinStyle = .round
        path.lineCapStyle = roundCaps ? .round : .butt
        color.setStroke()
        path.stroke()
    }

    private func drawPolygon(
        _ points: [CGPoint],
        fill: NSColor,
        stroke: NSColor?,
        lineWidth: CGFloat
    ) {
        guard points.count >= 3 else { return }
        let path = NSBezierPath()
        path.move(to: points[0])
        for point in points.dropFirst() { path.line(to: point) }
        path.close()
        fill.setFill()
        path.fill()
        if let stroke, lineWidth > 0 {
            path.lineWidth = lineWidth
            path.lineJoinStyle = .round
            stroke.setStroke()
            path.stroke()
        }
    }

    private func drawLabel(
        _ text: String,
        origin: CGPoint,
        border: NSColor,
        cornerRadius: CGFloat
    ) {
        let size = OverlayCanvasView.labelSize(text)
        let rect = CGRect(origin: origin, size: size)
        let path = NSBezierPath(roundedRect: rect, xRadius: cornerRadius, yRadius: cornerRadius)
        OverlayPalette.labelBackground.setFill()
        path.fill()
        path.lineWidth = 2
        border.setStroke()
        path.stroke()

        let textOrigin = CGPoint(
            x: rect.origin.x + OverlayCanvasView.labelPadding.width,
            y: rect.origin.y + OverlayCanvasView.labelPadding.height)
        OverlayCanvasView.attributedLabel(text).draw(at: textOrigin)
    }

    // MARK: - Measurement

    static func attributedLabel(_ text: String) -> NSAttributedString {
        NSAttributedString(string: text, attributes: [
            .font: labelFont,
            .foregroundColor: OverlayPalette.labelForeground
        ])
    }

    /// Padded size of a label, used by the placement search before drawing.
    static func labelSize(_ text: String) -> CGSize {
        let textSize = attributedLabel(text).size()
        return CGSize(
            width: ceil(textSize.width) + labelPadding.width * 2,
            height: ceil(textSize.height) + labelPadding.height * 2)
    }
}

/// Base class for the three overlays: transparent, click-through, above
/// everything, and present on every space including over full-screen apps.
class OverlayWindow: NSWindow {

    let canvas = OverlayCanvasView()

    init() {
        super.init(
            contentRect: NSRect(x: 0, y: 0, width: 1, height: 1),
            styleMask: [.borderless],
            backing: .buffered,
            defer: false)

        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        ignoresMouseEvents = true
        level = .screenSaver
        collectionBehavior = [
            .canJoinAllSpaces, .stationary, .fullScreenAuxiliary, .ignoresCycle
        ]
        isReleasedWhenClosed = false
        // The overlay is decoration, not content: keep it out of the
        // accessibility tree so it can never appear in its own captures.
        setAccessibilityElement(false)
        contentView = canvas
    }

    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }

    /// Positions the window over the union of all displays and returns the
    /// AX-space origin that canvas coordinates are relative to.
    @discardableResult
    func coverAllScreens() -> CGPoint {
        let virtual = ScreenGeometry.virtualFrameAX
        setFrame(ScreenGeometry.cocoaRect(fromAX: virtual), display: false)
        return virtual.origin
    }

    func present() {
        if !isVisible { orderFrontRegardless() }
    }

    func hideOverlay() {
        canvas.shapes = []
        if isVisible { orderOut(nil) }
    }
}

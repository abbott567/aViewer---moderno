import AppKit

/// Draws a ring around the element currently being inspected.
///
/// The window is sized to the element rather than to the whole desktop, so it
/// never covers more of the screen than the ring itself occupies.
final class FocusRingWindow: OverlayWindow {

    private static let padding: CGFloat = 3

    /// - Parameter frame: element bounds in accessibility coordinates.
    func show(around frame: CGRect) {
        guard frame.width > 0, frame.height > 0,
              frame.origin.x.isFinite, frame.origin.y.isFinite else {
            hideOverlay()
            return
        }

        let padded = frame.insetBy(dx: -FocusRingWindow.padding, dy: -FocusRingWindow.padding)
        setFrame(ScreenGeometry.cocoaRect(fromAX: padded), display: false)

        let thickness = OverlayPalette.ringThickness
        canvas.shapes = [
            .rectangle(
                CGRect(origin: .zero, size: padded.size),
                color: OverlayPalette.inspection,
                lineWidth: thickness)
        ]
        present()
    }
}

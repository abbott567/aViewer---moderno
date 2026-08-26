import AppKit
import Foundation

/// Converts between accessibility screen coordinates and Cocoa screen
/// coordinates.
///
/// AX reports positions with the origin at the top-left of the primary display
/// and y growing downwards. Cocoa places the origin at the bottom-left of the
/// primary display with y growing upwards. Every frame captured from AX is kept
/// in AX coordinates for as long as possible — including all overlay geometry,
/// which is why the arrow routing ports from the Windows build unchanged — and
/// converted only when an `NSWindow` is actually positioned.
enum ScreenGeometry {

    /// Height of the primary display, which is the axis the two coordinate
    /// systems are mirrored about.
    static var primaryHeight: CGFloat {
        NSScreen.screens.first?.frame.height ?? 0
    }

    static func cocoaRect(fromAX rect: CGRect) -> NSRect {
        NSRect(
            x: rect.origin.x,
            y: primaryHeight - rect.origin.y - rect.height,
            width: rect.width,
            height: rect.height)
    }

    static func axRect(fromCocoa rect: NSRect) -> CGRect {
        CGRect(
            x: rect.origin.x,
            y: primaryHeight - rect.origin.y - rect.height,
            width: rect.width,
            height: rect.height)
    }

    /// The union of every attached display, in AX coordinates. This is the
    /// equivalent of the Windows virtual screen the full-screen overlays cover.
    static var virtualFrameAX: CGRect {
        let screens = NSScreen.screens
        guard let first = screens.first else { return .zero }
        var union = axRect(fromCocoa: first.frame)
        for screen in screens.dropFirst() {
            union = union.union(axRect(fromCocoa: screen.frame))
        }
        return union
    }

    /// Current pointer position in AX coordinates, ready to pass straight to
    /// `AXUIElementCopyElementAtPosition`.
    static var cursorLocationAX: CGPoint {
        let cocoa = NSEvent.mouseLocation
        return CGPoint(x: cocoa.x, y: primaryHeight - cocoa.y)
    }
}

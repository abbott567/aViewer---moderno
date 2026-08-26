import AppKit

/// Watches for navigation key presses in *other* applications.
///
/// This is the macOS counterpart to the Windows low-level keyboard hook. A
/// global `NSEvent` monitor sees only events delivered elsewhere, which is
/// exactly the semantics focus-order recording wants: key presses inside
/// aViewer itself must not be recorded as focus stops. It observes without
/// consuming, so the inspected application behaves normally.
final class GlobalFocusKeyMonitor {

    private enum KeyCode {
        static let tab: UInt16 = 48
        static let left: UInt16 = 123
        static let right: UInt16 = 124
        static let down: UInt16 = 125
        static let up: UInt16 = 126
    }

    private var monitor: Any?

    var onNavigationKey: ((FocusNavigationKey) -> Void)?

    var isRunning: Bool { monitor != nil }

    /// - Returns: false when accessibility access has not been granted, in
    ///   which case the system silently delivers no events.
    @discardableResult
    func start() -> Bool {
        guard monitor == nil else { return true }
        guard AXPermissions.isTrusted else { return false }

        monitor = NSEvent.addGlobalMonitorForEvents(matching: [.keyUp]) { [weak self] event in
            guard let self, let key = GlobalFocusKeyMonitor.navigationKey(for: event) else { return }
            self.onNavigationKey?(key)
        }
        return monitor != nil
    }

    func stop() {
        guard let monitor else { return }
        NSEvent.removeMonitor(monitor)
        self.monitor = nil
    }

    deinit { stop() }

    private static func navigationKey(for event: NSEvent) -> FocusNavigationKey? {
        switch event.keyCode {
        case KeyCode.tab:
            return event.modifierFlags.contains(.shift) ? .shiftTab : .tab
        case KeyCode.left: return .arrowLeft
        case KeyCode.right: return .arrowRight
        case KeyCode.up: return .arrowUp
        case KeyCode.down: return .arrowDown
        default: return nil
        }
    }
}

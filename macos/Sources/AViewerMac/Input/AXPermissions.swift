import AppKit
import ApplicationServices

/// The accessibility permission gate.
///
/// Everything this tool does is an assistive-client operation, so without this
/// permission there is nothing useful to show. macOS grants it per signed
/// bundle: rebuilding with a different signature makes the system treat the app
/// as new and the permission has to be granted again.
enum AXPermissions {

    static var isTrusted: Bool {
        AXIsProcessTrusted()
    }

    /// Asks the system to show its own permission prompt. Returns the trust
    /// state as it stands now — granting happens asynchronously in System
    /// Settings, so a false result is not final.
    @discardableResult
    static func requestTrust() -> Bool {
        let options = [
            kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true
        ] as CFDictionary
        return AXIsProcessTrustedWithOptions(options)
    }

    static func openAccessibilitySettings() {
        let url = URL(
            string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")
        guard let url else { return }
        NSWorkspace.shared.open(url)
    }
}

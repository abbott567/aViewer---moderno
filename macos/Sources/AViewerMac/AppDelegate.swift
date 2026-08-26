import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {

    private var mainController: MainWindowController?

    func applicationDidFinishLaunching(_ notification: Notification) {
        let controller = MainWindowController()
        mainController = controller

        MainMenuBuilder.rebuild(for: controller)
        controller.show()
        NSApp.activate(ignoringOtherApps: true)

        // Ask once at launch so the permission is usually already granted by
        // the time the user reaches for an inspection command.
        if !AXPermissions.isTrusted {
            AXPermissions.requestTrust()
        }
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }
}

@main
enum AViewerMacApp {
    static func main() {
        let application = NSApplication.shared
        let delegate = AppDelegate()
        application.delegate = delegate
        application.setActivationPolicy(.regular)
        application.run()
        // Keep the delegate alive for the lifetime of the process.
        _ = delegate
    }
}

import Foundation

/// Where all persisted preferences live, mirroring the Windows build's use of
/// a single per-user application-data folder.
enum SupportDirectory {
    static let url: URL = {
        let base = FileManager.default.urls(
            for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? URL(fileURLWithPath: NSHomeDirectory())
        return base.appendingPathComponent("AViewerMac", isDirectory: true)
    }()

    static func file(_ name: String) -> URL {
        url.appendingPathComponent(name)
    }

    /// Best-effort write. Losing a preference must never interrupt inspection,
    /// so failures are swallowed and the setting simply applies for this session.
    static func write(_ data: Data, to name: String) {
        try? FileManager.default.createDirectory(
            at: url, withIntermediateDirectories: true)
        try? data.write(to: file(name), options: .atomic)
    }

    static func read(_ name: String) -> Data? {
        try? Data(contentsOf: file(name))
    }

    static func remove(_ name: String) {
        try? FileManager.default.removeItem(at: file(name))
    }
}

/// Window and behaviour preferences that survive a restart.
final class AppSettingsService {

    private struct State: Codable {
        var alwaysOnTop = false
        var showRelationships = false
        var includeArrowNavigation = true
        var enhancedUserInterface = false
        var treeDepth = 2
        var uiLanguage: String?
    }

    private static let fileName = "app-settings.json"
    private var state: State

    init() {
        if let data = SupportDirectory.read(AppSettingsService.fileName),
           let decoded = try? JSONDecoder().decode(State.self, from: data) {
            state = decoded
        } else {
            state = State()
        }
    }

    var alwaysOnTop: Bool {
        get { state.alwaysOnTop }
        set { state.alwaysOnTop = newValue; save() }
    }

    var showRelationships: Bool {
        get { state.showRelationships }
        set { state.showRelationships = newValue; save() }
    }

    var includeArrowNavigation: Bool {
        get { state.includeArrowNavigation }
        set { state.includeArrowNavigation = newValue; save() }
    }

    var enhancedUserInterface: Bool {
        get { state.enhancedUserInterface }
        set { state.enhancedUserInterface = newValue; save() }
    }

    var treeDepth: Int {
        get { state.treeDepth }
        set { state.treeDepth = newValue; save() }
    }

    var uiLanguage: String? {
        get { state.uiLanguage }
        set { state.uiLanguage = newValue; save() }
    }

    private func save() {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        guard let data = try? encoder.encode(state) else { return }
        SupportDirectory.write(data, to: AppSettingsService.fileName)
    }
}

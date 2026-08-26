import Foundation

/// A configurable entry in the Help menu.
struct HelpMenuLink: Decodable {
    var label: String?
    var resourceKey: String?
    var url: String?
    var isSeparator: Bool?
}

/// Loads Help menu entries from `HelpMenuLinks.json` inside the app bundle, so
/// a deployment can point users at its own support resources without a rebuild.
enum HelpMenuLinkService {

    private static let fileName = "HelpMenuLinks"

    static func load() -> [HelpMenuLink] {
        guard let url = Bundle.main.url(forResource: fileName, withExtension: "json"),
              let data = try? Data(contentsOf: url),
              let links = try? JSONDecoder().decode([HelpMenuLink].self, from: data)
        else { return defaultLinks }
        return links
    }

    /// Only web links are ever opened. A configuration file is user-editable,
    /// so it must not become a way to launch arbitrary local handlers.
    static func isAllowedURL(_ value: String?) -> Bool {
        guard let value, let url = URL(string: value), let scheme = url.scheme?.lowercased()
        else { return false }
        return (scheme == "https" || scheme == "http") && url.host != nil
    }

    private static let defaultLinks: [HelpMenuLink] = [
        HelpMenuLink(
            label: nil, resourceKey: "HelpDocumentation",
            url: "https://github.com/stevefaulkner/aViewer---moderno", isSeparator: nil),
        HelpMenuLink(
            label: nil, resourceKey: "HelpProjectWebsite",
            url: "https://github.com/stevefaulkner/aViewer---moderno", isSeparator: nil),
        HelpMenuLink(label: nil, resourceKey: nil, url: nil, isSeparator: true),
        HelpMenuLink(
            label: nil, resourceKey: "HelpReportIssue",
            url: "https://github.com/stevefaulkner/aViewer---moderno/issues", isSeparator: nil)
    ]
}

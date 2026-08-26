import Foundation

/// Builds an HTML representation from the accessibility metadata AX exposes.
///
/// This is not a DOM dump. macOS accessibility does not expose `outerHTML`, so
/// the tag is inferred from the AX role and subrole, and attributes come from
/// what the provider actually publishes: `AXDOMIdentifier` for `id`,
/// `AXDOMClassList` for `class`, and the `AXARIA*` attributes for ARIA
/// properties. Exact source capture would need a browser integration such as
/// the Chrome DevTools Protocol or a WebDriver session.
enum HTMLExportService {

    static func serializeElement(_ node: AccessibilityNode) -> String {
        serialize(node, includeChildren: false, depth: 0)
    }

    static func serializeSubtree(_ node: AccessibilityNode) -> String {
        serialize(node, includeChildren: true, depth: 0)
    }

    private static func serialize(
        _ node: AccessibilityNode,
        includeChildren: Bool,
        depth: Int
    ) -> String {
        let indent = String(repeating: "  ", count: depth)
        let tag = resolveTag(node)
        var markup = "\(indent)<\(tag)"

        markup += attribute("id", node.property("DOM", "DOM identifier (id)"))
        markup += attribute("class", classList(node))
        markup += attribute("role", node.property("ARIA", "Role"))
        markup += ariaAttributes(node)
        if !node.name.isEmpty, !hasTextContent(node, tag: tag) {
            markup += attribute("aria-label", node.name)
        }
        markup += ">"

        if includeChildren, !node.children.isEmpty {
            markup += "\n"
            for child in node.children {
                markup += serialize(child, includeChildren: true, depth: depth + 1) + "\n"
            }
            markup += indent
        } else if !node.name.isEmpty, hasTextContent(node, tag: tag) {
            markup += escape(node.name)
        }

        return markup + "</\(tag)>"
    }

    // MARK: - Attributes

    private static func classList(_ node: AccessibilityNode) -> String {
        let raw = node.property("DOM", "DOM class list")
        guard !raw.isEmpty, raw != "None" else { return "" }
        // The reader renders arrays as "a; b; c"; HTML wants them space separated.
        return raw
            .split(separator: ";")
            .map { $0.trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty }
            .joined(separator: " ")
    }

    /// Emits every ARIA-group property whose label is a real ARIA attribute.
    private static func ariaAttributes(_ node: AccessibilityNode) -> String {
        var markup = ""
        for property in node.properties
        where property.name.hasPrefix("aria-") && !property.value.isEmpty {
            guard property.value != "None", property.value != "false" else { continue }
            markup += attribute(property.name, property.value)
        }
        return markup
    }

    private static func attribute(_ name: String, _ value: String) -> String {
        guard !value.isEmpty, value != "Unavailable" else { return "" }
        return " \(name)=\"\(escape(value))\""
    }

    // MARK: - Tag inference

    /// Text-bearing tags render the accessible name as content; container tags
    /// carry it as `aria-label` instead.
    private static func hasTextContent(_ node: AccessibilityNode, tag: String) -> Bool {
        !["input", "img", "br", "hr", "table", "ul", "ol", "div", "nav", "main",
          "aside", "header", "footer", "form", "section"].contains(tag)
    }

    private static let subroleTags: [String: String] = [
        "AXLandmarkNavigation": "nav",
        "AXLandmarkMain": "main",
        "AXLandmarkBanner": "header",
        "AXLandmarkContentInfo": "footer",
        "AXLandmarkComplementary": "aside",
        "AXLandmarkSearch": "form",
        "AXLandmarkRegion": "section",
        "AXSearchField": "input",
        "AXSecureTextField": "input",
        "AXDefinitionListTerm": "dt",
        "AXDefinitionListDefinition": "dd"
    ]

    private static let roleTags: [String: String] = [
        "AXButton": "button",
        "AXPopUpButton": "select",
        "AXLink": "a",
        "AXCheckBox": "input",
        "AXRadioButton": "input",
        "AXTextField": "input",
        "AXTextArea": "textarea",
        "AXTable": "table",
        "AXRow": "tr",
        "AXColumn": "col",
        "AXCell": "td",
        "AXList": "ul",
        "AXImage": "img",
        "AXStaticText": "span",
        "AXWebArea": "body",
        "AXGroup": "div",
        "AXForm": "form",
        "AXToolbar": "div",
        "AXList Marker": "li"
    ]

    private static func resolveTag(_ node: AccessibilityNode) -> String {
        if let tag = subroleTags[node.subrole] { return tag }
        if let tag = roleTags[node.role] { return tag }

        // Headings expose their level through the value on both WebKit and
        // Chromium, so h1–h6 can be recovered rather than flattened to h2.
        if node.role == "AXHeading" {
            let level = Int(node.property("Value", "Value")) ?? 2
            return "h\(min(max(level, 1), 6))"
        }
        return "div"
    }

    private static func escape(_ value: String) -> String {
        value
            .replacingOccurrences(of: "&", with: "&amp;")
            .replacingOccurrences(of: "<", with: "&lt;")
            .replacingOccurrences(of: ">", with: "&gt;")
            .replacingOccurrences(of: "\"", with: "&quot;")
    }
}

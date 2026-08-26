import Accessibility
import AppKit
import ApplicationServices
import Foundation

/// Reads AX attributes defensively.
///
/// Every accessor here returns a value or nothing; an unresponsive or hostile
/// provider must never propagate an error into the inspection walk. This
/// mirrors the containment the Windows build applies at property boundaries.
enum AXValueReader {

    /// Applied to every element we talk to. Without a timeout a single
    /// unresponsive application freezes the whole inspection pass.
    static let messagingTimeout: Float = 0.4

    static func applyTimeout(_ element: AXUIElement) {
        AXUIElementSetMessagingTimeout(element, messagingTimeout)
    }

    // MARK: - Raw reads

    static func rawValue(_ element: AXUIElement, _ attribute: String) -> CFTypeRef? {
        read(element, attribute).value
    }

    /// A null attribute and an unreadable one mean different things to someone
    /// auditing a page, so the error is reported alongside the value rather
    /// than collapsed into "nothing".
    static func read(
        _ element: AXUIElement,
        _ attribute: String
    ) -> (value: CFTypeRef?, error: AXError) {
        var value: CFTypeRef?
        let result = AXUIElementCopyAttributeValue(element, attribute as CFString, &value)
        guard result == .success else { return (nil, result) }
        return (value, .success)
    }

    static func attributeNames(_ element: AXUIElement) -> [String] {
        var names: CFArray?
        guard AXUIElementCopyAttributeNames(element, &names) == .success,
              let list = names as? [String] else { return [] }
        return list
    }

    static func parameterizedAttributeNames(_ element: AXUIElement) -> [String] {
        var names: CFArray?
        guard AXUIElementCopyParameterizedAttributeNames(element, &names) == .success,
              let list = names as? [String] else { return [] }
        return list
    }

    static func actionNames(_ element: AXUIElement) -> [String] {
        var names: CFArray?
        guard AXUIElementCopyActionNames(element, &names) == .success,
              let list = names as? [String] else { return [] }
        return list
    }

    static func actionDescription(_ element: AXUIElement, _ action: String) -> String {
        var description: CFString?
        guard AXUIElementCopyActionDescription(element, action as CFString, &description) == .success
        else { return "" }
        return (description as String?) ?? ""
    }

    // MARK: - Typed reads

    static func string(_ element: AXUIElement, _ attribute: String) -> String {
        guard let value = rawValue(element, attribute) else { return "" }
        return describe(value, expandElements: false)
    }

    static func bool(_ element: AXUIElement, _ attribute: String) -> Bool {
        guard let value = rawValue(element, attribute) else { return false }
        if CFGetTypeID(value) == CFBooleanGetTypeID() {
            return CFBooleanGetValue((value as! CFBoolean))
        }
        if let number = value as? NSNumber { return number.boolValue }
        return false
    }

    static func integer(_ element: AXUIElement, _ attribute: String) -> Int? {
        guard let value = rawValue(element, attribute), let number = value as? NSNumber
        else { return nil }
        return number.intValue
    }

    static func element(_ element: AXUIElement, _ attribute: String) -> AXUIElement? {
        guard let value = rawValue(element, attribute) else { return nil }
        guard CFGetTypeID(value) == AXUIElementGetTypeID() else { return nil }
        return (value as! AXUIElement)
    }

    static func elements(_ element: AXUIElement, _ attribute: String) -> [AXUIElement] {
        guard let value = rawValue(element, attribute) else { return [] }
        return unwrapElements(value)
    }

    /// Accepts either a single element or an array of them; providers are
    /// inconsistent about which they use for the same logical relationship.
    static func unwrapElements(_ value: CFTypeRef) -> [AXUIElement] {
        if CFGetTypeID(value) == AXUIElementGetTypeID() {
            return [(value as! AXUIElement)]
        }
        guard CFGetTypeID(value) == CFArrayGetTypeID() else { return [] }
        let array = value as! CFArray as [AnyObject]
        return array.compactMap { item in
            let ref = item as CFTypeRef
            guard CFGetTypeID(ref) == AXUIElementGetTypeID() else { return nil }
            return (ref as! AXUIElement)
        }
    }

    static func processId(_ element: AXUIElement) -> pid_t {
        var pid: pid_t = 0
        guard AXUIElementGetPid(element, &pid) == .success else { return 0 }
        return pid
    }

    // MARK: - Geometry

    /// Frame in accessibility screen coordinates (top-left origin, y down).
    static func frame(_ element: AXUIElement) -> CGRect {
        if let frameValue = rawValue(element, "AXFrame"),
           CFGetTypeID(frameValue) == AXValueGetTypeID() {
            let axValue = frameValue as! AXValue
            if AXValueGetType(axValue) == .cgRect {
                var rect = CGRect.zero
                if AXValueGetValue(axValue, .cgRect, &rect) { return rect }
            }
        }

        var origin = CGPoint.zero
        var size = CGSize.zero

        if let positionValue = rawValue(element, kAXPositionAttribute),
           CFGetTypeID(positionValue) == AXValueGetTypeID() {
            let axValue = positionValue as! AXValue
            if AXValueGetType(axValue) == .cgPoint {
                AXValueGetValue(axValue, .cgPoint, &origin)
            }
        }

        if let sizeValue = rawValue(element, kAXSizeAttribute),
           CFGetTypeID(sizeValue) == AXValueGetTypeID() {
            let axValue = sizeValue as! AXValue
            if AXValueGetType(axValue) == .cgSize {
                AXValueGetValue(axValue, .cgSize, &size)
            }
        }

        return CGRect(origin: origin, size: size)
    }

    // MARK: - Description

    /// Renders any AX value as display text.
    ///
    /// `expandElements` controls whether referenced elements are summarised as
    /// "role: name" (useful in the property grid) or reduced to a role only.
    static func describe(_ value: CFTypeRef?, expandElements: Bool = true, depth: Int = 0) -> String {
        guard let value else { return "" }
        let typeId = CFGetTypeID(value)

        if typeId == CFStringGetTypeID() {
            return (value as! CFString) as String
        }
        if typeId == CFAttributedStringGetTypeID() {
            return (value as! NSAttributedString).string
        }
        if typeId == CFBooleanGetTypeID() {
            return CFBooleanGetValue((value as! CFBoolean)) ? "true" : "false"
        }
        if typeId == CFNumberGetTypeID() {
            return "\((value as! NSNumber))"
        }
        if typeId == CFURLGetTypeID() {
            return ((value as! CFURL) as URL).absoluteString
        }
        if typeId == AXUIElementGetTypeID() {
            return describeElement((value as! AXUIElement), expanded: expandElements)
        }
        if typeId == AXValueGetTypeID() {
            return describeAXValue((value as! AXValue))
        }
        if typeId == CFArrayGetTypeID() {
            guard depth < 2 else { return "…" }
            let array = value as! CFArray as [AnyObject]
            if array.isEmpty { return "None" }
            let items = array.prefix(24).map {
                describe($0 as CFTypeRef, expandElements: expandElements, depth: depth + 1)
            }
            let suffix = array.count > 24 ? "; … \(array.count - 24) more" : ""
            return items.joined(separator: "; ") + suffix
        }
        if typeId == CFDictionaryGetTypeID() {
            guard let dictionary = value as? [String: AnyObject] else { return "" }
            return dictionary.keys.sorted()
                .map { "\($0)=\(describe(dictionary[$0] as CFTypeRef?, expandElements: false, depth: depth + 1))" }
                .joined(separator: "; ")
        }
        if typeId == CFNullGetTypeID() {
            return ""
        }
        if typeId == CFDataGetTypeID() {
            return describeData(value as! CFData as Data)
        }
        return describeOpaque(value)
    }

    /// WebKit delivers AXCustomContent as a keyed archive rather than a plain
    /// array. It is the only channel that carries aria-describedby and
    /// aria-description, so decoding it is not optional for a web inspector.
    static func customContent(_ data: Data) -> [(label: String, value: String)]? {
        guard let items = try? NSKeyedUnarchiver.unarchivedObject(
            ofClasses: [NSArray.self, AXCustomContent.self, NSString.self],
            from: data) as? [AXCustomContent] else { return nil }
        return items.map { ($0.label, $0.value) }
    }

    private static func describeData(_ data: Data) -> String {
        if let content = customContent(data) {
            if content.isEmpty { return "None" }
            return content.map { "\($0.label): \($0.value)" }.joined(separator: "; ")
        }
        return "Data (\(data.count) bytes)"
    }

    /// Text markers and marker ranges are opaque handles used by the
    /// parameterised text API. Their raw description is a long hex dump that
    /// tells the reader nothing, so they are named instead of printed.
    private static func describeOpaque(_ value: CFTypeRef) -> String {
        let raw = String(describing: value)
        if raw.hasPrefix("<AXTextMarkerRange") { return "Text marker range (opaque)" }
        if raw.hasPrefix("<AXTextMarker") { return "Text marker (opaque)" }
        guard raw.count > 500 else { return raw }
        return String(raw.prefix(500)) + "…"
    }

    private static func describeAXValue(_ value: AXValue) -> String {
        switch AXValueGetType(value) {
        case .cgPoint:
            var point = CGPoint.zero
            AXValueGetValue(value, .cgPoint, &point)
            return String(format: "%.0f, %.0f", point.x, point.y)
        case .cgSize:
            var size = CGSize.zero
            AXValueGetValue(value, .cgSize, &size)
            return String(format: "%.0f x %.0f", size.width, size.height)
        case .cgRect:
            var rect = CGRect.zero
            AXValueGetValue(value, .cgRect, &rect)
            return String(
                format: "%.0f,%.0f %.0fx%.0f",
                rect.origin.x, rect.origin.y, rect.width, rect.height)
        case .cfRange:
            var range = CFRange(location: 0, length: 0)
            AXValueGetValue(value, .cfRange, &range)
            return "location \(range.location), length \(range.length)"
        case .axError:
            var error = AXError.success
            AXValueGetValue(value, .axError, &error)
            return "AXError \(error.rawValue)"
        case .illegal:
            return "Unsupported value"
        @unknown default:
            return "Unsupported value"
        }
    }

    static func describeElement(_ element: AXUIElement, expanded: Bool = true) -> String {
        let role = string(element, kAXRoleAttribute)
        guard expanded else { return role.isEmpty ? "Element" : role }
        let name = accessibleName(element)
        if name.isEmpty { return role.isEmpty ? "Element" : role }
        return "\(role.isEmpty ? "Element" : role): \(name)"
    }

    /// The best available human-readable name, in the order a screen reader
    /// would generally resolve it.
    static func accessibleName(_ element: AXUIElement) -> String {
        let title = string(element, kAXTitleAttribute)
        if !title.isEmpty { return title }

        if let titleElement = self.element(element, kAXTitleUIElementAttribute) {
            let referenced = string(titleElement, kAXTitleAttribute)
            if !referenced.isEmpty { return referenced }
            let referencedValue = string(titleElement, kAXValueAttribute)
            if !referencedValue.isEmpty { return referencedValue }
        }

        let description = string(element, kAXDescriptionAttribute)
        if !description.isEmpty { return description }

        // Only text-bearing controls should fall back to their value.
        let value = string(element, kAXValueAttribute)
        if !value.isEmpty, value.count <= 200 { return value }

        return ""
    }
}

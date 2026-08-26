import ApplicationServices
import Foundation

/// A single name/value pair shown in the property grid.
/// `group` is the section heading, mirroring the Windows build's grouping.
struct AccessibilityProperty: Hashable {
    let group: String
    let name: String
    let value: String

    init(_ group: String, _ name: String, _ value: String) {
        self.group = group
        self.name = name
        self.value = value
    }
}

/// A directed relationship from the inspected element to another element,
/// carrying enough geometry for the overlay to draw a connector without
/// re-reading the target.
struct AccessibilityRelationship {
    let type: String
    let source: String
    let targetId: String
    let targetName: String
    let targetRole: String
    let targetFrame: CGRect
    let targetElement: AXUIElement?
}

/// A node in the captured accessibility tree.
///
/// Frames are stored in accessibility screen coordinates: the origin is the
/// top-left of the primary display and y grows downwards. That matches the AX
/// API and the Windows build, so the overlay geometry ports unchanged. Cocoa
/// conversion happens only at the point of placing a window.
final class AccessibilityNode {
    let api = "AX"
    let id: String
    let element: AXUIElement?
    let name: String
    let role: String
    let subrole: String
    let roleDescription: String
    let identifier: String
    let processName: String
    let processId: pid_t
    let frame: CGRect
    let isEnabled: Bool
    let isFocused: Bool
    var properties: [AccessibilityProperty]
    /// The ARIA view of this element, reconstructed by `AriaMapper`. Kept
    /// apart from `properties` so an interpretation can never be mistaken for
    /// something the provider published.
    var ariaProperties: [AccessibilityProperty]
    var relationships: [AccessibilityRelationship]
    var children: [AccessibilityNode]
    weak var parent: AccessibilityNode?

    init(
        id: String,
        element: AXUIElement?,
        name: String,
        role: String,
        subrole: String = "",
        roleDescription: String = "",
        identifier: String = "",
        processName: String = "",
        processId: pid_t = 0,
        frame: CGRect = .zero,
        isEnabled: Bool = false,
        isFocused: Bool = false,
        properties: [AccessibilityProperty] = [],
        ariaProperties: [AccessibilityProperty] = [],
        relationships: [AccessibilityRelationship] = [],
        children: [AccessibilityNode] = []
    ) {
        self.id = id
        self.element = element
        self.name = name
        self.role = role
        self.subrole = subrole
        self.roleDescription = roleDescription
        self.identifier = identifier
        self.processName = processName
        self.processId = processId
        self.frame = frame
        self.isEnabled = isEnabled
        self.isFocused = isFocused
        self.properties = properties
        self.ariaProperties = ariaProperties
        self.relationships = relationships
        self.children = children
        for child in children {
            child.parent = self
        }
    }

    /// Label shown in the tree: role first, then the accessible name.
    var displayRole: String {
        if !subrole.isEmpty {
            return "\(role) (\(subrole))"
        }
        return role
    }

    var frameDescription: String {
        guard frame.width > 0 || frame.height > 0 else { return "" }
        return String(
            format: "%.0f,%.0f %.0fx%.0f",
            frame.origin.x, frame.origin.y, frame.width, frame.height)
    }

    var isDrawable: Bool {
        frame.width > 0 && frame.height > 0 && !frame.origin.x.isNaN && !frame.origin.y.isNaN
    }

    func property(_ group: String, _ name: String) -> String {
        properties.first {
            $0.group.caseInsensitiveCompare(group) == .orderedSame
                && $0.name.caseInsensitiveCompare(name) == .orderedSame
        }?.value ?? ""
    }

    /// Depth-first walk including self.
    func flattened() -> [AccessibilityNode] {
        var result: [AccessibilityNode] = [self]
        for child in children {
            result.append(contentsOf: child.flattened())
        }
        return result
    }

    var nodeCount: Int {
        1 + children.reduce(0) { $0 + $1.nodeCount }
    }
}

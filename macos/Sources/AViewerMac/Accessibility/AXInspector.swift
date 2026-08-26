import AppKit
import ApplicationServices
import Foundation

/// Captures accessibility trees through the macOS AXUIElement API.
///
/// This is the macOS counterpart to the Windows build's `Uia3Inspector`. The
/// shape of the result is deliberately the same, so exports and overlay
/// geometry are comparable across the two platforms, but the traversal differs
/// in one important way: AX gives us durable element references, so parent
/// navigation follows `AXParent` directly instead of re-hit-testing by bounds.
///
/// All methods are safe to call off the main thread and must be called from a
/// single serial queue — `enabledApplications` is not synchronised.
final class AXInspector {

    struct Result {
        let root: AccessibilityNode?
        let truncated: Bool
    }

    /// Guards against pathological trees (a full browser document can be
    /// hundreds of thousands of nodes). Exceeding it is reported, never hidden.
    static let maxNodes = 15000

    private let systemWide = AXUIElementCreateSystemWide()
    private var enabledApplications: Set<pid_t> = []

    /// Sets `AXEnhancedUserInterface` on inspected applications. Off by default:
    /// it makes some AppKit applications change their layout, which is a poor
    /// default for a tool meant to observe without disturbing.
    var enhancedUserInterface = false

    init() {
        AXValueReader.applyTimeout(systemWide)
    }

    // MARK: - Entry points

    func inspectPoint(_ point: CGPoint, maxDepth: Int) -> Result {
        var element: AXUIElement?
        let status = AXUIElementCopyElementAtPosition(
            systemWide, Float(point.x), Float(point.y), &element)
        guard status == .success, let element else { return Result(root: nil, truncated: false) }
        return build(from: element, maxDepth: maxDepth)
    }

    func inspectFocused(maxDepth: Int) -> Result {
        guard let element = AXValueReader.element(systemWide, kAXFocusedUIElementAttribute)
        else { return Result(root: nil, truncated: false) }
        return build(from: element, maxDepth: maxDepth)
    }

    /// Moves one level up the accessibility tree from the given node.
    func inspectParent(of node: AccessibilityNode, maxDepth: Int) -> Result {
        guard let element = node.element,
              let parent = AXValueReader.element(element, kAXParentAttribute)
        else { return Result(root: nil, truncated: false) }
        return build(from: parent, maxDepth: maxDepth)
    }

    /// Loads the whole application subtree that owns the given node.
    func inspectApplicationRoot(from node: AccessibilityNode, maxDepth: Int) -> Result {
        guard node.processId > 0 else { return Result(root: nil, truncated: false) }
        let application = AXUIElementCreateApplication(node.processId)
        AXValueReader.applyTimeout(application)
        return build(from: application, maxDepth: maxDepth)
    }

    // MARK: - Tree building

    private func build(from element: AXUIElement, maxDepth: Int) -> Result {
        enableAccessibility(for: AXValueReader.processId(element))
        var budget = AXInspector.maxNodes
        let root = map(element, depth: 0, maxDepth: max(0, maxDepth), budget: &budget)
        return Result(root: root, truncated: budget <= 0)
    }

    /// Chromium-based applications (Chrome, Edge, Electron, VS Code) expose only
    /// a stub tree until an assistive client asks for the full one. VoiceOver
    /// does this too; without it these applications look empty.
    private func enableAccessibility(for pid: pid_t) {
        guard pid > 0, !enabledApplications.contains(pid) else { return }
        enabledApplications.insert(pid)

        let application = AXUIElementCreateApplication(pid)
        AXValueReader.applyTimeout(application)
        AXUIElementSetAttributeValue(
            application, "AXManualAccessibility" as CFString, kCFBooleanTrue)

        if enhancedUserInterface {
            AXUIElementSetAttributeValue(
                application, "AXEnhancedUserInterface" as CFString, kCFBooleanTrue)
        }
    }

    private func map(
        _ element: AXUIElement,
        depth: Int,
        maxDepth: Int,
        budget: inout Int
    ) -> AccessibilityNode? {
        guard budget > 0 else { return nil }
        budget -= 1

        AXValueReader.applyTimeout(element)

        let attributeNames = AXValueReader.attributeNames(element)
        let attributeSet = Set(attributeNames)
        let pid = AXValueReader.processId(element)
        let frame = AXValueReader.frame(element)
        let role = AXValueReader.string(element, kAXRoleAttribute)
        let subrole = AXValueReader.string(element, kAXSubroleAttribute)

        var properties: [AccessibilityProperty] = []
        var relationships: [AccessibilityRelationship] = []
        var ariaProperties: [AccessibilityProperty] = []

        appendAttributeProperties(
            element,
            attributeNames: attributeNames,
            into: &properties,
            relationships: &relationships)

        appendIdentityProperties(
            element, pid: pid, frame: frame, into: &properties)

        ariaProperties = AriaMapper.properties(
            for: element, role: role, subrole: subrole, attributes: attributeSet)

        appendActionProperties(element, into: &properties)

        if !attributeSet.isEmpty {
            properties.append(AccessibilityProperty(
                "Parameterised",
                "Parameterised attributes",
                listOrNone(AXValueReader.parameterizedAttributeNames(element).sorted())))
        }

        let node = AccessibilityNode(
            id: identity(of: element, pid: pid),
            element: element,
            name: AXValueReader.accessibleName(element),
            role: role.isEmpty ? "AXUnknown" : role,
            subrole: subrole,
            roleDescription: AXValueReader.string(element, kAXRoleDescriptionAttribute),
            identifier: AXValueReader.string(element, kAXIdentifierAttribute),
            processName: processName(for: pid),
            processId: pid,
            frame: frame,
            isEnabled: AXValueReader.bool(element, kAXEnabledAttribute),
            isFocused: AXValueReader.bool(element, kAXFocusedAttribute),
            properties: properties.sorted(by: propertyOrder),
            ariaProperties: ariaProperties,
            relationships: relationships)

        if depth < maxDepth {
            for child in AXValueReader.elements(element, kAXChildrenAttribute) {
                guard budget > 0 else { break }
                if let mapped = map(child, depth: depth + 1, maxDepth: maxDepth, budget: &budget) {
                    mapped.parent = node
                    node.children.append(mapped)
                }
            }
        }

        return node
    }

    // MARK: - Properties

    private func appendAttributeProperties(
        _ element: AXUIElement,
        attributeNames: [String],
        into properties: inout [AccessibilityProperty],
        relationships: inout [AccessibilityRelationship]
    ) {
        for attribute in attributeNames {
            guard !AXAttributeCatalog.structuralAttributes.contains(attribute) else { continue }

            let group = AXAttributeCatalog.group(for: attribute)
            let label = AXAttributeCatalog.label(for: attribute)
            let result = AXValueReader.read(element, attribute)

            guard let value = result.value else {
                // A published attribute holding nothing is a fact worth
                // showing; a read that actually failed is a different fact.
                properties.append(AccessibilityProperty(
                    group, label,
                    result.error == .noValue ? "" : "Unavailable"))
                continue
            }

            let targets = AXValueReader.unwrapElements(value)
            if !targets.isEmpty, let descriptor = relationshipDescriptor(for: attribute) {
                for target in targets {
                    relationships.append(makeRelationship(
                        type: descriptor.type, source: descriptor.source, target: target))
                }
            }

            properties.append(AccessibilityProperty(
                group, label, AXValueReader.describe(value)))
        }
    }

    private func appendIdentityProperties(
        _ element: AXUIElement,
        pid: pid_t,
        frame: CGRect,
        into properties: inout [AccessibilityProperty]
    ) {
        properties.append(AccessibilityProperty(
            "AX",
            "Bounding rectangle",
            String(
                format: "%.0f,%.0f %.0fx%.0f",
                frame.origin.x, frame.origin.y, frame.width, frame.height)))

        let application = NSRunningApplication(processIdentifier: pid)
        properties.append(AccessibilityProperty(
            "AX", "Process", application?.localizedName ?? ""))
        properties.append(AccessibilityProperty(
            "AX", "Process ID", pid > 0 ? "\(pid)" : ""))
        properties.append(AccessibilityProperty(
            "AX", "Bundle identifier", application?.bundleIdentifier ?? ""))
    }

    private func appendActionProperties(
        _ element: AXUIElement,
        into properties: inout [AccessibilityProperty]
    ) {
        let actions = AXValueReader.actionNames(element)
        properties.append(AccessibilityProperty(
            "Actions", "Supported actions", listOrNone(actions)))

        for action in actions {
            let description = AXValueReader.actionDescription(element, action)
            guard !description.isEmpty else { continue }
            properties.append(AccessibilityProperty("Actions", action, description))
        }
    }

    // MARK: - Relationships

    /// Attributes that legitimately return many elements as tree structure
    /// rather than as a semantic relationship. Drawing connectors for these
    /// would bury the overlay in lines.
    private static let bulkElementAttributes: Set<String> = [
        kAXRowsAttribute, kAXColumnsAttribute, "AXCells", kAXSelectedRowsAttribute,
        kAXSelectedColumnsAttribute, "AXSelectedCells", kAXSelectedChildrenAttribute,
        kAXTabsAttribute, kAXContentsAttribute, "AXSections", kAXSplittersAttribute,
        "AXWindows", "AXChildrenInNavigationOrder", "AXSharedTextUIElements",
        "AXFocusableAncestor", "AXEditableAncestor", "AXHighestEditableAncestor"
    ]

    private func relationshipDescriptor(for attribute: String) -> (type: String, source: String)? {
        if let known = AXAttributeCatalog.relationshipAttributes[attribute] { return known }
        guard !AXInspector.bulkElementAttributes.contains(attribute) else { return nil }
        // An element-valued attribute we do not recognise is still a genuine
        // relationship the provider is publishing; show it under its own name.
        return (AXAttributeCatalog.humanise(attribute), attribute)
    }

    private func makeRelationship(
        type: String,
        source: String,
        target: AXUIElement
    ) -> AccessibilityRelationship {
        AXValueReader.applyTimeout(target)
        let targetRole = AXValueReader.string(target, kAXRoleAttribute)
        return AccessibilityRelationship(
            type: type,
            source: source,
            targetId: identity(of: target, pid: AXValueReader.processId(target)),
            targetName: AXValueReader.accessibleName(target),
            targetRole: targetRole.isEmpty ? "AXUnknown" : targetRole,
            targetFrame: AXValueReader.frame(target),
            targetElement: target)
    }

    // MARK: - Helpers

    /// AX has no runtime identifier, so identity is the owning process plus the
    /// element reference's own hash. Equal element references hash equally,
    /// which is what the de-duplication and change detection need.
    private func identity(of element: AXUIElement, pid: pid_t) -> String {
        "\(pid).\(CFHash(element))"
    }

    private func processName(for pid: pid_t) -> String {
        guard pid > 0 else { return "" }
        return NSRunningApplication(processIdentifier: pid)?.localizedName ?? ""
    }

    private func listOrNone(_ values: [String]) -> String {
        values.isEmpty ? "None" : values.joined(separator: ", ")
    }

    private func propertyOrder(
        _ first: AccessibilityProperty,
        _ second: AccessibilityProperty
    ) -> Bool {
        let firstGroup = AXAttributeCatalog.sortIndex(of: first.group)
        let secondGroup = AXAttributeCatalog.sortIndex(of: second.group)
        if firstGroup != secondGroup { return firstGroup < secondGroup }
        if first.group != second.group { return first.group < second.group }
        return first.name.localizedCaseInsensitiveCompare(second.name) == .orderedAscending
    }
}

import Foundation

/// Serialises a captured tree.
///
/// Key names deliberately match the Windows build wherever the concept exists
/// on both platforms, so a macOS capture and a Windows capture of the same web
/// page can be diffed directly. Keys with no macOS counterpart are omitted
/// rather than emitted empty, and AX-only keys are added alongside.
enum JSONExportService {

    static func serialize(_ node: AccessibilityNode) -> String {
        let object = dictionary(for: node)
        guard let data = try? JSONSerialization.data(
            withJSONObject: object,
            options: [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]),
            let text = String(data: data, encoding: .utf8)
        else { return "{}" }
        return text
    }

    private static func dictionary(for node: AccessibilityNode) -> [String: Any] {
        var object: [String: Any] = [
            "Api": node.api,
            "Id": node.id,
            "Name": node.name,
            "ControlType": node.role,
            "ProcessName": node.processName,
            "ProcessId": Int(node.processId),
            "BoundingRectangle": node.frameDescription,
            "BoundingX": node.frame.origin.x,
            "BoundingY": node.frame.origin.y,
            "BoundingWidth": node.frame.width,
            "BoundingHeight": node.frame.height,
            "IsEnabled": node.isEnabled,
            "HasKeyboardFocus": node.isFocused,
            "Properties": node.properties.map {
                ["Group": $0.group, "Name": $0.name, "Value": $0.value]
            },
            "Relationships": node.relationships.map {
                [
                    "Type": $0.type,
                    "Source": $0.source,
                    "TargetId": $0.targetId,
                    "TargetName": $0.targetName,
                    "TargetControlType": $0.targetRole,
                    "TargetX": $0.targetFrame.origin.x,
                    "TargetY": $0.targetFrame.origin.y,
                    "TargetWidth": $0.targetFrame.width,
                    "TargetHeight": $0.targetFrame.height
                ]
            },
            "Children": node.children.map(dictionary)
        ]

        if !node.subrole.isEmpty { object["Subrole"] = node.subrole }
        if !node.roleDescription.isEmpty { object["RoleDescription"] = node.roleDescription }
        if !node.identifier.isEmpty { object["Identifier"] = node.identifier }
        return object
    }
}

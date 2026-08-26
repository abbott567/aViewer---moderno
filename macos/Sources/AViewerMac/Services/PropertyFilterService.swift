import Foundation

/// One row in the "Choose properties" dialogue.
struct PropertyChoice {
    let group: String
    let name: String
    var isSelected: Bool
}

/// Remembers which properties the user has hidden.
///
/// The catalogue grows as new properties are encountered, which matters more on
/// macOS than on Windows: the attribute set is discovered from each element
/// rather than fixed in advance, so the list of things that *can* be hidden is
/// only known after inspecting a few applications.
final class PropertyFilterService {

    private struct State: Codable {
        var hiddenKeys: [String] = []
        var knownProperties: [Descriptor] = []
    }

    private struct Descriptor: Codable {
        let group: String
        let name: String
    }

    static let defaultFileName = "property-filter.json"
    private static let separator = "\u{001f}"

    /// Overridable so tests can persist to a scratch file instead of the
    /// user's real preferences.
    private let fileName: String
    private var hiddenKeys: Set<String> = []
    private var knownProperties: [String: Descriptor] = [:]

    init(fileName: String = PropertyFilterService.defaultFileName) {
        self.fileName = fileName
        guard let data = SupportDirectory.read(fileName),
              let state = try? JSONDecoder().decode(State.self, from: data) else { return }
        hiddenKeys = Set(state.hiddenKeys)
        for descriptor in state.knownProperties {
            knownProperties[key(descriptor.group, descriptor.name)] = descriptor
        }
    }

    func choices(from properties: [AccessibilityProperty]) -> [PropertyChoice] {
        register(properties)
        return knownProperties.values
            .sorted {
                let firstGroup = AXAttributeCatalog.sortIndex(of: $0.group)
                let secondGroup = AXAttributeCatalog.sortIndex(of: $1.group)
                if firstGroup != secondGroup { return firstGroup < secondGroup }
                if $0.group != $1.group { return $0.group < $1.group }
                return $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending
            }
            .map {
                PropertyChoice(
                    group: $0.group,
                    name: $0.name,
                    isSelected: !hiddenKeys.contains(key($0.group, $0.name)))
            }
    }

    func filter(_ properties: [AccessibilityProperty]) -> [AccessibilityProperty] {
        register(properties)
        return properties.filter { !hiddenKeys.contains(key($0.group, $0.name)) }
    }

    func apply(_ choices: [PropertyChoice]) {
        hiddenKeys.removeAll()
        for choice in choices {
            let identifier = key(choice.group, choice.name)
            knownProperties[identifier] = Descriptor(group: choice.group, name: choice.name)
            if !choice.isSelected { hiddenKeys.insert(identifier) }
        }
        save()
    }

    private func register(_ properties: [AccessibilityProperty]) {
        var changed = false
        for property in properties {
            let identifier = key(property.group, property.name)
            guard knownProperties[identifier] == nil else { continue }
            knownProperties[identifier] = Descriptor(
                group: property.group, name: property.name)
            changed = true
        }
        if changed { save() }
    }

    private func key(_ group: String, _ name: String) -> String {
        "\(group)\(PropertyFilterService.separator)\(name)"
    }

    private func save() {
        let state = State(
            hiddenKeys: hiddenKeys.sorted(),
            knownProperties: knownProperties.values.sorted {
                $0.group == $1.group ? $0.name < $1.name : $0.group < $1.group
            })
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        guard let data = try? encoder.encode(state) else { return }
        SupportDirectory.write(data, to: fileName)
    }
}

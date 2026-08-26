import ApplicationServices
import Foundation

/// Reconstructs the ARIA picture of a web element from what macOS publishes.
///
/// macOS has no equivalent of the UI Automation `AriaProperties` string the
/// Windows build reads. ARIA reaches the AX layer three different ways: some
/// attributes keep an ARIA-shaped name, some are folded into a native AX
/// attribute that means something else on native controls, and some disappear
/// into the role, subrole or value. A grid of raw attributes therefore shows
/// the information without ever naming it.
///
/// Every mapping here was established empirically against Safari and WebKit
/// using `docs/aria-state-test-page.html`. Nothing is mapped from memory, and
/// every row records the attribute it came from so a reader can check the
/// working. Attributes that could not be told apart from a platform default
/// are deliberately absent — see `undeterminable` for the list and reasons.
enum AriaMapper {

    /// The section a row is filed under is its provenance, so the source is
    /// always visible next to the value it produced.
    private static func row(_ name: String, _ value: String, from source: String)
        -> AccessibilityProperty {
        AccessibilityProperty(source, name, value)
    }

    /// Attributes published under a dedicated AX attribute, verified present.
    /// The AX name is the provenance shown to the reader.
    private static let direct: [(attribute: String, aria: String)] = [
        ("AXARIAAtomic", "aria-atomic"),
        ("AXARIALive", "aria-live"),
        ("AXARIARelevant", "aria-relevant"),
        ("AXARIACurrent", "aria-current"),
        ("AXARIAPosInSet", "aria-posinset"),
        ("AXARIASetSize", "aria-setsize"),
        ("AXARIAColumnCount", "aria-colcount"),
        ("AXARIARowCount", "aria-rowcount"),
        ("AXARIAColumnIndex", "aria-colindex"),
        ("AXARIARowIndex", "aria-rowindex"),
        ("AXElementBusy", "aria-busy"),
        ("AXBrailleLabel", "aria-braillelabel"),
        ("AXBrailleRoleDescription", "aria-brailleroledescription"),
        ("AXExpanded", "aria-expanded"),
        ("AXHasPopup", "aria-haspopup"),
        ("AXKeyShortcutsValue", "aria-keyshortcuts"),
        ("AXInvalid", "aria-invalid"),
        ("AXRequired", "aria-required"),
        ("AXOrientation", "aria-orientation"),
        ("AXPlaceholderValue", "aria-placeholder"),
        ("AXSortDirection", "aria-sort"),
        ("AXMaxValue", "aria-valuemax"),
        ("AXMinValue", "aria-valuemin"),
        ("AXValueDescription", "aria-valuetext"),
        ("AXSelected", "aria-selected"),
        ("AXOwns", "aria-owns"),
        ("AXActiveElement", "aria-activedescendant"),
        ("AXDetailsElements", "aria-details"),
        ("AXErrorMessageElements", "aria-errormessage")
    ]

    /// One AX attribute, more than one possible ARIA source. Naming both is
    /// honest; picking one would be a guess presented as a reading.
    private static let ambiguous: [(attribute: String, aria: String)] = [
        ("AXLinkedUIElements", "aria-controls or aria-flowto")
    ]

    /// ARIA attributes that reach macOS in a form this tool cannot separate
    /// from a platform default or a native equivalent. Reported in the
    /// documentation rather than invented in the grid.
    static let undeterminable: [(aria: String, reason: String)] = [
        ("aria-autocomplete", "not observed; the role becomes AXComboBox"),
        ("aria-multiselectable", "not observed to reach the AX layer"),
        ("aria-readonly", "not observed to reach the AX layer"),
        ("aria-colindextext", "not observed to reach the AX layer"),
        ("aria-rowindextext", "not observed to reach the AX layer"),
        ("aria-roledescription",
         "AXRoleDescription always carries a value, so an authored one cannot "
         + "be told apart from the platform default"),
        ("aria-hidden", "the element is removed from the accessibility tree entirely")
    ]

    /// Builds the ARIA view of one element.
    ///
    /// - Returns: an empty array for anything that is not web content. ARIA
    ///   belongs to markup; a native checkbox has no ARIA on it, and saying
    ///   otherwise would be a finding the page never earned.
    static func properties(
        for element: AXUIElement,
        role: String,
        subrole: String,
        attributes: Set<String>
    ) -> [AccessibilityProperty] {
        guard attributes.contains(where: { $0.hasPrefix("AXDOM") }) else { return [] }

        var result: [AccessibilityProperty] = []

        for entry in direct where attributes.contains(entry.attribute) {
            guard let value = AXValueReader.rawValue(element, entry.attribute) else { continue }
            let text = AXValueReader.describe(value)
            guard !text.isEmpty, text != "None" else { continue }
            result.append(row(entry.aria, text, from: entry.attribute))
        }

        for entry in ambiguous where attributes.contains(entry.attribute) {
            guard let value = AXValueReader.rawValue(element, entry.attribute) else { continue }
            let text = AXValueReader.describe(value)
            guard !text.isEmpty, text != "None" else { continue }
            result.append(row(entry.aria, text, from: entry.attribute))
        }

        appendName(element, attributes: attributes, into: &result)
        appendCustomContent(element, attributes: attributes, into: &result)
        appendInverted(element, attributes: attributes, into: &result)
        appendFolded(element, role: role, subrole: subrole, into: &result)
        appendSpans(element, attributes: attributes, into: &result)

        return result.sorted { $0.name < $1.name }
    }

    // MARK: - Accessible name

    /// `AXDescription` carries the authored accessible name. Which ARIA
    /// attribute authored it is recoverable: aria-labelledby also publishes
    /// the referenced element as `AXTitleUIElement`, and aria-label does not.
    /// Without either, `AXDescription` can still hold a non-ARIA name source
    /// such as an image's alt text, so the fallback names both.
    private static func appendName(
        _ element: AXUIElement,
        attributes: Set<String>,
        into result: inout [AccessibilityProperty]
    ) {
        guard attributes.contains(kAXDescriptionAttribute) else { return }
        let name = AXValueReader.string(element, kAXDescriptionAttribute)
        guard !name.isEmpty else { return }

        if attributes.contains(kAXTitleUIElementAttribute),
           AXValueReader.element(element, kAXTitleUIElementAttribute) != nil {
            result.append(row(
                "aria-labelledby", name,
                from: "AXDescription and AXTitleUIElement"))
        } else {
            result.append(row(
                "aria-label or alt", name,
                from: "AXDescription"))
        }
    }

    // MARK: - Custom content

    /// The accessible description travels in `AXCustomContent`, as an archived
    /// payload rather than a string, which is why nothing else on the element
    /// appears to carry aria-describedby. The two sources produce an identical
    /// payload, so both are named.
    private static func appendCustomContent(
        _ element: AXUIElement,
        attributes: Set<String>,
        into result: inout [AccessibilityProperty]
    ) {
        guard attributes.contains("AXCustomContent"),
              let raw = AXValueReader.rawValue(element, "AXCustomContent"),
              CFGetTypeID(raw) == CFDataGetTypeID(),
              let content = AXValueReader.customContent(raw as! CFData as Data)
        else { return }

        for item in content where !item.value.isEmpty {
            if item.label == "description" {
                result.append(row(
                    "aria-description or aria-describedby", item.value,
                    from: "AXCustomContent"))
            } else {
                result.append(row(item.label, item.value, from: "AXCustomContent"))
            }
        }
    }

    // MARK: - Inverted

    /// `aria-disabled` arrives as the absence of `AXEnabled`, which the
    /// `disabled` attribute also produces. Both are named.
    private static func appendInverted(
        _ element: AXUIElement,
        attributes: Set<String>,
        into result: inout [AccessibilityProperty]
    ) {
        guard attributes.contains(kAXEnabledAttribute),
              AXValueReader.rawValue(element, kAXEnabledAttribute) != nil,
              !AXValueReader.bool(element, kAXEnabledAttribute) else { return }
        result.append(row(
            "aria-disabled or the disabled attribute", "true",
            from: "AXEnabled false"))
    }

    // MARK: - Folded into role, subrole or value

    private static func appendFolded(
        _ element: AXUIElement,
        role: String,
        subrole: String,
        into result: inout [AccessibilityProperty]
    ) {
        let numeric = AXValueReader.integer(element, kAXValueAttribute)

        if subrole == "AXToggle" {
            if let state = triState(numeric) {
                result.append(row("aria-pressed", state, from: "AXSubrole AXToggle and AXValue"))
            }
        } else if role == kAXCheckBoxRole || role == kAXRadioButtonRole {
            if let state = triState(numeric) {
                result.append(row("aria-checked", state, from: "AXRole \(role) and AXValue"))
            }
        }

        if role == "AXHeading", let level = numeric {
            result.append(row("aria-level", "\(level)", from: "AXRole AXHeading and AXValue"))
        }

        if subrole == "AXApplicationDialog" {
            result.append(row("aria-modal", "true", from: "AXSubrole AXApplicationDialog"))
        }

        if role == kAXTextAreaRole {
            result.append(row("aria-multiline", "true", from: "AXRole AXTextArea"))
        }

        if role == kAXSliderRole || role == "AXProgressIndicator" {
            let value = AXValueReader.string(element, kAXValueAttribute)
            if !value.isEmpty {
                result.append(row("aria-valuenow", value, from: "AXRole \(role) and AXValue"))
            }
        }
    }

    // MARK: - Spans

    /// A cell's span is the length of its index range. A length of one is the
    /// default, so only a real span is reported.
    private static func appendSpans(
        _ element: AXUIElement,
        attributes: Set<String>,
        into result: inout [AccessibilityProperty]
    ) {
        let spans = [
            (kAXColumnIndexRangeAttribute, "aria-colspan"),
            (kAXRowIndexRangeAttribute, "aria-rowspan")
        ]
        for (attribute, aria) in spans where attributes.contains(attribute) {
            guard let length = rangeLength(element, attribute), length > 1 else { continue }
            result.append(row(aria, "\(length)", from: "\(attribute) length"))
        }
    }

    private static func rangeLength(_ element: AXUIElement, _ attribute: String) -> Int? {
        guard let value = AXValueReader.rawValue(element, attribute),
              CFGetTypeID(value) == AXValueGetTypeID() else { return nil }
        let axValue = value as! AXValue
        guard AXValueGetType(axValue) == .cfRange else { return nil }
        var range = CFRange(location: 0, length: 0)
        guard AXValueGetValue(axValue, .cfRange, &range) else { return nil }
        return range.length
    }

    /// AX reports tri-state values as 0, 1 and 2.
    private static func triState(_ value: Int?) -> String? {
        switch value {
        case 0: return "false"
        case 1: return "true"
        case 2: return "mixed"
        default: return nil
        }
    }
}

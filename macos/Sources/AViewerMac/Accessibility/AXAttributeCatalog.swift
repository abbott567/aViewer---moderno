import ApplicationServices
import Foundation

/// Classifies AX attributes into property-grid sections and gives the
/// well-known ones a readable label.
///
/// Unlike the Windows build, which queries a fixed list of UIA properties,
/// this catalogue only *describes* attributes — the inspector enumerates
/// whatever the element actually publishes. On macOS the attribute set varies
/// widely between AppKit, Electron and each browser engine, so a fixed list
/// would silently hide the web-specific attributes that matter most.
enum AXAttributeCatalog {

    /// Values aViewer inferred rather than read. Kept in its own section, and
    /// its own tab, so it can never be mistaken for a published attribute.
    static let derivedAriaGroup = "ARIA (derived)"

    /// Sections, in the order they appear in the property grid.
    static let groupOrder = [
        "AX", "ARIA", derivedAriaGroup, "DOM", "Value", "Text", "Table", "Table cell",
        "State", "Relationships", "Actions", "Parameterised", "Other"
    ]

    /// Handled explicitly elsewhere, or pure tree structure. Listing these as
    /// properties would be noise.
    static let structuralAttributes: Set<String> = [
        kAXChildrenAttribute,
        kAXParentAttribute,
        kAXWindowAttribute,
        kAXTopLevelUIElementAttribute,
        kAXPositionAttribute,
        kAXSizeAttribute,
        "AXFrame",
        "AXChildrenInNavigationOrder",
        "AXVisibleChildren",
        "AXVisibleRows",
        "AXVisibleColumns",
        "AXVisibleCells"
    ]

    /// Attributes whose element values are drawn as relationship connectors.
    /// The tuple is the label shown on the overlay and in the grid, plus the
    /// provenance string explaining where the relationship came from.
    static let relationshipAttributes: [String: (type: String, source: String)] = [
        kAXTitleUIElementAttribute: (
            "Labelled by", "AXTitleUIElement / aria-labelledby"),
        kAXServesAsTitleForUIElementsAttribute: (
            "Label for", "AXServesAsTitleForUIElements"),
        kAXLinkedUIElementsAttribute: (
            "Linked", "AXLinkedUIElements / aria-controls, aria-flowto"),
        kAXSharedFocusElementsAttribute: (
            "Shared focus", "AXSharedFocusElements"),
        "AXDescribedBy": (
            "Described by", "AXDescribedBy / aria-describedby"),
        "AXDetailsElements": (
            "Details", "AXDetailsElements / aria-details"),
        "AXErrorMessageElements": (
            "Error message", "AXErrorMessageElements / aria-errormessage"),
        "AXOwns": (
            "Owns", "AXOwns / aria-owns"),
        "AXFlowTo": (
            "Flows to", "AXFlowTo / aria-flowto"),
        "AXControls": (
            "Controls", "AXControls / aria-controls"),
        kAXHeaderAttribute: (
            "Header", "AXHeader"),
        "AXRowHeaderUIElements": (
            "Row header", "AXRowHeaderUIElements / HTML headers"),
        "AXColumnHeaderUIElements": (
            "Column header", "AXColumnHeaderUIElements / HTML headers"),
        "AXNextContents": (
            "Next contents", "AXNextContents"),
        "AXPreviousContents": (
            "Previous contents", "AXPreviousContents")
    ]

    private static let coreAttributes: Set<String> = [
        kAXRoleAttribute, kAXSubroleAttribute, kAXRoleDescriptionAttribute,
        kAXTitleAttribute, kAXDescriptionAttribute, kAXHelpAttribute,
        kAXIdentifierAttribute, kAXPlaceholderValueAttribute, kAXURLAttribute,
        "AXLanguage", "AXElementBusy", "AXAlternateUIVisible"
    ]

    private static let stateAttributes: Set<String> = [
        kAXEnabledAttribute, kAXFocusedAttribute, kAXSelectedAttribute,
        kAXExpandedAttribute, kAXHiddenAttribute, "AXRequired", "AXInvalid",
        "AXDisclosing", "AXDisclosureLevel", "AXEdited", "AXVisited",
        "AXHasPopup", "AXPopupValue", "AXIsAttachment", "AXIsIndeterminate"
    ]

    private static let valueAttributes: Set<String> = [
        kAXValueAttribute, kAXValueDescriptionAttribute, kAXMinValueAttribute,
        kAXMaxValueAttribute, kAXValueIncrementAttribute, kAXValueWrapsAttribute,
        "AXValueAutofillAvailable", "AXAllowedValues", "AXUnits",
        "AXUnitDescription"
    ]

    private static let textAttributes: Set<String> = [
        kAXNumberOfCharactersAttribute, kAXSelectedTextAttribute,
        kAXSelectedTextRangeAttribute, kAXSelectedTextRangesAttribute,
        kAXVisibleCharacterRangeAttribute, kAXInsertionPointLineNumberAttribute,
        "AXTextInputMarkedRange", "AXIsEditable"
    ]

    private static let tableAttributes: Set<String> = [
        kAXRowCountAttribute, kAXColumnCountAttribute, kAXRowsAttribute,
        kAXColumnsAttribute, kAXSelectedRowsAttribute, kAXSelectedColumnsAttribute,
        kAXSortDirectionAttribute, kAXOrderedByRowAttribute,
        "AXARIAColumnCount", "AXARIARowCount"
    ]

    private static let tableCellAttributes: Set<String> = [
        kAXRowIndexRangeAttribute, kAXColumnIndexRangeAttribute,
        "AXARIAColumnIndex", "AXARIARowIndex"
    ]

    /// Human-readable labels for attributes whose raw name is opaque.
    private static let friendlyNames: [String: String] = [
        kAXRoleAttribute: "Role",
        kAXSubroleAttribute: "Subrole",
        kAXRoleDescriptionAttribute: "Role description",
        kAXTitleAttribute: "Title",
        kAXDescriptionAttribute: "Description",
        kAXHelpAttribute: "Help",
        kAXIdentifierAttribute: "Identifier",
        kAXValueAttribute: "Value",
        kAXValueDescriptionAttribute: "Value description",
        kAXMinValueAttribute: "Minimum value",
        kAXMaxValueAttribute: "Maximum value",
        kAXValueIncrementAttribute: "Value increment",
        kAXPlaceholderValueAttribute: "Placeholder",
        kAXEnabledAttribute: "Enabled",
        kAXFocusedAttribute: "Focused",
        kAXSelectedAttribute: "Selected",
        kAXExpandedAttribute: "Expanded",
        kAXHiddenAttribute: "Hidden",
        kAXURLAttribute: "URL",
        kAXNumberOfCharactersAttribute: "Number of characters",
        kAXSelectedTextAttribute: "Selected text",
        kAXSelectedTextRangeAttribute: "Selected text range",
        kAXVisibleCharacterRangeAttribute: "Visible character range",
        kAXInsertionPointLineNumberAttribute: "Insertion point line",
        kAXRowCountAttribute: "Row count",
        kAXColumnCountAttribute: "Column count",
        kAXRowIndexRangeAttribute: "Row index range",
        kAXColumnIndexRangeAttribute: "Column index range",
        kAXSortDirectionAttribute: "Sort direction",
        "AXDOMIdentifier": "DOM identifier (id)",
        "AXDOMClassList": "DOM class list",
        "AXARIARole": "Role",
        "AXARIALive": "aria-live",
        "AXARIAAtomic": "aria-atomic",
        "AXARIARelevant": "aria-relevant",
        "AXARIABusy": "aria-busy",
        "AXARIACurrent": "aria-current",
        "AXARIAPosInSet": "aria-posinset",
        "AXARIASetSize": "aria-setsize",
        "AXARIAColumnCount": "aria-colcount",
        "AXARIARowCount": "aria-rowcount",
        "AXARIAColumnIndex": "aria-colindex",
        "AXARIARowIndex": "aria-rowindex",
        "AXInvalid": "aria-invalid",
        "AXRequired": "aria-required",
        "AXHasPopup": "aria-haspopup",
        "AXPopupValue": "aria-haspopup value",
        "AXKeyShortcutsValue": "aria-keyshortcuts",
        "AXVisited": "Visited",
        "AXIsEditable": "Editable",
        "AXLanguage": "Language",
        "AXElementBusy": "Busy"
    ]

    static func group(for attribute: String) -> String {
        // Anything that reads as an ARIA attribute belongs with the rest of
        // them, wherever it happens to sit in the AX naming scheme.
        if label(for: attribute).hasPrefix("aria-") { return "ARIA" }
        if relationshipAttributes[attribute] != nil { return "Relationships" }
        if coreAttributes.contains(attribute) { return "AX" }
        if stateAttributes.contains(attribute) { return "State" }
        if valueAttributes.contains(attribute) { return "Value" }
        if textAttributes.contains(attribute) { return "Text" }
        if tableAttributes.contains(attribute) { return "Table" }
        if tableCellAttributes.contains(attribute) { return "Table cell" }
        if attribute.hasPrefix("AXARIA") { return "ARIA" }
        if attribute.hasPrefix("AXDOM") { return "DOM" }
        return "Other"
    }

    static func label(for attribute: String) -> String {
        if let friendly = friendlyNames[attribute] { return friendly }
        if let relationship = relationshipAttributes[attribute] { return relationship.type }
        // Web providers publish ARIA state under an "AXARIA" prefix. Rendering
        // these as the ARIA attribute they came from is far more useful to the
        // reader than a prose paraphrase of the AX name.
        if attribute.hasPrefix("AXARIA"), attribute.count > 6 {
            return "aria-" + attribute.dropFirst(6).lowercased()
        }
        return humanise(attribute)
    }

    /// "AXSomeAttributeName" -> "Some attribute name".
    static func humanise(_ attribute: String) -> String {
        var name = attribute
        if name.hasPrefix("AX") { name.removeFirst(2) }
        guard !name.isEmpty else { return attribute }

        var words: [String] = []
        var current = ""
        for character in name {
            if character.isUppercase, !current.isEmpty,
               !(current.count == 1 && current.first!.isUppercase) {
                words.append(current)
                current = String(character)
            } else {
                current.append(character)
            }
        }
        if !current.isEmpty { words.append(current) }

        let joined = words.joined(separator: " ")
        return joined.prefix(1).uppercased() + joined.dropFirst().lowercased()
    }

    static func sortIndex(of group: String) -> Int {
        groupOrder.firstIndex(of: group) ?? groupOrder.count
    }
}

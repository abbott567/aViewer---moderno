import XCTest
@testable import AViewerMac

final class AXAttributeCatalogTests: XCTestCase {

    func testHumanisesAttributeNames() {
        XCTAssertEqual(AXAttributeCatalog.humanise("AXRoleDescription"), "Role description")
        XCTAssertEqual(AXAttributeCatalog.humanise("AXTitle"), "Title")
    }

    func testMapsAriaAttributesToTheirAriaNames() {
        XCTAssertEqual(AXAttributeCatalog.label(for: "AXARIALive"), "aria-live")
        // Unknown ARIA attributes still resolve, so a provider can publish
        // something new without the catalogue needing an update.
        XCTAssertEqual(AXAttributeCatalog.label(for: "AXARIAModal"), "aria-modal")
    }

    func testGroupsAttributesBySection() {
        XCTAssertEqual(AXAttributeCatalog.group(for: "AXRequired"), "ARIA")
        XCTAssertEqual(AXAttributeCatalog.group(for: "AXDOMClassList"), "DOM")
        XCTAssertEqual(AXAttributeCatalog.group(for: "AXTitleUIElement"), "Relationships")
    }

    func testNamesKnownRelationships() {
        XCTAssertEqual(AXAttributeCatalog.label(for: "AXTitleUIElement"), "Labelled by")
    }

    func testAncestorPointersAreNotTreatedAsRelationshipsOrState() {
        // Chromium publishes these on every web element. They are structure,
        // not semantics: grouping them with relationships would fill the
        // overlay with connectors that say nothing about the page.
        for attribute in ["AXEditableAncestor", "AXFocusableAncestor",
                          "AXHighestEditableAncestor"] {
            XCTAssertEqual(AXAttributeCatalog.group(for: attribute), "Other", attribute)
        }
    }

    func testTreeStructureIsNotListedAsProperties() {
        XCTAssertTrue(AXAttributeCatalog.structuralAttributes.contains("AXChildren"))
        XCTAssertTrue(AXAttributeCatalog.structuralAttributes.contains("AXParent"))
    }
}

import XCTest
@testable import AViewerMac

/// The derived ARIA section names states macOS has no attribute for.
///
/// These assert the section is ordered and labelled correctly. The mapping
/// itself was established empirically against WebKit: an aria-pressed button
/// is published as AXRole AXCheckBox, AXSubrole AXToggle, AXValue 0 or 1.
final class DerivedAriaTests: XCTestCase {

    func testDerivedSectionSortsDirectlyAfterPublishedAriaAttributes() {
        let aria = AXAttributeCatalog.sortIndex(of: "ARIA")
        let derived = AXAttributeCatalog.sortIndex(of: "ARIA (derived)")
        let dom = AXAttributeCatalog.sortIndex(of: "DOM")

        XCTAssertEqual(derived, aria + 1)
        XCTAssertLessThan(derived, dom)
    }

    func testDerivedSectionIsDistinctFromPublishedAttributes() {
        // Interpretation must never be presented as something the provider
        // published, so the two sections cannot share a name.
        XCTAssertNotEqual(
            AXAttributeCatalog.sortIndex(of: "ARIA"),
            AXAttributeCatalog.sortIndex(of: "ARIA (derived)"))
    }

    func testAriaCurrentIsPublishedDirectlyAndNeedsNoDerivation() {
        // WebKit does publish this one, so it belongs in the plain ARIA
        // section and must not be duplicated as a derived value.
        XCTAssertEqual(AXAttributeCatalog.group(for: "AXARIACurrent"), "ARIA")
        XCTAssertEqual(AXAttributeCatalog.label(for: "AXARIACurrent"), "aria-current")
    }
}

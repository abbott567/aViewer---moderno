import Accessibility
import XCTest
@testable import AViewerMac

/// The ARIA tab reconstructs the ARIA picture of a web element.
///
/// Every mapping it uses was established empirically against Safari and WebKit
/// with `docs/aria-state-test-page.html`. These tests guard the properties of
/// the reconstruction that keep it honest rather than re-asserting the mapping
/// table, which only the probe can establish.
final class AriaMapperTests: XCTestCase {

    func testNonWebContentGetsNoAriaAtAll() {
        // A native checkbox has no ARIA on it. Reporting any would be a
        // finding the page never earned.
        let rows = AriaMapper.properties(
            for: AXUIElementCreateSystemWide(),
            role: "AXCheckBox",
            subrole: "",
            attributes: ["AXRole", "AXValue", "AXEnabled"])
        XCTAssertTrue(rows.isEmpty)
    }

    func testEveryUndeterminableAttributeCarriesAReason() {
        // The list is documentation for auditors: an attribute is only allowed
        // on it if we can say why it cannot be reported.
        XCTAssertFalse(AriaMapper.undeterminable.isEmpty)
        for entry in AriaMapper.undeterminable {
            XCTAssertTrue(entry.aria.hasPrefix("aria-"), entry.aria)
            XCTAssertFalse(entry.reason.isEmpty, entry.aria)
        }
    }

    func testUndeterminableAttributesAreNotAlsoReported() throws {
        // Nothing may be both "cannot be determined" and mapped, or the
        // documentation and the grid would contradict each other.
        let named = Set(AriaMapper.undeterminable.map(\.aria))
        XCTAssertFalse(named.contains("aria-expanded"))
        XCTAssertFalse(named.contains("aria-pressed"))
        XCTAssertTrue(named.contains("aria-roledescription"))
        XCTAssertTrue(named.contains("aria-hidden"))
    }
}

final class AXAttributeCatalogAriaTests: XCTestCase {

    func testAriaCurrentIsPublishedDirectly() {
        XCTAssertEqual(AXAttributeCatalog.group(for: "AXARIACurrent"), "ARIA")
        XCTAssertEqual(AXAttributeCatalog.label(for: "AXARIACurrent"), "aria-current")
    }

    func testColumnAndRowAriaNamesUseTheAbbreviatedSpelling() {
        // The generic AXARIA prefix rule would produce "aria-columncount";
        // the spec spells it "aria-colcount".
        XCTAssertEqual(AXAttributeCatalog.label(for: "AXARIAColumnCount"), "aria-colcount")
        XCTAssertEqual(AXAttributeCatalog.label(for: "AXARIARowIndex"), "aria-rowindex")
    }
}

final class CustomContentTests: XCTestCase {

    /// WebKit delivers aria-describedby as a keyed archive of AXCustomContent
    /// objects. Round-trip one to prove the decoder reads the wire format.
    func testDecodesArchivedCustomContent() throws {
        let item = AXCustomContent(label: "description", value: "The description text")
        let data = try NSKeyedArchiver.archivedData(
            withRootObject: [item], requiringSecureCoding: true)

        let decoded = try XCTUnwrap(AXValueReader.customContent(data))
        XCTAssertEqual(decoded.count, 1)
        XCTAssertEqual(decoded.first?.label, "description")
        XCTAssertEqual(decoded.first?.value, "The description text")
    }

    func testDescribedbyIsNoLongerListedAsUndeterminable() {
        let named = Set(AriaMapper.undeterminable.map(\.aria))
        XCTAssertFalse(named.contains("aria-describedby"))
        XCTAssertFalse(named.contains("aria-description"))
        XCTAssertFalse(named.contains("aria-activedescendant"))
    }
}

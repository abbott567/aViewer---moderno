import XCTest
@testable import AViewerMac

final class HTMLExportServiceTests: XCTestCase {

    func testInfersTagFromRole() {
        XCTAssertEqual(
            HTMLExportService.serializeElement(node(role: "AXButton", name: "Send")),
            "<button>Send</button>")
    }

    func testSubroleWinsOverRole() {
        XCTAssertEqual(
            HTMLExportService.serializeElement(
                node(role: "AXGroup", subrole: "AXLandmarkNavigation")),
            "<nav></nav>")
    }

    func testHeadingLevelComesFromTheValue() {
        XCTAssertEqual(
            HTMLExportService.serializeElement(node(
                role: "AXHeading", name: "Contact",
                properties: [AccessibilityProperty("Value", "Value", "3")])),
            "<h3>Contact</h3>")
    }

    func testEmitsDomIdentifierAndClassList() {
        XCTAssertEqual(
            HTMLExportService.serializeElement(node(
                role: "AXLink", name: "Home",
                properties: [
                    AccessibilityProperty("DOM", "DOM identifier (id)", "nav-home"),
                    AccessibilityProperty("DOM", "DOM class list", "btn; primary")
                ])),
            "<a id=\"nav-home\" class=\"btn primary\">Home</a>")
    }

    func testEmitsAriaPropertiesButSkipsFalseOnes() {
        XCTAssertEqual(
            HTMLExportService.serializeElement(node(
                role: "AXGroup", subrole: "AXLandmarkRegion",
                properties: [
                    AccessibilityProperty("ARIA", "aria-live", "polite"),
                    AccessibilityProperty("ARIA", "aria-atomic", "false")
                ])),
            "<section aria-live=\"polite\"></section>")
    }

    func testEscapesText() {
        XCTAssertEqual(
            HTMLExportService.serializeElement(node(role: "AXStaticText", name: "a < b & c")),
            "<span>a &lt; b &amp; c</span>")
    }

    func testSubtreeNestsChildren() {
        let parent = AccessibilityNode(
            id: "p", element: nil, name: "", role: "AXList",
            children: [node(role: "AXStaticText", name: "One")])
        XCTAssertEqual(
            HTMLExportService.serializeSubtree(parent),
            "<ul>\n  <span>One</span>\n</ul>")
    }

    private func node(
        role: String,
        subrole: String = "",
        name: String = "",
        properties: [AccessibilityProperty] = []
    ) -> AccessibilityNode {
        AccessibilityNode(
            id: "1", element: nil, name: name, role: role,
            subrole: subrole, properties: properties)
    }
}

final class JSONExportServiceTests: XCTestCase {

    func testExportsWindowsCompatibleKeys() throws {
        let node = AccessibilityNode(
            id: "1", element: nil, name: "Send", role: "AXButton",
            properties: [AccessibilityProperty("AX", "Role", "AXButton")])
        let data = try XCTUnwrap(JSONExportService.serialize(node).data(using: .utf8))
        let parsed = try XCTUnwrap(
            JSONSerialization.jsonObject(with: data) as? [String: Any])

        XCTAssertEqual(parsed["Api"] as? String, "AX")
        // The Windows build calls this ControlType; keeping the key lets the
        // two platforms' exports be diffed directly.
        XCTAssertEqual(parsed["ControlType"] as? String, "AXButton")
        XCTAssertEqual((parsed["Properties"] as? [[String: Any]])?.count, 1)
        XCTAssertNil(parsed["Subrole"], "empty optional keys should be omitted")
    }
}

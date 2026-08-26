import XCTest
@testable import AViewerMac

final class PropertyFilterServiceTests: XCTestCase {

    private let scratchFile = "property-filter-tests.json"

    override func tearDown() {
        SupportDirectory.remove(scratchFile)
        super.tearDown()
    }

    func testHidesClearedPropertiesAndKeepsTheRest() throws {
        let service = PropertyFilterService(fileName: scratchFile)
        let properties = [
            AccessibilityProperty("AX", "Role", "AXButton"),
            AccessibilityProperty("AX", "Title", "Send")
        ]

        var choices = service.choices(from: properties)
        XCTAssertEqual(choices.count, 2)

        let index = try XCTUnwrap(choices.firstIndex { $0.name == "Title" })
        choices[index].isSelected = false
        service.apply(choices)

        let visible = service.filter(properties)
        XCTAssertFalse(visible.contains { $0.name == "Title" })
        XCTAssertTrue(visible.contains { $0.name == "Role" })
    }

    func testChoicesPersistAcrossInstances() throws {
        let first = PropertyFilterService(fileName: scratchFile)
        var choices = first.choices(from: [AccessibilityProperty("AX", "Title", "Send")])
        choices[0].isSelected = false
        first.apply(choices)

        let second = PropertyFilterService(fileName: scratchFile)
        XCTAssertTrue(second.filter(
            [AccessibilityProperty("AX", "Title", "Send")]).isEmpty)
    }
}

final class ScreenGeometryTests: XCTestCase {

    func testAccessibilityAndCocoaCoordinatesRoundTrip() {
        let rect = CGRect(x: 10, y: 20, width: 100, height: 50)
        XCTAssertEqual(
            ScreenGeometry.axRect(fromCocoa: ScreenGeometry.cocoaRect(fromAX: rect)),
            rect)
    }
}

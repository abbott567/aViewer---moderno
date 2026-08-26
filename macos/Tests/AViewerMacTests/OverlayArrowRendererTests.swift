import XCTest
@testable import AViewerMac

final class OverlayArrowRendererTests: XCTestCase {

    private let source = CGRect(x: 100, y: 100, width: 80, height: 20)

    func testRouteLeavesAndEntersFacingBorders() throws {
        let target = CGRect(x: 100, y: 200, width: 80, height: 20)
        let route = try XCTUnwrap(
            OverlayArrowRenderer.orthogonalRoute(source: source, target: target))

        XCTAssertEqual(route.first?.y, source.maxY)
        XCTAssertEqual(route.last?.y, target.minY)
        // The connector runs along a lane halfway through the gap.
        XCTAssertTrue(route.contains { $0.y == 160 })
    }

    func testOverlappingRectanglesProduceNoRoute() {
        // There is no honest line-only route between overlapping rectangles:
        // any connector would cover content belonging to one of them.
        XCTAssertNil(OverlayArrowRenderer.orthogonalRoute(
            source: source, target: source.offsetBy(dx: 5, dy: 5)))
        XCTAssertNil(OverlayArrowRenderer.orthogonalRoute(
            source: source, target: source))
    }

    func testBoundaryPointSitsOnTheEdgeFacingTheTarget() {
        let point = OverlayArrowRenderer.boundaryPoint(
            source, toward: CGPoint(x: 300, y: 110))
        XCTAssertEqual(point.x, source.maxX)
    }

    func testConsecutiveDuplicatesCollapse() {
        let points = OverlayArrowRenderer.removeConsecutiveDuplicates(
            [.zero, .zero, CGPoint(x: 10, y: 0)])
        XCTAssertEqual(points.count, 2)
    }
}

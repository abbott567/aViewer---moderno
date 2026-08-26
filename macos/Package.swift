// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "AViewerMac",
    platforms: [.macOS(.v13)],
    targets: [
        .executableTarget(
            name: "AViewerMac",
            path: "Sources/AViewerMac",
            swiftSettings: [.unsafeFlags(["-parse-as-library"])]
        ),
        .testTarget(
            name: "AViewerMacTests",
            dependencies: ["AViewerMac"],
            path: "Tests/AViewerMacTests"
        )
    ]
)

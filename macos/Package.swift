// swift-tools-version:5.10
import PackageDescription

let package = Package(
    name: "Whirl",
    platforms: [.macOS(.v14)],
    targets: [
        // Pure logic (slice geometry, ordering, paging, hotkey presets). No UI, easy to test.
        .target(name: "WhirlCore", path: "Sources/WhirlCore"),
        .executableTarget(
            name: "Whirl",
            dependencies: ["WhirlCore"],
            path: "Sources/Whirl",
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("SwiftUI"),
                .linkedFramework("Carbon"),
                .linkedFramework("ServiceManagement"),
            ]
        ),
        .testTarget(name: "WhirlCoreTests", dependencies: ["WhirlCore"], path: "Tests/WhirlCoreTests"),
    ]
)

import AppKit
import WhirlCore

/// `Whirl --self-test`: drives the real wheel (panel, hit testing, switching) without needing
/// a human or any permission, and checks that the chosen app really comes to the front.
///
/// It launches Calculator and TextEdit, opens the wheel, steers the pointer onto the slice of each
/// app and "releases the key", then waits for macOS to report that app as frontmost.
@MainActor
enum SelfTest {
    private static var failures = 0

    static func run() async -> Int32 {
        AppCatalog.shared.start()
        let targets = ["com.apple.calculator", "com.apple.TextEdit"]
        var launched: [NSRunningApplication] = []

        for id in targets where NSRunningApplication.runningApplications(withBundleIdentifier: id).isEmpty {
            guard let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: id) else {
                check("\(id) is installed", false); continue
            }
            let configuration = NSWorkspace.OpenConfiguration()
            configuration.activates = false
            if let app = try? await NSWorkspace.shared.openApplication(at: url, configuration: configuration) {
                launched.append(app)
            }
        }
        try? await Task.sleep(for: .seconds(1.5))

        let controller = WheelController.shared
        let state = WheelState.shared
        var chosen: SwitchItem?
        controller.perform = { chosen = $0; Switcher.activate($0) }

        // Step through the apps twice so the second switch proves we really left the first one.
        for id in targets + [targets[0]] {
            let items = AppCatalog.shared.items(mode: .apps, pinned: [])
            check("wheel lists \(id)", items.contains { $0.bundleID == id })

            chosen = nil
            controller.open(driveManually: true)
            check("wheel panel is visible", controller.isOpen)

            // Find the page and slice holding the app, then point at the middle of that slice.
            var found = false
            for _ in 0..<state.pageCount {
                if let slot = state.pageItems.firstIndex(where: { $0?.bundleID == id }) {
                    state.updateHover(at: WheelGeometry.point(forSlice: slot))
                    found = true
                    break
                }
                state.nextPage()
            }
            check("\(id) has a slice", found)
            check("hovering its slice selects it", state.hoveredItem?.bundleID == id)

            controller.finish(commit: true, allowQuickTap: false)
            check("choice reached the switcher", chosen?.bundleID == id)
            let arrived = await waitForFront(id)
            check("\(id) is now in front", arrived)
        }

        // Releasing over the hub cancels and leaves the front app alone.
        chosen = nil
        controller.open(driveManually: true)
        state.updateHover(at: CGPoint(x: WheelGeometry.size / 2, y: WheelGeometry.size / 2))
        controller.finish(commit: true, allowQuickTap: false)
        check("releasing on the hub cancels", chosen == nil)

        // The quick-tap target is the app used just before the current one.
        let items = AppCatalog.shared.items(mode: .apps, pinned: [])
        check("quick tap targets the previous app",
              ItemOrdering.previous(in: items, recency: AppCatalog.shared.recencyPIDs)?.bundleID == targets[1])

        controller.finish(commit: false)
        for app in launched { app.terminate() }
        print(failures == 0 ? "\nAll checks passed." : "\n\(failures) check(s) failed.")
        return failures == 0 ? 0 : 1
    }

    private static func waitForFront(_ bundleID: String) async -> Bool {
        for _ in 0..<40 {
            if NSWorkspace.shared.frontmostApplication?.bundleIdentifier == bundleID { return true }
            try? await Task.sleep(for: .milliseconds(100))
        }
        return false
    }

    private static func check(_ name: String, _ ok: Bool) {
        if !ok { failures += 1 }
        print("  \(ok ? "✓" : "✗") \(name)")
    }
}

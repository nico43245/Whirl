import AppKit
import WhirlCore

/// Brings an app or window to the front.
@MainActor
enum Switcher {
    static func activate(_ item: SwitchItem) {
        log.info("switching to \(item.bundleID ?? "?", privacy: .public)")
        guard let pid = item.pid, let app = NSRunningApplication(processIdentifier: pid), !app.isTerminated else {
            launch(item)
            return
        }
        bringToFront(app)
        if case .window(let index) = item.kind {
            raiseWindow(of: pid, index: index, title: item.title)
        }
    }

    /// Same route the Dock takes, so hidden and window-less apps behave like a Dock click.
    private static func bringToFront(_ app: NSRunningApplication) {
        if app.isHidden { app.unhide() }
        if let url = app.bundleURL {
            let configuration = NSWorkspace.OpenConfiguration()
            configuration.activates = true
            NSWorkspace.shared.openApplication(at: url, configuration: configuration) { _, error in
                if let error { log.error("activate failed: \(error.localizedDescription, privacy: .public)") }
            }
        } else {
            app.activate(options: [.activateAllWindows])
        }
    }

    private static func launch(_ item: SwitchItem) {
        guard let id = item.bundleID, let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: id) else { return }
        let configuration = NSWorkspace.OpenConfiguration()
        configuration.activates = true
        NSWorkspace.shared.openApplication(at: url, configuration: configuration) { _, error in
            if let error { log.error("launch failed: \(error.localizedDescription, privacy: .public)") }
        }
    }

    private static func raiseWindow(of pid: Int32, index: Int, title: String) {
        let windows = WindowLister.windows(of: pid)
        // Prefer the same title (the order may have changed since the wheel opened), else the same position.
        let target = windows.first { $0.title == title } ?? (index < windows.count ? windows[index] : nil)
        if let target { WindowLister.raise(target) }
    }
}

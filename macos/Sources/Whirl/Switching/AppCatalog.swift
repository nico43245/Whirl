import AppKit
import WhirlCore

/// Knows which apps (and windows) are open and in what order they were used.
///
/// Only public NSWorkspace information is used: the list of running apps and which one is
/// frontmost. Nothing is read from inside the apps except, in Windows mode, window titles
/// through the Accessibility API.
@MainActor
final class AppCatalog {
    static let shared = AppCatalog()

    private var recency = RecencyTracker()
    private var observers: [NSObjectProtocol] = []

    var recencyPIDs: [Int32] { recency.pids }

    func start() {
        let workspace = NSWorkspace.shared
        let apps = Self.regularApps()
        recency.seed(frontmost: workspace.frontmostApplication?.processIdentifier,
                     others: apps.sorted { ($0.launchDate ?? .distantPast) > ($1.launchDate ?? .distantPast) }
                                 .map(\.processIdentifier))
        let center = workspace.notificationCenter
        observers.append(center.addObserver(forName: NSWorkspace.didActivateApplicationNotification, object: nil, queue: .main) { [weak self] note in
            guard let app = note.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication,
                  app.activationPolicy == .regular, app.processIdentifier != getpid() else { return }
            MainActor.assumeIsolated { self?.recency.activated(app.processIdentifier) }
        })
        observers.append(center.addObserver(forName: NSWorkspace.didTerminateApplicationNotification, object: nil, queue: .main) { [weak self] note in
            guard let app = note.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication else { return }
            MainActor.assumeIsolated { self?.recency.terminated(app.processIdentifier) }
        })
    }

    static func regularApps() -> [NSRunningApplication] {
        NSWorkspace.shared.runningApplications.filter {
            $0.activationPolicy == .regular && !$0.isTerminated && $0.processIdentifier != getpid()
        }
    }

    /// Everything the wheel can show right now, in wheel order.
    func items(mode: SwitchMode, pinned: [PinnedApp]) -> [SwitchItem] {
        let frontmost = NSWorkspace.shared.frontmostApplication?.processIdentifier
        let apps = Self.regularApps()
        let infos = apps.enumerated().map { offset, app in
            RunningAppInfo(pid: app.processIdentifier, bundleID: app.bundleIdentifier,
                           name: app.localizedName ?? app.bundleIdentifier ?? "App",
                           launchOrder: Int(app.launchDate?.timeIntervalSince1970 ?? Double(offset)))
        }
        switch mode {
        case .apps:
            return ItemOrdering.appItems(running: infos, pinned: pinned, recency: recency.pids, frontmost: frontmost)
        case .windows:
            let ordered = ItemOrdering.appItems(running: infos, pinned: [], recency: recency.pids, frontmost: frontmost)
            return ordered.flatMap { app -> [SwitchItem] in
                guard let pid = app.pid else { return [] }
                let windows = WindowLister.windows(of: pid)
                if windows.isEmpty { return [app] }   // keep window-less apps reachable
                return windows.enumerated().map { index, window in
                    SwitchItem(id: "\(pid):\(index)", title: window.title.isEmpty ? app.title : window.title,
                               subtitle: app.title, bundleID: app.bundleID, pid: pid,
                               kind: .window(index: index), isFrontmost: app.isFrontmost && index == 0)
                }
            }
        }
    }
}

/// App icons for the wheel, cached by bundle id.
@MainActor
enum IconCache {
    private static var cache: [String: NSImage] = [:]

    static func icon(for item: SwitchItem) -> NSImage {
        let key = item.bundleID ?? "pid:\(item.pid ?? 0)"
        if let hit = cache[key] { return hit }
        var image: NSImage?
        if let id = item.bundleID, let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: id) {
            image = NSWorkspace.shared.icon(forFile: url.path)
        }
        if image == nil, let pid = item.pid, let app = NSRunningApplication(processIdentifier: pid) { image = app.icon }
        let result = image ?? NSWorkspace.shared.icon(for: .applicationBundle)
        cache[key] = result
        return result
    }
}

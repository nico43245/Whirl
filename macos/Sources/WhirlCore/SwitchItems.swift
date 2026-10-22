import Foundation

/// One thing the wheel can switch to: an app, or (in window mode) one window of an app.
public struct SwitchItem: Identifiable, Equatable {
    public enum Kind: Equatable {
        case app
        /// `index` is the position in the app's accessibility window list when the wheel opened.
        case window(index: Int)
    }

    public var id: String
    public var title: String
    /// The owning app's name when `title` is a window title.
    public var subtitle: String?
    public var bundleID: String?
    /// nil when the app is pinned but not running.
    public var pid: Int32?
    public var kind: Kind
    public var isPinned: Bool
    public var isFrontmost: Bool

    public var isRunning: Bool { pid != nil }

    public init(id: String, title: String, subtitle: String? = nil, bundleID: String?, pid: Int32?,
                kind: Kind = .app, isPinned: Bool = false, isFrontmost: Bool = false) {
        self.id = id
        self.title = title
        self.subtitle = subtitle
        self.bundleID = bundleID
        self.pid = pid
        self.kind = kind
        self.isPinned = isPinned
        self.isFrontmost = isFrontmost
    }
}

/// A running app, reduced to what ordering needs.
public struct RunningAppInfo: Equatable {
    public var pid: Int32
    public var bundleID: String?
    public var name: String
    public var launchOrder: Int   // larger = launched more recently

    public init(pid: Int32, bundleID: String?, name: String, launchOrder: Int) {
        self.pid = pid
        self.bundleID = bundleID
        self.name = name
        self.launchOrder = launchOrder
    }
}

/// Remembers the order apps were last used in (most recent first).
public struct RecencyTracker {
    public private(set) var pids: [Int32] = []

    public init() {}

    public mutating func activated(_ pid: Int32) {
        pids.removeAll { $0 == pid }
        pids.insert(pid, at: 0)
    }

    public mutating func terminated(_ pid: Int32) {
        pids.removeAll { $0 == pid }
    }

    public mutating func seed(frontmost: Int32?, others: [Int32]) {
        pids = []
        for pid in others.reversed() { activated(pid) }
        if let frontmost { activated(frontmost) }
    }
}

public enum ItemOrdering {
    /// Pinned apps first (in the order the user pinned them, running or not), then every other
    /// running app, most recently used first.
    public static func appItems(running: [RunningAppInfo], pinned: [PinnedApp],
                                recency: [Int32], frontmost: Int32?) -> [SwitchItem] {
        func rank(_ app: RunningAppInfo) -> Int {
            // Apps we never saw activate come after the tracked ones, newest launch first.
            recency.firstIndex(of: app.pid) ?? (recency.count + 1_000_000 - app.launchOrder)
        }
        var items: [SwitchItem] = []
        var used = Set<Int32>()

        for pin in pinned {
            let match = running
                .filter { $0.bundleID == pin.bundleID }
                .min { rank($0) < rank($1) }
            if let match { used.insert(match.pid) }
            items.append(SwitchItem(id: pin.bundleID, title: match?.name ?? pin.name, bundleID: pin.bundleID,
                                    pid: match?.pid, isPinned: true,
                                    isFrontmost: match?.pid == frontmost && frontmost != nil))
        }
        for app in running.sorted(by: { rank($0) < rank($1) }) where !used.contains(app.pid) {
            items.append(SwitchItem(id: app.bundleID ?? "pid:\(app.pid)", title: app.name,
                                    bundleID: app.bundleID, pid: app.pid,
                                    isFrontmost: app.pid == frontmost))
        }
        return items
    }

    /// The item a quick tap of the hotkey should jump to: the app used just before the current one.
    public static func previous(in items: [SwitchItem], recency: [Int32]) -> SwitchItem? {
        let current = items.first { $0.isFrontmost }?.pid
        for pid in recency where pid != current {
            if let item = items.first(where: { $0.pid == pid }) { return item }
        }
        return nil
    }
}

/// An app the user pinned to the wheel.
public struct PinnedApp: Codable, Equatable, Hashable {
    public var bundleID: String
    public var name: String
    public init(bundleID: String, name: String) {
        self.bundleID = bundleID
        self.name = name
    }
}

/// Splits items into pages of eight.
public enum Pager {
    public static func pageCount(for itemCount: Int) -> Int {
        max(1, (itemCount + WheelGeometry.sliceCount - 1) / WheelGeometry.sliceCount)
    }

    public static func page(_ items: [SwitchItem], index: Int) -> [SwitchItem?] {
        let size = WheelGeometry.sliceCount
        let start = index * size
        return (0..<size).map { slot in
            let i = start + slot
            return i < items.count ? items[i] : nil
        }
    }

    public static func wrapped(_ index: Int, by step: Int, pageCount: Int) -> Int {
        ((index + step) % pageCount + pageCount) % pageCount
    }
}

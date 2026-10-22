import AppKit
import Combine
import WhirlCore

/// Live state of the wheel: which page, which slice is under the cursor.
@MainActor
final class WheelState: ObservableObject {
    static let shared = WheelState()

    @Published private(set) var items: [SwitchItem] = []
    @Published var pageIndex = 0
    @Published var mode: SwitchMode = .apps
    @Published var hit: WheelHit = .none
    /// Shown in the hub when Windows mode cannot see any window.
    @Published var needsAccessibility = false

    private var lastPageSwitch = Date.distantPast
    private var scrollAccumulator: CGFloat = 0

    var pageCount: Int { Pager.pageCount(for: items.count) }
    var pageItems: [SwitchItem?] { Pager.page(items, index: pageIndex) }

    var hoveredItem: SwitchItem? {
        guard case .slice(let index) = hit else { return nil }
        return pageItems[index]
    }

    func load(_ items: [SwitchItem], mode: SwitchMode) {
        self.items = items
        self.mode = mode
        pageIndex = 0
        hit = .none
        scrollAccumulator = 0
    }

    /// `point` is in wheel coordinates with the origin top-left. Empty slices are not selectable.
    func updateHover(at point: CGPoint) {
        var newHit = WheelGeometry.hit(at: point)
        if case .slice(let index) = newHit, pageItems[index] == nil { newHit = .none }
        if newHit != hit { hit = newHit }
    }

    func nextPage() { switchPage(by: 1) }
    func previousPage() { switchPage(by: -1) }

    func switchPage(by step: Int) {
        guard pageCount > 1 else { return }
        pageIndex = Pager.wrapped(pageIndex, by: step, pageCount: pageCount)
        lastPageSwitch = Date()
    }

    /// Mouse wheel or two-finger swipe; debounced so one flick moves one page.
    func handleScroll(delta: CGFloat) {
        guard Date().timeIntervalSince(lastPageSwitch) > 0.35 else { scrollAccumulator = 0; return }
        scrollAccumulator += delta
        if abs(scrollAccumulator) > 6 {
            switchPage(by: scrollAccumulator > 0 ? -1 : 1)
            scrollAccumulator = 0
        }
    }
}

import XCTest
@testable import WhirlCore

final class GeometryTests: XCTestCase {
    func testEverySliceIsHitAtItsOwnCenter() {
        for index in 0..<WheelGeometry.sliceCount {
            XCTAssertEqual(WheelGeometry.hit(at: WheelGeometry.point(forSlice: index)), .slice(index))
        }
    }

    func testSliceZeroIsAtTwelveOClockAndOrderIsClockwise() {
        XCTAssertEqual(WheelGeometry.sliceIndex(dx: 0, dy: -100), 0)
        XCTAssertEqual(WheelGeometry.sliceIndex(dx: 100, dy: 0), 2)
        XCTAssertEqual(WheelGeometry.sliceIndex(dx: 0, dy: 100), 4)
        XCTAssertEqual(WheelGeometry.sliceIndex(dx: -100, dy: 0), 6)
    }

    func testHubAndOutside() {
        let c = WheelGeometry.size / 2
        XCTAssertEqual(WheelGeometry.hit(at: CGPoint(x: c, y: c)), .hub)
        XCTAssertEqual(WheelGeometry.hit(at: CGPoint(x: c, y: c + WheelGeometry.hubRadius - 1)), .hub)
        XCTAssertEqual(WheelGeometry.hit(at: CGPoint(x: 0, y: 0)), .none)
        XCTAssertEqual(WheelGeometry.hit(at: CGPoint(x: c, y: c - WheelGeometry.outerRadius - 100)), .none)
    }
}

final class OrderingTests: XCTestCase {
    private func app(_ pid: Int32, _ id: String, launch: Int = 0) -> RunningAppInfo {
        RunningAppInfo(pid: pid, bundleID: id, name: id.uppercased(), launchOrder: launch)
    }

    func testMostRecentlyUsedFirst() {
        let running = [app(1, "a"), app(2, "b"), app(3, "c")]
        let items = ItemOrdering.appItems(running: running, pinned: [], recency: [3, 1, 2], frontmost: 3)
        XCTAssertEqual(items.map(\.id), ["c", "a", "b"])
        XCTAssertTrue(items[0].isFrontmost)
    }

    func testPinnedComeFirstAndKeepTheirOrderEvenWhenNotRunning() {
        let running = [app(1, "a"), app(2, "b")]
        let pinned = [PinnedApp(bundleID: "z", name: "Zed"), PinnedApp(bundleID: "b", name: "B")]
        let items = ItemOrdering.appItems(running: running, pinned: pinned, recency: [1, 2], frontmost: 1)
        XCTAssertEqual(items.map(\.id), ["z", "b", "a"])
        XCTAssertNil(items[0].pid)
        XCTAssertEqual(items[0].title, "Zed")
        XCTAssertEqual(items[1].pid, 2)
        XCTAssertTrue(items[0].isPinned && items[1].isPinned && !items[2].isPinned)
    }

    func testQuickTapGoesToPreviousApp() {
        let running = [app(1, "a"), app(2, "b"), app(3, "c")]
        let items = ItemOrdering.appItems(running: running, pinned: [], recency: [2, 3, 1], frontmost: 2)
        XCTAssertEqual(ItemOrdering.previous(in: items, recency: [2, 3, 1])?.id, "c")
    }

    func testQuickTapWithNothingElseRunning() {
        let items = ItemOrdering.appItems(running: [app(1, "a")], pinned: [], recency: [1], frontmost: 1)
        XCTAssertNil(ItemOrdering.previous(in: items, recency: [1]))
    }

    func testUntrackedAppsFollowTrackedOnesNewestFirst() {
        let running = [app(1, "a", launch: 1), app(2, "b", launch: 5), app(3, "c", launch: 3)]
        let items = ItemOrdering.appItems(running: running, pinned: [], recency: [3], frontmost: 3)
        XCTAssertEqual(items.map(\.id), ["c", "b", "a"])
    }

    func testRecencyTracker() {
        var tracker = RecencyTracker()
        tracker.seed(frontmost: 2, others: [1, 2, 3])
        XCTAssertEqual(tracker.pids, [2, 1, 3])
        tracker.activated(3)
        XCTAssertEqual(tracker.pids, [3, 2, 1])
        tracker.terminated(2)
        XCTAssertEqual(tracker.pids, [3, 1])
    }
}

final class PagerTests: XCTestCase {
    private func items(_ n: Int) -> [SwitchItem] {
        (0..<n).map { SwitchItem(id: "\($0)", title: "\($0)", bundleID: nil, pid: Int32($0)) }
    }

    func testPageCount() {
        XCTAssertEqual(Pager.pageCount(for: 0), 1)
        XCTAssertEqual(Pager.pageCount(for: 8), 1)
        XCTAssertEqual(Pager.pageCount(for: 9), 2)
        XCTAssertEqual(Pager.pageCount(for: 24), 3)
    }

    func testPagePadsWithEmptySlots() {
        let page = Pager.page(items(10), index: 1)
        XCTAssertEqual(page.count, 8)
        XCTAssertEqual(page[0]?.id, "8")
        XCTAssertEqual(page[1]?.id, "9")
        XCTAssertNil(page[2])
    }

    func testWrapping() {
        XCTAssertEqual(Pager.wrapped(0, by: -1, pageCount: 3), 2)
        XCTAssertEqual(Pager.wrapped(2, by: 1, pageCount: 3), 0)
    }
}

final class HotkeyTests: XCTestCase {
    func testDefaultIsOptionTab() {
        XCTAssertEqual(HotkeyPreset.default.id, "option-tab")
        XCTAssertEqual(HotkeyPreset.default.keyCode, 48)
        XCTAssertEqual(HotkeyPreset.default.modifiers, 0x0800)
    }

    func testUnknownIDFallsBack() {
        XCTAssertEqual(HotkeyPreset.preset(id: "nope"), .default)
    }

    func testPresetIDsAreUnique() {
        XCTAssertEqual(Set(HotkeyPreset.all.map(\.id)).count, HotkeyPreset.all.count)
    }
}

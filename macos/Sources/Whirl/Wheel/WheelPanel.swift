import AppKit
import SwiftUI
import WhirlCore

/// A borderless, transparent panel that floats under the cursor. It never takes focus, so the app
/// you are leaving stays active until you pick a slice.
@MainActor
final class WheelPanel: NSPanel {
    static let shared = WheelPanel()

    let state = WheelState.shared
    /// Called when the user clicks a slice instead of releasing the key.
    var onClick: (() -> Void)?

    private init() {
        let size = WheelGeometry.size
        super.init(contentRect: NSRect(x: 0, y: 0, width: size, height: size),
                   styleMask: [.borderless, .nonactivatingPanel],
                   backing: .buffered, defer: false)
        level = .popUpMenu
        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        isFloatingPanel = true
        hidesOnDeactivate = false
        becomesKeyOnlyIfNeeded = true
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .transient, .ignoresCycle]

        let host = NSHostingView(rootView: WheelView(state: state))
        host.frame = NSRect(x: 0, y: 0, width: size, height: size)
        contentView = host
    }

    override var canBecomeKey: Bool { false }

    /// True from `show()` until `hide()`; the fade animations lag behind this.
    private(set) var isShowing = false

    /// Shows the wheel centred on the cursor (kept fully on screen) and returns the cursor position.
    @discardableResult
    func show() -> CGPoint {
        let size = WheelGeometry.size
        let mouse = NSEvent.mouseLocation
        var origin = NSPoint(x: mouse.x - size / 2, y: mouse.y - size / 2)
        if let screen = NSScreen.screens.first(where: { NSMouseInRect(mouse, $0.frame, false) }) {
            let f = screen.visibleFrame
            origin.x = min(max(origin.x, f.minX), f.maxX - size)
            origin.y = min(max(origin.y, f.minY), f.maxY - size)
        }
        setFrameOrigin(origin)
        isShowing = true
        alphaValue = 0
        orderFrontRegardless()
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.12
            animator().alphaValue = 1
        }
        return mouse
    }

    func hide() {
        guard isShowing else { return }
        isShowing = false
        NSAnimationContext.runAnimationGroup({ ctx in
            ctx.duration = 0.1
            animator().alphaValue = 0
        }, completionHandler: { [weak self] in
            Task { @MainActor in
                guard let self, !self.isShowing else { return }   // shown again meanwhile
                self.orderOut(nil)
            }
        })
    }

    /// Wheel coordinates (origin top-left) of a screen point.
    func wheelPoint(forScreenPoint p: CGPoint) -> CGPoint {
        CGPoint(x: p.x - frame.minX, y: WheelGeometry.size - (p.y - frame.minY))
    }

    override func scrollWheel(with event: NSEvent) {
        state.handleScroll(delta: event.scrollingDeltaY != 0 ? event.scrollingDeltaY : event.deltaY)
    }

    override func mouseDown(with event: NSEvent) {
        onClick?()
    }
}

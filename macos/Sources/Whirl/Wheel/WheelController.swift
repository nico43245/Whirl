import AppKit
import WhirlCore

/// Runs one "hold the shortcut, pick a slice, let go" interaction.
///
/// The wheel stays up while the shortcut's modifier keys are held. Releasing them over a slice
/// switches to it; releasing over the middle (or pressing Esc) cancels. A very quick tap that
/// barely moves the mouse jumps back to the previous app instead, like ⌘Tab.
@MainActor
final class WheelController {
    static let shared = WheelController()

    private let panel = WheelPanel.shared
    private let state = WheelState.shared
    private var timer: Timer?
    private var requiredFlags: NSEvent.ModifierFlags = .option
    private var escapeKeys: [HotKey] = []
    private var openedAt = Date()
    private var openPoint = CGPoint.zero

    /// Replaced by the self-test to observe the choice without switching.
    var perform: (SwitchItem) -> Void = { Switcher.activate($0) }

    var isOpen: Bool { panel.isShowing }

    private init() {
        panel.onClick = { [weak self] in self?.finish(commit: true, allowQuickTap: false) }
    }

    /// The shortcut was pressed. Pressing it again while the wheel is up turns the page.
    func hotkeyPressed() {
        if isOpen { state.nextPage() } else { open() }
    }

    /// `driveManually` skips the live timer so a test can steer the wheel itself.
    func open(driveManually: Bool = false) {
        let settings = Settings.shared
        var mode = settings.mode
        var needsAccessibility = false
        if mode == .windows && !WindowLister.isTrusted {
            mode = .apps
            needsAccessibility = true
        }
        let items = AppCatalog.shared.items(mode: mode, pinned: settings.pinned)
        guard !items.isEmpty else { return }

        state.load(items, mode: mode)
        state.needsAccessibility = needsAccessibility
        requiredFlags = Self.flags(for: settings.hotkey)
        openedAt = Date()
        openPoint = panel.show()
        log.info("wheel opened with \(items.count) item(s)")

        escapeKeys = [HotKey(keyCode: 53, modifiers: 0) { [weak self] in self?.finish(commit: false) },
                      HotKey(keyCode: 53, modifiers: settings.hotkey.modifiers) { [weak self] in self?.finish(commit: false) }]
            .compactMap { $0 }

        if !driveManually {
            let timer = Timer(timeInterval: 1.0 / 60, repeats: true) { [weak self] _ in
                Task { @MainActor in self?.tick() }
            }
            RunLoop.main.add(timer, forMode: .common)
            self.timer = timer
        }
    }

    private func tick() {
        guard isOpen else { stopTracking(); return }
        state.updateHover(at: panel.wheelPoint(forScreenPoint: NSEvent.mouseLocation))
        let held = NSEvent.modifierFlags.intersection(.deviceIndependentFlagsMask)
        if !held.contains(requiredFlags) { finish(commit: true) }
    }

    /// Ends the interaction. With `commit`, switches to whatever is under the cursor.
    func finish(commit: Bool, allowQuickTap: Bool = true) {
        guard isOpen else { return }
        stopTracking()
        panel.hide()
        guard commit else { log.info("wheel cancelled"); return }

        var target = state.hoveredItem
        if target == nil, allowQuickTap, Settings.shared.quickTapSwitchesBack,
           Date().timeIntervalSince(openedAt) < 0.3,
           hypot(NSEvent.mouseLocation.x - openPoint.x, NSEvent.mouseLocation.y - openPoint.y) < 12 {
            target = ItemOrdering.previous(in: state.items, recency: AppCatalog.shared.recencyPIDs)
        }
        guard let target else { return }
        perform(target)
    }

    private func stopTracking() {
        timer?.invalidate()
        timer = nil
        escapeKeys.forEach { $0.unregister() }
        escapeKeys = []
    }

    private static func flags(for preset: HotkeyPreset) -> NSEvent.ModifierFlags {
        var flags: NSEvent.ModifierFlags = []
        if preset.modifiers & HotkeyPreset.option != 0 { flags.insert(.option) }
        if preset.modifiers & HotkeyPreset.control != 0 { flags.insert(.control) }
        return flags
    }
}

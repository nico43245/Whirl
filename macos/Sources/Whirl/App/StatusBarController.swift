import AppKit
import WhirlCore

/// The menu bar icon and its menu.
@MainActor
final class StatusBarController: NSObject, NSMenuDelegate {
    private let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private let menu = NSMenu()

    override init() {
        super.init()
        if let button = item.button {
            button.image = NSImage(systemSymbolName: "circle.hexagongrid.circle", accessibilityDescription: "Whirl")
            button.image?.isTemplate = true
            button.toolTip = "Whirl — hold \(Settings.shared.hotkey.title)"
        }
        menu.delegate = self
        item.menu = menu
    }

    /// Rebuilt every time it opens so the pinned list and permission state are current.
    func menuNeedsUpdate(_ menu: NSMenu) {
        menu.removeAllItems()
        let settings = Settings.shared

        let header = NSMenuItem(title: "Hold \(settings.hotkey.title), move to a slice, let go", action: nil, keyEquivalent: "")
        header.isEnabled = false
        menu.addItem(header)
        if settings.mode == .windows && !WindowLister.isTrusted {
            menu.addItem(entry("⚠︎ Windows mode needs Accessibility…", #selector(openAccessibility)))
        }
        menu.addItem(.separator())

        let showMenu = NSMenu()
        for mode in SwitchMode.allCases {
            let row = entry(mode.title, #selector(pickMode(_:)))
            row.representedObject = mode.rawValue
            row.state = mode == settings.mode ? .on : .off
            showMenu.addItem(row)
        }
        let show = NSMenuItem(title: "Show", action: nil, keyEquivalent: "")
        show.submenu = showMenu
        menu.addItem(show)

        let pinMenu = NSMenu()
        let pinnedIDs = Set(settings.pinned.map(\.bundleID))
        for app in AppCatalog.regularApps().sorted(by: { ($0.localizedName ?? "") < ($1.localizedName ?? "") }) {
            guard let id = app.bundleIdentifier else { continue }
            let row = entry(app.localizedName ?? id, #selector(togglePin(_:)))
            row.representedObject = PinnedApp(bundleID: id, name: app.localizedName ?? id)
            row.state = pinnedIDs.contains(id) ? .on : .off
            pinMenu.addItem(row)
        }
        let pin = NSMenuItem(title: "Pin to Wheel", action: nil, keyEquivalent: "")
        pin.submenu = pinMenu
        menu.addItem(pin)

        let themeMenu = NSMenu()
        for theme in Theme.all {
            let row = entry("\(theme.emoji)  \(theme.name)", #selector(pickTheme(_:)))
            row.representedObject = theme.id
            row.state = theme.id == settings.themeID ? .on : .off
            themeMenu.addItem(row)
        }
        let themes = NSMenuItem(title: "Theme", action: nil, keyEquivalent: "")
        themes.submenu = themeMenu
        menu.addItem(themes)

        menu.addItem(.separator())
        menu.addItem(entry("Preferences…", #selector(openPreferences), key: ","))
        menu.addItem(entry("Quit Whirl", #selector(quit), key: "q"))
    }

    private func entry(_ title: String, _ action: Selector?, key: String = "") -> NSMenuItem {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: key)
        item.target = self
        return item
    }

    // MARK: Actions

    @objc private func openPreferences() { PreferencesWindow.show() }
    @objc private func quit() { NSApp.terminate(nil) }

    @objc private func openAccessibility() {
        WindowLister.requestTrust()
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
            NSWorkspace.shared.open(url)
        }
    }

    @objc private func pickMode(_ sender: NSMenuItem) {
        if let raw = sender.representedObject as? String, let mode = SwitchMode(rawValue: raw) {
            Settings.shared.mode = mode
            if mode == .windows && !WindowLister.isTrusted { WindowLister.requestTrust() }
        }
    }

    @objc private func togglePin(_ sender: NSMenuItem) {
        guard let app = sender.representedObject as? PinnedApp else { return }
        Settings.shared.togglePin(app)
    }

    @objc private func pickTheme(_ sender: NSMenuItem) {
        if let id = sender.representedObject as? String { Settings.shared.themeID = id }
    }
}

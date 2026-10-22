import AppKit

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusBar: StatusBarController?
    private var hotKey: HotKey?

    func applicationDidFinishLaunching(_ notification: Notification) {
        statusBar = StatusBarController()
        AppCatalog.shared.start()
        _ = WheelPanel.shared   // build the wheel up front so the first open is instant

        registerHotKey()
        Settings.shared.onHotkeyChange = { [weak self] in self?.registerHotKey() }

        // Show the preferences once so people see the shortcut; it lives in the menu bar after that.
        if !UserDefaults.standard.bool(forKey: "didShowWelcome") {
            UserDefaults.standard.set(true, forKey: "didShowWelcome")
            PreferencesWindow.show()
        }
    }

    private func registerHotKey() {
        hotKey?.unregister()
        let preset = Settings.shared.hotkey
        hotKey = HotKey(keyCode: preset.keyCode, modifiers: preset.modifiers) {
            WheelController.shared.hotkeyPressed()
        }
        if hotKey == nil {
            log.error("could not register \(preset.title, privacy: .public); pick another shortcut in Preferences")
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        hotKey?.unregister()
    }
}

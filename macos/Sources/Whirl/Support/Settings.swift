import AppKit
import Combine
import ServiceManagement
import WhirlCore

/// What a slice stands for.
enum SwitchMode: String, CaseIterable, Identifiable {
    case apps
    case windows

    var id: String { rawValue }
    var title: String {
        switch self {
        case .apps: return "Apps"
        case .windows: return "Windows (needs Accessibility)"
        }
    }
}

/// User preferences, persisted in UserDefaults. Nothing else is ever stored.
final class Settings: ObservableObject {
    static let shared = Settings()

    private let defaults = UserDefaults.standard

    @Published var themeID: String {
        didSet { defaults.set(themeID, forKey: "themeID") }
    }
    @Published var hotkeyID: String {
        didSet { defaults.set(hotkeyID, forKey: "hotkeyID"); onHotkeyChange?() }
    }
    @Published var mode: SwitchMode {
        didSet { defaults.set(mode.rawValue, forKey: "mode") }
    }
    /// Release the hotkey within a fraction of a second, without moving, to jump to the previous app.
    @Published var quickTapSwitchesBack: Bool {
        didSet { defaults.set(quickTapSwitchesBack, forKey: "quickTap") }
    }
    @Published var pinned: [PinnedApp] {
        didSet { defaults.set(try? JSONEncoder().encode(pinned), forKey: "pinned") }
    }

    var onHotkeyChange: (() -> Void)?

    private init() {
        defaults.register(defaults: [
            "themeID": Theme.all[0].id,
            "hotkeyID": HotkeyPreset.default.id,
            "mode": SwitchMode.apps.rawValue,
            "quickTap": true,
        ])
        themeID = defaults.string(forKey: "themeID") ?? Theme.all[0].id
        hotkeyID = defaults.string(forKey: "hotkeyID") ?? HotkeyPreset.default.id
        mode = SwitchMode(rawValue: defaults.string(forKey: "mode") ?? "") ?? .apps
        quickTapSwitchesBack = defaults.bool(forKey: "quickTap")
        pinned = defaults.data(forKey: "pinned").flatMap { try? JSONDecoder().decode([PinnedApp].self, from: $0) } ?? []
    }

    var theme: Theme { Theme.all.first { $0.id == themeID } ?? Theme.all[0] }
    var hotkey: HotkeyPreset { HotkeyPreset.preset(id: hotkeyID) }

    func togglePin(_ app: PinnedApp) {
        if let index = pinned.firstIndex(where: { $0.bundleID == app.bundleID }) {
            pinned.remove(at: index)
        } else {
            pinned.append(app)
        }
    }

    // MARK: Launch at login

    var launchAtLogin: Bool {
        get { SMAppService.mainApp.status == .enabled }
        set {
            do {
                if newValue { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() }
            } catch {
                log.error("launch at login change failed: \(error.localizedDescription, privacy: .public)")
            }
            objectWillChange.send()
        }
    }
}

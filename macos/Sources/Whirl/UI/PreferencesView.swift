import SwiftUI
import WhirlCore

struct PreferencesView: View {
    @ObservedObject var settings = Settings.shared
    @State private var launchAtLogin = Settings.shared.launchAtLogin
    @State private var hasAccessibility = WindowLister.isTrusted

    var body: some View {
        Form {
            Section("Wheel") {
                Picker("Hold to open", selection: $settings.hotkeyID) {
                    ForEach(HotkeyPreset.all) { Text($0.title).tag($0.id) }
                }
                Picker("Slices show", selection: $settings.mode) {
                    ForEach(SwitchMode.allCases) { Text($0.title).tag($0) }
                }
                Toggle("Quick tap jumps back to the previous app", isOn: $settings.quickTapSwitchesBack)
                Toggle("Open at login", isOn: $launchAtLogin)
                    .onChange(of: launchAtLogin) { _, on in Settings.shared.launchAtLogin = on }
            }
            Section("Pinned apps") {
                if settings.pinned.isEmpty {
                    Text("Pinned apps always take the first slices, in the order you pin them, even when they are closed (choosing one launches it). Windows mode ignores pins.")
                        .font(.caption).foregroundStyle(.secondary)
                }
                ForEach(settings.pinned, id: \.bundleID) { app in
                    HStack {
                        Text(app.name)
                        Spacer()
                        Button { settings.togglePin(app) } label: { Image(systemName: "minus.circle") }
                            .buttonStyle(.borderless)
                            .help("Unpin")
                    }
                }
                Button("Add App…", action: addApp)
            }
            Section("Look") {
                Picker("Theme", selection: $settings.themeID) {
                    ForEach(Theme.all) { Text("\($0.emoji)  \($0.name)").tag($0.id) }
                }
                ThemeSwatches(selected: settings.themeID)
            }
            Section("Permissions") {
                LabeledContent("Apps mode") {
                    Label("Nothing needed", systemImage: "checkmark.circle.fill").foregroundStyle(.green)
                }
                LabeledContent("Windows mode (Accessibility)") {
                    if hasAccessibility {
                        Label("Granted", systemImage: "checkmark.circle.fill").foregroundStyle(.green)
                    } else {
                        Button("Grant…") {
                            WindowLister.requestTrust()
                            hasAccessibility = WindowLister.isTrusted
                        }
                    }
                }
            }
        }
        .formStyle(.grouped)
        .frame(width: 500, height: 600)
        .onReceive(Timer.publish(every: 2, on: .main, in: .common).autoconnect()) { _ in
            hasAccessibility = WindowLister.isTrusted
        }
    }

    private func addApp() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.applicationBundle]
        panel.directoryURL = URL(fileURLWithPath: "/Applications")
        panel.allowsMultipleSelection = false
        panel.prompt = "Pin"
        guard panel.runModal() == .OK, let url = panel.url, let bundle = Bundle(url: url),
              let id = bundle.bundleIdentifier else { return }
        let name = (bundle.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String)
            ?? (bundle.object(forInfoDictionaryKey: "CFBundleName") as? String)
            ?? url.deletingPathExtension().lastPathComponent
        if !settings.pinned.contains(where: { $0.bundleID == id }) {
            settings.pinned.append(PinnedApp(bundleID: id, name: name))
        }
    }
}

struct ThemeSwatches: View {
    let selected: String
    var body: some View {
        HStack(spacing: 10) {
            ForEach(Theme.all) { theme in
                Circle()
                    .fill(theme.accent)
                    .frame(width: 22, height: 22)
                    .overlay(Circle().stroke(theme.id == selected ? Color.primary : .clear, lineWidth: 2).padding(-3))
                    .onTapGesture { Settings.shared.themeID = theme.id }
                    .help(theme.name)
            }
        }
    }
}

/// Hosts SwiftUI content in a normal titled window, reusing it if already open.
@MainActor
enum WindowHost {
    private static var windows: [String: NSWindow] = [:]

    static func show<Content: View>(id: String, title: String, content: () -> Content) {
        if let existing = windows[id] {
            existing.makeKeyAndOrderFront(nil)
        } else {
            let window = NSWindow(contentViewController: NSHostingController(rootView: content()))
            window.title = title
            window.styleMask = [.titled, .closable, .miniaturizable]
            window.isReleasedWhenClosed = false
            window.center()
            windows[id] = window
            window.makeKeyAndOrderFront(nil)
        }
        NSApp.activate(ignoringOtherApps: true)
    }
}

enum PreferencesWindow {
    @MainActor static func show() {
        WindowHost.show(id: "preferences", title: "Whirl Preferences") { PreferencesView() }
    }
}

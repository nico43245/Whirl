import AppKit
import SwiftUI
import WhirlCore

MainActor.assumeIsolated {
    let app = NSApplication.shared
    let args = CommandLine.arguments

    // End-to-end check: Whirl --self-test   (opens the wheel for real and verifies the switch)
    if args.count >= 2, args[1] == "--self-test" {
        app.setActivationPolicy(.accessory)
        Task { @MainActor in exit(await SelfTest.run())}
        app.run()
    }

    // Prints what the wheel would list: Whirl --list [apps|windows]
    if args.count >= 2, args[1] == "--list" {
        AppCatalog.shared.start()
        let mode = args.count > 2 ? SwitchMode(rawValue: args[2]) ?? .apps : .apps
        for item in AppCatalog.shared.items(mode: mode, pinned: Settings.shared.pinned) {
            print("\(item.isFrontmost ? "*" : " ") \(item.bundleID ?? "?")\t\(item.title)\(item.subtitle.map { " — \($0)" } ?? "")")
        }
        exit(0)
    }

    // Design check: Whirl --snapshot <out.png> [hovered slice 0-7 | hub] [theme id]
    // Draws the wheel with a fixed set of Apple apps, so screenshots never show your own apps.
    if args.count >= 3, args[1] == "--snapshot" {
        let state = WheelState.shared
        let demo = [("com.apple.finder", "Finder"), ("com.apple.Safari", "Safari"), ("com.apple.mail", "Mail"),
                    ("com.apple.Notes", "Notes"), ("com.apple.iCal", "Calendar"), ("com.apple.Music", "Music"),
                    ("com.apple.Terminal", "Terminal"), ("com.apple.Photos", "Photos")]
        let items = demo.enumerated().map { index, app in
            SwitchItem(id: app.0, title: app.1, bundleID: app.0, pid: Int32(100 + index), isFrontmost: index == 0)
        }
        state.load(items, mode: .apps)
        if args.count > 3 {
            state.hit = args[3] == "hub" ? .hub : Int(args[3]).map { .slice($0) } ?? .none
        }
        let saved = Settings.shared.themeID
        if args.count > 4 { Settings.shared.themeID = args[4] }
        let renderer = ImageRenderer(content: WheelView(state: state, showsBlur: false))
        renderer.scale = 2
        guard let image = renderer.cgImage,
              let png = NSBitmapImageRep(cgImage: image).representation(using: .png, properties: [:]) else { exit(1) }
        try? png.write(to: URL(fileURLWithPath: args[2]))
        Settings.shared.themeID = saved
        exit(0)
    }

    let delegate = AppDelegate()
    app.delegate = delegate
    app.setActivationPolicy(.accessory)
    app.run()
}

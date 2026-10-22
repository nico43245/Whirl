import Foundation

/// Hold-to-show shortcuts offered in Preferences. Key codes and modifier masks are the
/// Carbon values (kVK_* / optionKey …), kept here as plain numbers so the table is testable.
public struct HotkeyPreset: Identifiable, Equatable {
    public let id: String
    public let title: String
    public let keyCode: UInt32
    /// Carbon modifier mask.
    public let modifiers: UInt32

    public static let option: UInt32 = 0x0800     // optionKey
    public static let control: UInt32 = 0x1000    // controlKey

    public static let all: [HotkeyPreset] = [
        HotkeyPreset(id: "option-tab", title: "⌥ Tab", keyCode: 48, modifiers: option),
        HotkeyPreset(id: "control-option-tab", title: "⌃⌥ Tab", keyCode: 48, modifiers: option | control),
        HotkeyPreset(id: "option-space", title: "⌥ Space", keyCode: 49, modifiers: option),
        HotkeyPreset(id: "control-space", title: "⌃ Space", keyCode: 49, modifiers: control),
        HotkeyPreset(id: "option-grave", title: "⌥ `", keyCode: 50, modifiers: option),
    ]

    public static let `default` = all[0]

    public static func preset(id: String) -> HotkeyPreset {
        all.first { $0.id == id } ?? .default
    }
}

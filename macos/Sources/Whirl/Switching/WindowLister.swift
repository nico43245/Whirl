import AppKit
import ApplicationServices

/// Lists and raises individual windows through the Accessibility API.
/// Needs the Accessibility permission; without it every call returns nothing / does nothing.
enum WindowLister {
    struct Window {
        var title: String
        var element: AXUIElement
    }

    static var isTrusted: Bool { AXIsProcessTrusted() }

    static func requestTrust() {
        let key = kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String
        _ = AXIsProcessTrustedWithOptions([key: true] as CFDictionary)
    }

    /// Standard windows of an app on the current Space, front to back, minimized ones included.
    static func windows(of pid: Int32) -> [Window] {
        guard isTrusted else { return [] }
        let app = AXUIElementCreateApplication(pid)
        AXUIElementSetMessagingTimeout(app, 0.25)   // a hung app must not freeze the wheel
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(app, kAXWindowsAttribute as CFString, &value) == .success,
              let elements = value as? [AXUIElement] else { return [] }
        return elements.compactMap { element in
            var subrole: CFTypeRef?
            AXUIElementCopyAttributeValue(element, kAXSubroleAttribute as CFString, &subrole)
            guard (subrole as? String) == kAXStandardWindowSubrole as String else { return nil }
            var title: CFTypeRef?
            AXUIElementCopyAttributeValue(element, kAXTitleAttribute as CFString, &title)
            return Window(title: (title as? String) ?? "", element: element)
        }
    }

    @discardableResult
    static func raise(_ window: Window) -> Bool {
        AXUIElementSetAttributeValue(window.element, kAXMinimizedAttribute as CFString, kCFBooleanFalse)
        let raised = AXUIElementPerformAction(window.element, kAXRaiseAction as CFString) == .success
        AXUIElementSetAttributeValue(window.element, kAXMainAttribute as CFString, kCFBooleanTrue)
        AXUIElementSetAttributeValue(window.element, kAXFocusedAttribute as CFString, kCFBooleanTrue)
        return raised
    }
}

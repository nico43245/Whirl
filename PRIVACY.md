# Privacy

Whirl works on your computer and nowhere else.

- **No network access.** The apps contain no networking code. There are no accounts, analytics, crash reporting, telemetry, advertising or update checks.
- **What it looks at.** Only which apps are open: their names, icons and (on Windows) window titles. In Windows mode on macOS it also reads window titles through the Accessibility API. It never reads the contents of any window, file or document, and takes no screenshots.
- **Keyboard and mouse.** Whirl needs to know when you hold its shortcut.
  - On macOS it registers one system-wide hotkey (Carbon `RegisterEventHotKey`), which needs no permission and gives the app no view of the keyboard beyond that one combination. While the wheel is open it also registers Esc.
  - On Windows it uses a low-level keyboard hook. The only keys it reacts to are Ctrl, Alt, Shift, Win, the shortcut's trigger key and Esc (while the wheel is open). Typed text is never read, stored or sent anywhere.
- **Settings.** Your preferences (theme, shortcut, mode and the apps you pinned) stay on your machine:
  - macOS: `~/Library/Preferences/io.github.nico43245.whirl.plist`
  - Windows: `%APPDATA%\Whirl\settings.json`
- **Nothing else is stored.** No history of the apps you switched to is kept, in memory or on disk, beyond the order of recent apps while Whirl is running.

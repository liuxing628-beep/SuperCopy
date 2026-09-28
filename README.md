# SuperCopy 1.4 — Clipboard Blocks

[Download the latest Windows ZIP](https://github.com/liuxing628-beep/SuperCopy/releases/latest) · [Source code](https://github.com/liuxing628-beep/SuperCopy)

Capture an Excel row or selected text, split it into reusable blocks, and click a block to paste it at your text cursor. SuperCopy runs in the Windows system tray and opens an adaptive panel beside your mouse pointer.

## Quick start

1. Download the Windows ZIP from [Releases](https://github.com/liuxing628-beep/SuperCopy/releases/latest), extract it, and run **SuperCopy.exe**. Windows 10/11 with **.NET Framework 4.8** is required. No app installation is needed.
2. On startup, press a middle, right, or side mouse button within 30 seconds to bind it. A side button is recommended. Press Esc to cancel, or choose **Rebind mouse button** to try again.
3. In Excel or another app, select the content you want to capture. Press and release **Ctrl+Shift+C**. SuperCopy sends Ctrl+C to that app and reads the new text. Keep the same window active until capture finishes.
4. Place the text cursor in your destination field, document, or Excel cell. Press your bound mouse button to open the panel. Hover to highlight a block, then left-click to paste it. The panel closes automatically and keeps the destination app's focus. Existing selected text is replaced according to that app's normal paste behavior.
5. Open the panel again to reuse the same blocks. Only a successful **Ctrl+Shift+C** capture replaces them. Ordinary copying, clicking a block, or clipboard changes from another app do not replace the saved set.
6. Double-click the system tray icon to open Settings. Closing Settings hides it; **Exit SuperCopy** fully exits the app and releases the mouse binding and shortcuts.

**Ctrl+Alt+F8** is a backup shortcut for opening the panel. It does not replace Excel's Ctrl+Alt+V Paste Special shortcut.

To update from an earlier release, exit the old copy from the system tray, extract the new package, and start the new executable. Bind your mouse button again.

## Adaptive block panel

- Short blocks appear side by side. Longer blocks take more width, with automatic wrapping into additional rows.
- Blocks retain their original order from left to right, then top to bottom. Each block displays its number.
- The panel sizes itself to the amount of content and stays within the screen's working area. Larger sets scroll vertically.
- Long text previews show up to three lines. Hover for a tooltip of up to 3,000 characters. Pasting always uses the full original block.
- Scroll with the mouse wheel or the scrollbar. If Windows is configured not to scroll inactive windows on hover, use the scrollbar.
- Press the bound mouse button again or click outside to close the panel. Esc also closes it when that shortcut is available.
- The panel does not take keyboard focus, so arrow keys and Enter remain available to your destination app.
- The panel draws only visible rows rather than creating a separate window control for every block.

## Splitting rules

Only **tabs and line breaks** split the captured text. Supported line breaks include CR, LF, CRLF, and Unicode line separators. Ordinary spaces, full-width spaces, non-breaking spaces, and leading/trailing spaces within a non-empty block are preserved. Empty or whitespace-only blocks are skipped.

For example, an Excel selection containing:

```text
John Smith<Tab>New York Office<Tab>42
Jane Doe<Tab>London Office<Tab>57
```

produces six blocks. `John Smith` and `New York Office` each remain a single block. The `<Tab>` notation above represents an actual tab character.

Punctuation, URLs, numbers, duplicate values, and Unicode text are preserved. SuperCopy reads plain text; it does not preserve Excel formatting or decode Excel's quoted text conventions.

Each capture supports up to 2,000,000 characters and 50,000 blocks. An empty, failed, or oversized capture leaves the previous set unchanged. Clipboard writes are retried briefly if another app has locked the clipboard.

Before pasting, SuperCopy waits for mouse buttons and modifier keys to be released. If the target window is closed, focus changes, or the clipboard changes unexpectedly, automatic pasting is canceled with a notification. The tray menu's **View saved blocks** is for viewing and copying only; open the panel from your destination app with the bound button or backup shortcut for automatic pasting.

## Mouse and Excel compatibility

- Supported buttons: middle, right, side button 1 (Back), and side button 2 (Forward). While bound, the button's normal action is replaced by opening the panel. Left-click remains available for normal use and selecting blocks.
- Mouse-driver-generated standard button events are supported. If your mouse software has a separate Excel profile, map its side button to standard Back or Forward in that profile.
- Driver-only DPI or macro buttons may not produce standard mouse events. Map one to a middle or side button in the manufacturer's software before binding it. Wheel scrolling is not a bindable button.
- Mouse monitoring runs on a dedicated thread so clipboard work on the UI thread does not block the mouse hook's message loop.
- The panel avoids taking focus from Excel, including during in-cell editing.
- If a side button does not open the panel, compare its behavior with **Ctrl+Alt+F8**. The Settings status area reports if the backup shortcut is already in use.
- If **Ctrl+Shift+C** is in use by another app, resolve the conflict and restart SuperCopy.
- If the target app runs as administrator, SuperCopy may need the same privilege level to send copy/paste input. SuperCopy does not request administrator privileges by default.
- If notifications are disabled, double-click the tray icon to read the latest status. Apps that do not support Ctrl+C or do not place plain text on the clipboard cannot be captured this way.
- The app uses system DPI scaling. Windows provides compatibility scaling between monitors with different scaling settings.

## Language and privacy

All application labels, notifications, status messages, and instructions are in English. The interface uses Segoe UI, with Windows font fallback for captured multilingual text. Your captured content is never translated.

Blocks and the mouse binding are kept only in process memory. SuperCopy does not write clipboard history files or make network requests. Exiting clears its saved blocks and binding; the last copied content remains in the Windows clipboard.

## Build on Windows

Extract the source package, keep its folder structure, and double-click **build.bat**. The script looks for the .NET Framework C# compiler included with Windows and produces:

```text
dist\SuperCopy.exe
dist\SuperCopy.exe.config
```

No Visual Studio installation or third-party package download is required for this build path. If the compiler is missing, install Microsoft .NET Framework 4.8 and retry. If the executable cannot be overwritten, exit the running copy first.

With the .NET SDK installed, you can also cross-compile and run the automated checks:

```sh
dotnet build SuperCopy.csproj -c Release -o dist
dotnet run --project tests/Tests.csproj -c Release
```

The SDK build downloads Microsoft's .NET Framework reference assemblies from NuGet. The test project requires the .NET 8 SDK. App source is restricted to C# 5 for compatibility with the Framework compiler. Multilingual strings in test fixtures verify Unicode preservation; they are not application UI text.

## Validation

The app is cross-compiled on macOS to a Windows PE executable. Automated checks cover text splitting, space preservation, Unicode, duplicate order, capture limits, preserving old blocks on failure, Win32 input structure layout, adaptive wrapping, narrow screens, hit testing, non-overlapping layouts, and 50,000-block sets.

The previous adaptive-layout version was reported to work well by its user. This English release has not been visually tested on Windows in the build environment. When trying a new setup, check mouse binding, Excel selection and in-cell editing, automatic pasting, scrolling and click targets, display scaling, and exiting/restoring normal mouse behavior.

References:
- [Microsoft: Low-level mouse hooks](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc)
- [Microsoft: SendInput and privilege restrictions](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)

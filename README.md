# SuperCopy 1.6 — Clipboard Blocks

[Download the latest Windows ZIP](https://github.com/liuxing628-beep/SuperCopy/releases/latest) · [Source code](https://github.com/liuxing628-beep/SuperCopy)

Capture an Excel row or selected text, split it into reusable blocks, and click a block to paste it at your text cursor. SuperCopy runs in the Windows system tray and opens an adaptive panel beside your mouse pointer.

## Quick start

1. Download the Windows ZIP from [Releases](https://github.com/liuxing628-beep/SuperCopy/releases/latest), extract it, and run **SuperCopy.exe**. Windows 10/11 with **.NET Framework 4.8** is required. No app installation is needed.
2. **Step 1 — Super Copy shortcut:** click the capture field, press your preferred combination, then click **Use capture shortcut**. Use Ctrl or Alt with a letter, number, function key, or punctuation key; Shift is optional. For example, Ctrl+Shift+C is a choice, not a fixed binding.
3. **Step 2 — Open panel shortcut:** record a different combination and click **Use panel shortcut**, or click **Or bind a mouse button** and press your preferred middle, right, or side button within 30 seconds. Setting a keyboard shortcut replaces the mouse binding, and vice versa. Both setup steps are required before background operation.
4. In Excel or another app, select content and press and release **your Super Copy shortcut**. SuperCopy sends Ctrl+C to that app and reads new text. Keep the same window active until capture finishes. A dialog offers **Append to existing**, **Replace all**, or **Cancel**. Appending adds new blocks at the end; replacing refreshes the entire saved set; canceling keeps the saved set unchanged. The dialog appears even for the first capture.
5. Place the text cursor in your destination field, document, or Excel cell. Use **your panel shortcut** to open the panel. Hover to highlight a block, then **left-click to paste** it. The panel closes automatically and keeps the destination app's focus. Existing selected text is replaced according to that app's normal paste behavior.
6. **Hold the left mouse button and drag a block** to change its position in the reading order. A blue insertion line shows the destination. Release to apply the move; other blocks automatically reflow. Drag near the top or bottom to scroll. Releasing outside the panel cancels the move. A click with only small pointer movement still pastes. The custom order remains when the panel is reopened and when new blocks are appended.
7. **Right-click a block to remove it** from the saved set. The panel stays open and immediately rearranges the remaining blocks. Only the clicked occurrence is removed when there are duplicates. Removing the last block closes the empty panel. Deletion does not change the clipboard or send paste input.
8. Open the panel again to reuse the saved set. Ordinary copying or clipboard changes from another app do not replace it. Double-click the system tray icon to open Settings and change shortcuts. Closing Settings hides it after setup; **Exit SuperCopy** fully exits the app and releases all bindings.

There are no automatically registered capture or panel shortcuts. Ctrl+C and Ctrl+V are reserved for native copy/paste, and the capture and panel shortcuts must differ. The app reports unavailable combinations so you can choose another. Shortcuts and mouse bindings apply only until exit.

To update from an earlier release, exit the old copy from the system tray, extract the new package, and start the new executable. Bind your mouse button again.

## Adaptive block panel

- Short blocks appear side by side. Longer blocks take more width, with automatic wrapping into additional rows.
- Blocks initially follow the captured order from left to right, then top to bottom. Dragging updates this saved order and automatically reflows the panel; positions follow the layout rather than fixed screen coordinates. Each block displays its current number.
- The panel sizes itself to the amount of content and stays within the screen's working area. Larger sets scroll vertically.
- Long text previews show up to three lines. Hover for a tooltip of up to 3,000 characters. Pasting always uses the full original block.
- Scroll with the mouse wheel or the scrollbar. If Windows is configured not to scroll inactive windows on hover, use the scrollbar.
- Use your panel shortcut again or click outside to close the panel. Esc also closes it when that shortcut is available.
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

Each capture and the combined saved set support up to 2,000,000 characters and 50,000 blocks. Appending is checked against both combined limits before any change is made. An empty, failed, or oversized capture leaves the previous set unchanged. Clipboard writes are retried briefly if another app has locked the clipboard.

Before pasting, SuperCopy waits for mouse buttons and modifier keys to be released. If the target window is closed, focus changes, or the clipboard changes unexpectedly, automatic pasting is canceled with a notification. The tray menu's **View saved blocks** is for viewing and copying only; open the panel from your destination app with your configured keyboard shortcut or mouse button for automatic pasting.

## Mouse and Excel compatibility

- Supported buttons: middle, right, side button 1 (Back), and side button 2 (Forward). While bound, the button's normal action is replaced by opening the panel. Left-click remains available for normal use and selecting blocks.
- Mouse-driver-generated standard button events are supported. If your mouse software has a separate Excel profile, map its side button to standard Back or Forward in that profile.
- Driver-only DPI or macro buttons may not produce standard mouse events. Map one to a middle or side button in the manufacturer's software before binding it. Wheel scrolling is not a bindable button.
- Mouse monitoring runs on a dedicated thread so clipboard work on the UI thread does not block the mouse hook's message loop.
- The panel avoids taking focus from Excel, including during in-cell editing.
- If a side button does not open the panel, open Settings and configure a keyboard panel shortcut for comparison. No fixed backup shortcut is registered.
- A mouse right-button binding opens/closes the panel outside its bounds. Inside the panel, right-clicking a block removes it instead of toggling the panel.
- If a chosen shortcut is already in use, choose another combination. The app preserves the previous registration when it can restore it after an unsuccessful change.
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

The app is cross-compiled on macOS to a Windows PE executable. Automated checks cover text splitting, space preservation, Unicode, duplicate order, capture limits, preserving old blocks on failure, Win32 input structure layout, adaptive wrapping, narrow screens, hit testing, non-overlapping layouts, 50,000-block sets, append/replace/cancel data behavior, exact removal of duplicates, combined append limits, shortcut validation, drag/click separation, insertion targets, canceled drags, exact movement of duplicate occurrences, and reorder invariants.

Previous releases were reported to work well by the user. This upgraded release has not been visually tested on Windows in the build environment. When trying a new setup, check mouse binding, Excel selection and in-cell editing, automatic pasting, scrolling and click targets, display scaling, and exiting/restoring normal mouse behavior.

References:
- [Microsoft: Low-level mouse hooks](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc)
- [Microsoft: SendInput and privilege restrictions](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)

## Changes in 1.5

- Two-step startup setup for a custom capture shortcut followed by a custom keyboard or mouse panel shortcut.
- A choice to append, replace, or cancel after every successful text capture.
- Right-click deletion with immediate adaptive rearrangement and persistent removal from the saved set.
- Left-click paste and space-preserving text splitting remain available.

Windows acceptance checks: configure two different shortcuts; try an occupied shortcut; test both keyboard and side-button panel activation in Excel; append two captures; cancel a third; replace the set; remove one duplicate, a block after scrolling, and the final block; verify left-click paste; test right-click deletion when the right button is itself the panel binding.

## Changes in 1.6

- Hold and drag a block with the left mouse button to reorder it, with a floating preview and blue insertion line.
- Drop applies the new saved order and automatically fills available row space. Reopening the panel retains the order; appended content goes after it.
- Drag near the panel's top or bottom to scroll through longer sets. An outside drop or lost mouse capture cancels the move without pasting.
- Ordinary left-click paste and right-click removal continue to work. Dragged blocks are never automatically pasted.

Windows acceptance checks for dragging: click with small pointer jitter; move within a row and across rows; move first/last blocks and duplicate occurrences; drag back to the source; drop outside the panel; drag while scrolling at both edges; reopen the panel and append new blocks; test Excel cell editing and display scaling to confirm no focus change or unintended paste.

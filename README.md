# SuperCopy — Clipboard Blocks

[Download Windows or macOS](https://github.com/liuxing628-beep/SuperCopy/releases/latest) · [Source code](https://github.com/liuxing628-beep/SuperCopy)

Capture an Excel row or selected text, split it into reusable blocks, and click a block to paste it at your text cursor. SuperCopy runs in the Windows system tray or macOS menu bar and opens an adaptive panel beside your mouse pointer.

## Quick start (Windows)

1. Download the Windows ZIP from [Releases](https://github.com/liuxing628-beep/SuperCopy/releases/latest), extract it, and run **SuperCopy.exe**. Windows 10/11 with **.NET Framework 4.8** is required. No app installation is needed.
2. **Step 1 — Super Copy shortcut:** click the capture field, press your preferred combination, then click **Use capture shortcut**. Use Ctrl or Alt with a letter, number, function key, or punctuation key; Shift is optional. For example, Ctrl+Shift+C is a choice, not a fixed binding.
3. **Step 2 — Open panel shortcut:** record a different combination and click **Use panel shortcut**, or click **Or bind a mouse button** and press your preferred middle, right, or side button within 30 seconds. Setting a keyboard shortcut replaces the mouse binding, and vice versa. Both setup steps are required before background operation.
4. In Excel or another app, select content and press and release **your Super Copy shortcut**. SuperCopy sends Ctrl+C to that app and reads new text. Keep the same window active until capture finishes. The choice dialog opens near the mouse on the source application's screen. If the mouse is on another monitor, it opens around the center of the source screen instead. Its location is saved when you trigger capture, so moving the mouse during copying does not move the dialog to another display. It avoids screen edges and the taskbar. A dialog offers **Add to front**, **Replace all**, or **Cancel**. Adding keeps existing content and places the new batch at the front, preserving the natural order within the new batch and the current order of older blocks; replacing refreshes the entire saved set; canceling keeps the saved set unchanged. The dialog appears even for the first capture.
5. Place the text cursor in your destination field, document, or Excel cell. Use **your panel shortcut** to open the panel. Hover to highlight a block, then **left-click to paste** it. The panel closes automatically and keeps the destination app's focus. Existing selected text is replaced according to that app's normal paste behavior.
6. **Hold the left mouse button and drag a block** to change its position in the reading order. A blue insertion line shows the destination near tile edges or gaps. Release there to apply the move; other blocks automatically reflow. To combine content, hold over the center of another block for about half a second: the target turns from amber to green and the preview says **Release to merge**. Release to combine target text first, then dragged text, separated by a Windows line break (CRLF). Releasing before it turns green cancels the drop. The combined block stays at the target position; other blocks fill the space. Drag near the top or bottom to scroll. Releasing outside the panel cancels the move. A click with only small pointer movement still pastes. The custom order remains when the panel is reopened and when new blocks are added.
7. **Right-click a block to remove it** from the saved set. The panel stays open and immediately rearranges the remaining blocks. Only the clicked occurrence is removed when there are duplicates. Removing the last block closes the empty panel. Deletion does not change the clipboard or send paste input.
8. Open the panel again to reuse the saved set. Ordinary copying or clipboard changes from another app do not replace it. Double-click the system tray icon to open Settings and change shortcuts. Closing Settings hides it after setup; **Exit SuperCopy** fully exits the app and releases all bindings.

There are no automatically registered capture or panel shortcuts. Ctrl+C and Ctrl+V are reserved for native copy/paste, and the capture and panel shortcuts must differ. The app reports unavailable combinations so you can choose another. Shortcuts and mouse bindings apply only until exit.

To update from an earlier release, exit the old copy from the system tray, extract the new package, and start the new executable. Bind your mouse button again.

## Quick start (macOS)

1. Download the **macOS ZIP** from [Releases](https://github.com/liuxing628-beep/SuperCopy/releases/latest), extract **SuperCopy.app**, and move it to Applications if desired. It supports Apple silicon and Intel Macs with macOS 13 or later. The download is ad hoc signed; if macOS blocks a trusted copy on first launch, use the per-app **Open Anyway** option described in [Apple's guide](https://support.apple.com/en-us/102445).
2. In SuperCopy Settings, click **Grant Accessibility…**, enable SuperCopy in **System Settings → Privacy & Security → Accessibility**, then click **Check permissions**. SuperCopy uses this access to send ⌘C and ⌘V to the app you select. If macOS separately asks for Input Monitoring for a mouse binding or permission to read the clipboard after a capture, grant that access if you want the corresponding feature. Keyboard shortcuts do not require a keyboard event tap.
3. Record the capture shortcut first (for example, **⌃⇧C**). Record a different panel shortcut (for example, **⌃⇧V**) or bind a middle, right, or side mouse button. Click **Run in background**.
4. Select text or Excel cells, press and release your capture shortcut, and choose **Add to front**, **Replace all**, or **Cancel**. The choice opens beside the pointer on the source screen when the source window is available to macOS Accessibility.
5. Place the text cursor in the destination app, open the block panel, and left-click a block to paste. Right-click removes it. Drag to an edge or gap to reorder; hold over the middle of another block until it turns green to merge. Drag near the top/bottom of a long panel to scroll.
6. The clipboard icon in the menu bar opens Settings or lets you quit. **View saved blocks** from the menu is copy-only. As on Windows, blocks and bindings are held in memory until exit.

On updates, quit the old copy before replacing it. If macOS no longer recognizes its permissions after replacement, remove the old Accessibility entry and enable the updated app. The app makes no network requests and only reads the clipboard in response to a capture.

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

Each capture and the combined saved set support up to 2,000,000 characters and 50,000 blocks. Adding is checked against both combined limits before any change is made. An empty, failed, or oversized capture leaves the previous set unchanged. Clipboard writes are retried briefly if another app has locked the clipboard.

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

All application labels, notifications, status messages, and instructions are in English. The interface uses the platform fonts and font fallback for captured multilingual text. Your captured content is never translated.

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

## Build on macOS

Install Apple's Command Line Tools or Xcode, then run:

```sh
scripts/build-macos.sh
scripts/test-macos.sh --ui
python3 scripts/package-release.py macos
```

The build creates `dist/SuperCopy-macOS-vX.Y.Z-English.zip` containing a universal, ad hoc signed app. The smoke test opens AppKit windows and tests their click and drag handlers without accessing the system clipboard or sending input to other apps. Copy/paste and mouse shortcuts in an external app need a manual test with Accessibility enabled.

## Release both versions

[Release Windows and macOS](https://github.com/liuxing628-beep/SuperCopy/actions/workflows/release.yml) builds and tests Windows and macOS from the same commit. The Intel runner also unpacks and runs the Mac app. Publication waits for all jobs and both platform archives, and uploads them together with the source archive and SHA-256 checksums.

For a new release, update `VERSION` and both version attributes in `src/AssemblyInfo.cs`, commit the changes to main, then run the release workflow. Alternatively, push the matching `vX.Y.Z` tag. An optional `docs/releases/vX.Y.Z.md` supplies the release notes. The publisher assembles the assets on a draft and makes it public only after the uploads verify.

## Validation

The Windows app can be cross-compiled on macOS to a Windows PE executable. Both test suites run the same block behavior fixtures for natural order, duplicates, merge, and front insertion. Automated checks cover text splitting, space preservation, Unicode, duplicate order, capture limits, preserving old blocks on failure, Win32 input structure layout, adaptive wrapping, narrow screens, hit testing, non-overlapping layouts, 50,000-block sets, append/replace/cancel data behavior, exact removal of duplicates, combined append limits, shortcut validation, drag/click separation, insertion targets, canceled drags, exact movement of duplicate occurrences, and reorder invariants.

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

## Fix in 1.6.1

The Append / Replace all dialog now opens beside the pointer on the capture source screen instead of centering on the main/settings screen. Keyboard capture with the pointer on another monitor stays on the source application screen. Placement is calculated after font/DPI scaling, clamped to the current working area, and supports monitors to the left or above the primary display. On unusually small work areas, the dialog uses scrolling to keep its controls accessible.

Windows acceptance checks: capture from Excel/mail on each monitor; capture near all four screen edges; select by keyboard while leaving the mouse on the other monitor; compare mixed scaling settings; confirm Append, Replace all, Cancel, and restoring focus to the source app.

## Changes in 1.7

- Drag a block over another block's central area and hold for 450 ms to prepare a merge, similar to grouping icons on a phone. Amber / Hold to merge means wait; green / Release to merge means ready.
- Release on the ready target to form one plain-text block: **target text + CRLF + dragged text**. The source occurrence is removed and the merged text remains at the target's position. This joins text directly, rather than creating a folder of separate items.
- Drop near a tile's edge or in a gap to keep reordering with the blue insertion line. Releasing in an amber center cancels the drop, reducing accidental merges during sorting.
- Merged blocks retain their line breaks when pasted or when more captures are appended. They can be reordered, removed, and merged again. The combined character limit includes the new line break; exceeding it leaves all blocks unchanged.
- Click still pastes. Outside drops and canceled drags never paste or merge. The 1.6.1 source-monitor confirmation fix remains included.

Windows acceptance checks for merging: sort by edges; move briefly over centers; wait for green and merge in both directions; repeat with duplicate and already merged blocks; scroll while dragging; drop outside and cancel; paste the combined block into a multiline input; reopen the panel and append content to verify the merged block remains intact. In Excel, a merged block contains line breaks, so paste behavior depends on whether you are editing a cell or pasting into the worksheet.

## Fix in 1.7.1

- The capture dialog now uses **Add to front**. Keeping the existing set places the newest capture before all older blocks.
- Natural reading order within a capture is unchanged: tabs and paragraph/line breaks still produce blocks from first to last. A new batch is never reversed.
- Example: saved `A, B` plus new `C, D` becomes `C, D, A, B`. Adding `E, F` next produces `E, F, C, D, A, B`.
- Older custom order, merged blocks and their internal line breaks remain intact behind the new batch. Replace all, Cancel, splitting rules, and combined size checks keep their existing behavior.
- Earlier changelog references to appending at the end describe historical releases; 1.7.1 uses front insertion.

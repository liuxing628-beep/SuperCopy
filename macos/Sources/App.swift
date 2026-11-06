import AppKit
import ApplicationServices
import Carbon.HIToolbox

final class ShortcutRecorder: NSButton {
    var shortcut: Shortcut? { didSet { endRecording() } }
    var accept: ((Shortcut) -> Bool)?
    var invalid: (() -> Void)?
    private var recording = false
    override var acceptsFirstResponder: Bool { true }

    init() {
        super.init(frame: .zero)
        bezelStyle = .rounded
        title = "Record shortcut…"
        target = self; action = #selector(startRecording)
    }
    required init?(coder: NSCoder) { nil }
    @objc private func startRecording() {
        window?.makeFirstResponder(self)
        recording = true
        title = "Press a shortcut…"
    }
    func endRecording() {
        recording = false
        title = shortcut?.label ?? "Record shortcut…"
    }
    override func resignFirstResponder() -> Bool { endRecording(); return super.resignFirstResponder() }
    override func keyDown(with event: NSEvent) {
        if !handle(event) { super.keyDown(with: event) }
    }
    override func performKeyEquivalent(with event: NSEvent) -> Bool {
        handle(event) || super.performKeyEquivalent(with: event)
    }
    private func handle(_ event: NSEvent) -> Bool {
        guard recording else { return false }
        if event.keyCode == UInt16(kVK_Escape) { endRecording(); return true }
        if !event.isARepeat {
            if let shortcut = Shortcut(event: event) {
                if accept?(shortcut) == true { self.shortcut = shortcut }
            } else { invalid?() }
        }
        return true
    }
}

final class AppController: NSObject, NSApplicationDelegate {
    private var store = BlockStore()
    private let hotkeys = Hotkeys()
    private let mouseHook = MouseHook()
    private var mouseHookAvailable = false
    private var nextID: UInt32 = 10
    private var captureBinding: (shortcut: Shortcut, id: UInt32)?
    private var panelBinding: (shortcut: Shortcut, id: UInt32)?
    private var mouseButton: Int?
    private var learnUntil: TimeInterval?
    private var activeHotkeys: Set<UInt32> = []
    private var menuItem: NSStatusItem?
    private var menuStatus: NSMenuItem?
    private var settings: NSWindow?
    private let captureRecorder = ShortcutRecorder()
    private let panelRecorder = ShortcutRecorder()
    private let mouseControl = NSButton(title: "Or bind a mouse button", target: nil, action: nil)
    private let hint = NSTextField(wrappingLabelWithString: "")
    private let access = NSTextField(wrappingLabelWithString: "")
    private let permissions = NSButton(title: "Grant Accessibility…", target: nil, action: nil)
    private let background = NSButton(title: "Run in background", target: nil, action: nil)
    private var popup: BlocksPanel?
    private var popupTarget: InputTarget?
    private var dialog: CaptureDialog?
    private var dialogTarget: InputTarget?
    private var toast: PassivePanel?
    private var busy = false
    private var localMouse: Any?
    private var globalMouse: Any?
    private var activationObserver: NSObjectProtocol?

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        item.button?.image = NSImage(systemSymbolName: "doc.on.clipboard", accessibilityDescription: "SuperCopy")
        if item.button?.image == nil { item.button?.title = "SuperCopy" }
        let menu = NSMenu()
        let status = NSMenuItem(title: "Set up shortcuts to start", action: nil, keyEquivalent: "")
        menu.addItem(status); menuStatus = status
        menu.addItem(.separator())
        for (title, selector, key) in [
            ("Settings…", #selector(showSettings), ","),
            ("View saved blocks", #selector(viewBlocks), ""),
            ("Quit SuperCopy", #selector(quit), "q")
        ] {
            let action = NSMenuItem(title: title, action: selector, keyEquivalent: key)
            action.target = self; menu.addItem(action)
        }
        item.menu = menu; menuItem = item
        hotkeys.triggered = { [weak self] id in self?.hotkey(id) }
        mouseHook.pressed = { [weak self] button, location in
            self?.handleMouse(button, at: ScreenGeometry.fromQuartz(location)) ?? false
        }
        let clicks: NSEvent.EventTypeMask = [.leftMouseDown, .rightMouseDown, .otherMouseDown]
        localMouse = NSEvent.addLocalMonitorForEvents(matching: clicks) { [weak self] event in
            self?.dismissOutside(NSEvent.mouseLocation)
            return event
        }
        globalMouse = NSEvent.addGlobalMonitorForEvents(matching: clicks) { [weak self] _ in
            guard self?.mouseHookAvailable == false else { return }
            self?.dismissOutside(NSEvent.mouseLocation)
        }
        activationObserver = NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.didActivateApplicationNotification, object: nil, queue: .main
        ) { [weak self] _ in
            guard let self = self else { return }
            if let target = self.popupTarget, NSWorkspace.shared.frontmostApplication?.processIdentifier != target.pid { self.closePopup() }
            if let target = self.dialogTarget, NSWorkspace.shared.frontmostApplication?.processIdentifier != target.pid { self.closeDialog() }
        }
        showSettings()
    }

    func applicationDidBecomeActive(_ notification: Notification) { refreshPermissions() }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool { showSettings(); return true }
    func applicationWillTerminate(_ notification: Notification) {
        if let localMouse = localMouse { NSEvent.removeMonitor(localMouse) }
        if let globalMouse = globalMouse { NSEvent.removeMonitor(globalMouse) }
        if let observer = activationObserver { NSWorkspace.shared.notificationCenter.removeObserver(observer) }
        mouseHook.stop()
    }

    @objc private func quit() { NSApp.terminate(nil) }
    @objc private func viewBlocks() { togglePopup(at: NSEvent.mouseLocation, target: nil) }
    @objc private func showSettings() {
        closePopup(); closeDialog()
        if settings == nil { buildSettings() }
        refreshPermissions()
        if #available(macOS 14, *) { NSApp.activate() }
        else { NSApp.activate(ignoringOtherApps: true) }
        settings?.makeKeyAndOrderFront(nil)
    }
    private func buildSettings() {
        let width: CGFloat = 550, height: CGFloat = 448
        let window = NSWindow(contentRect: CGRect(x: 0, y: 0, width: width, height: height),
                              styleMask: [.titled, .closable, .miniaturizable], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        window.title = "SuperCopy · Settings"
        window.center()
        let root = NSView(frame: CGRect(x: 0, y: 0, width: width, height: height))
        window.contentView = root
        func label(_ value: String, y: CGFloat, height: CGFloat, size: CGFloat = 13, bold: Bool = false) {
            let label = NSTextField(wrappingLabelWithString: value)
            label.font = .systemFont(ofSize: size, weight: bold ? .semibold : .regular)
            label.frame = CGRect(x: 22, y: y, width: width - 44, height: height)
            root.addSubview(label)
        }
        func place(_ button: NSButton, x: CGFloat, y: CGFloat, width: CGFloat) {
            button.bezelStyle = .rounded
            button.frame = CGRect(x: x, y: y, width: width, height: 30)
            root.addSubview(button)
        }
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? ""
        label("SuperCopy " + version, y: 393, height: 36, size: 24, bold: true)
        label("Step 1 — Capture the selection", y: 356, height: 20, bold: true)
        place(captureRecorder, x: 20, y: 318, width: 202)
        label("Step 2 — Open the block panel", y: 280, height: 20, bold: true)
        place(panelRecorder, x: 20, y: 242, width: 202)
        place(mouseControl, x: 230, y: 242, width: 230)
        mouseControl.target = self; mouseControl.action = #selector(learnMouse)
        label("Use Command, Control or Option with a key. To paste a block, place the caret in the target app, open the panel, then click. Drag to reorder or merge; right-click to remove.", y: 164, height: 62)
        access.frame = CGRect(x: 22, y: 118, width: 506, height: 39)
        access.font = .systemFont(ofSize: 12)
        root.addSubview(access)
        permissions.target = self; permissions.action = #selector(grantAccessibility)
        place(permissions, x: 20, y: 81, width: 192)
        place(NSButton(title: "Check permissions", target: self, action: #selector(checkPermissions)), x: 218, y: 81, width: 155)
        background.target = self; background.action = #selector(hideSettings)
        place(background, x: 380, y: 81, width: 152)
        hint.frame = CGRect(x: 22, y: 14, width: 506, height: 55)
        hint.font = .systemFont(ofSize: 12)
        hint.textColor = .secondaryLabelColor
        hint.stringValue = "Choose a capture shortcut first (for example, ⌃⇧C), then choose how to open the panel."
        root.addSubview(hint)
        captureRecorder.accept = { [weak self] in self?.assign($0, capture: true) ?? false }
        panelRecorder.accept = { [weak self] in self?.assign($0, capture: false) ?? false }
        for recorder in [captureRecorder, panelRecorder] {
            recorder.invalid = { [weak self] in self?.report("Use Command, Control or Option with a key. Standard ⌘C, ⌘V and system shortcuts are reserved.", toast: false) }
        }
        settings = window
        syncSettings()
    }
    @objc private func hideSettings() {
        learnUntil = nil
        captureRecorder.endRecording(); panelRecorder.endRecording()
        settings?.orderOut(nil)
    }
    @objc private func grantAccessibility() {
        let pane: String
        if AXIsProcessTrusted() && !mouseHookAvailable {
            _ = CGRequestListenEventAccess()
            pane = "Privacy_ListenEvent"
        } else {
            NativeInput.requestAccessibility()
            pane = "Privacy_Accessibility"
        }
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?\(pane)") {
            NSWorkspace.shared.open(url)
        }
    }
    @objc private func checkPermissions() { refreshPermissions() }
    private func refreshPermissions() {
        let trusted = AXIsProcessTrusted()
        if trusted { mouseHookAvailable = mouseHook.start() }
        else { mouseHook.stop(); mouseHookAvailable = false }
        access.stringValue = trusted
            ? (mouseHookAvailable ? "Accessibility granted. Keyboard and mouse bindings are available." : "Accessibility granted. Mouse monitoring is unavailable; keyboard shortcuts still work.")
            : "Accessibility is required for automatic copy and paste. Grant access, then check permissions."
        permissions.title = trusted && !mouseHookAvailable ? "Open Input Monitoring…" : "Grant Accessibility…"
        access.textColor = trusted ? .secondaryLabelColor : .systemOrange
        syncSettings()
    }
    private func syncSettings() {
        panelRecorder.isEnabled = captureBinding != nil
        mouseControl.isEnabled = captureBinding != nil
        mouseControl.title = mouseButton.map { "Mouse button \($0 + 1) · Change…" } ?? "Or bind a mouse button"
        background.isEnabled = captureBinding != nil && (panelBinding != nil || mouseButton != nil)
    }
    private func assign(_ shortcut: Shortcut, capture: Bool) -> Bool {
        learnUntil = nil
        let old = capture ? captureBinding : panelBinding
        let other = capture ? panelBinding : captureBinding
        guard other?.shortcut != shortcut else { report("Use a different shortcut for capture and panel.", toast: false); return false }
        if old?.shortcut == shortcut { return true }
        let id = nextID
        nextID += 1
        guard hotkeys.register(id: id, key: shortcut.keyCode, modifiers: shortcut.modifiers) else {
            report("That shortcut is in use or reserved by macOS. Try another combination.", toast: false)
            return false
        }
        if let old = old { hotkeys.remove(old.id) }
        if capture { captureBinding = (shortcut, id) }
        else { panelBinding = (shortcut, id); mouseButton = nil }
        syncSettings()
        report(capture ? "Capture shortcut set. Now choose how to open the panel." : "Shortcuts ready. Click Run in background to start.", toast: false)
        return true
    }
    @objc private func learnMouse() {
        refreshPermissions()
        guard mouseHookAvailable else {
            report("Grant Accessibility and check permissions before binding a mouse button.", toast: false)
            return
        }
        captureRecorder.endRecording(); panelRecorder.endRecording()
        let deadline = ProcessInfo.processInfo.systemUptime + 30
        learnUntil = deadline
        report("Press a middle, right, or side mouse button within 30 seconds. Left-click remains available.", toast: false)
        DispatchQueue.main.asyncAfter(deadline: .now() + 30) { [weak self] in
            guard let self = self, self.learnUntil == deadline else { return }
            self.learnUntil = nil; self.report("Mouse binding timed out. Click the binding button to retry.", toast: false)
        }
    }
    private func handleMouse(_ button: Int, at point: CGPoint) -> Bool {
        if let deadline = learnUntil, ProcessInfo.processInfo.systemUptime < deadline, (1...4).contains(button) {
            learnUntil = nil
            DispatchQueue.main.async { [weak self] in
                guard let self = self else { return }
                if let binding = self.panelBinding { self.hotkeys.remove(binding.id) }
                self.panelBinding = nil; self.panelRecorder.shortcut = nil
                self.mouseButton = button; self.syncSettings()
                self.report("Mouse button set. Click Run in background to start.", toast: false)
            }
            return true
        }
        if button == mouseButton && dialog == nil {
            // The same right button must still work for tile removal inside the panel.
            if button == 1 && popup?.panel.frame.contains(point) == true { return false }
            DispatchQueue.main.async { [weak self] in self?.togglePopup(at: point, target: InputTarget.current()) }
            return true
        }
        DispatchQueue.main.async { [weak self] in self?.dismissOutside(point) }
        return false
    }
    private func dismissOutside(_ point: CGPoint) {
        if let popup = popup, !popup.panel.frame.contains(point) { closePopup() }
        if let dialog = dialog, !dialog.panel.frame.contains(point) { closeDialog() }
    }
    private func hotkey(_ id: UInt32) {
        if id == 1 { closePopup(); closeDialog(); learnUntil = nil; return }
        if id == 2 {
            dialog?.selected?(.add)
            return
        }
        if id == captureBinding?.id {
            guard let binding = captureBinding, (panelBinding != nil || mouseButton != nil), !busy, dialog == nil, popup == nil else { return }
            guard !activeHotkeys.contains(id) else { return }
            activeHotkeys.insert(id); releaseHotkey(id, key: binding.shortcut.keyCode)
            capture(key: binding.shortcut.keyCode)
        } else if id == panelBinding?.id {
            guard let binding = panelBinding, !activeHotkeys.contains(id) else { return }
            activeHotkeys.insert(id); releaseHotkey(id, key: binding.shortcut.keyCode)
            if let target = InputTarget.current() { togglePopup(at: NSEvent.mouseLocation, target: target) }
        }
    }
    private func releaseHotkey(_ id: UInt32, key: UInt32) {
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.06) { [weak self] in
            guard let self = self else { return }
            if CGEventSource.keyState(.combinedSessionState, key: CGKeyCode(key)) { self.releaseHotkey(id, key: key) }
            else { self.activeHotkeys.remove(id) }
        }
    }
    private func capture(key: UInt32) {
        guard AXIsProcessTrusted() else { report("Grant Accessibility in SuperCopy Settings before capturing."); return }
        guard let target = InputTarget.current() else { report("Select some text in another app first.", toast: false); return }
        let (anchor, area) = ScreenGeometry.captureLocation(target: target, pointer: NSEvent.mouseLocation)
        busy = true
        poll(until: ProcessInfo.processInfo.systemUptime + 2) { [weak self] expired in
            guard let self = self else { return true }
            guard NSWorkspace.shared.frontmostApplication?.processIdentifier == target.pid else {
                self.busy = false; self.report("Capture canceled: the source app changed."); return true
            }
            if !NativeInput.modifiersReleased() || CGEventSource.keyState(.combinedSessionState, key: CGKeyCode(key)) {
                if expired { self.busy = false; self.report("Release the capture shortcut and try again.") }
                return expired
            }
            guard target.stillFocused() else {
                self.busy = false; self.report("Capture canceled: the source window changed."); return true
            }
            let before = NSPasteboard.general.changeCount
            guard NativeInput.command(kVK_ANSI_C, target: target.pid) else {
                self.busy = false; self.report("Copy could not be sent. Check Accessibility permissions."); return true
            }
            self.awaitCopy(after: before, target: target, anchor: anchor, area: area)
            return true
        }
    }
    private func awaitCopy(after before: Int, target: InputTarget, anchor: CGPoint, area: CGRect) {
        poll(until: ProcessInfo.processInfo.systemUptime + 4) { [weak self] expired in
            guard let self = self else { return true }
            guard NSWorkspace.shared.frontmostApplication?.processIdentifier == target.pid else {
                self.busy = false; self.report("Capture canceled: the source app changed."); return true
            }
            let board = NSPasteboard.general
            if board.changeCount != before, let text = board.string(forType: .string) {
                self.busy = false
                guard target.stillFocused() else { self.report("Capture canceled: the source window changed."); return true }
                do { self.choose(try BlockStore.parse(text), target: target, anchor: anchor, area: area) }
                catch { self.report(error.localizedDescription) }
                return true
            }
            if expired {
                self.busy = false
                self.report("No text arrived on the clipboard. Select text that supports ⌘C and allow clipboard access if macOS asks.")
            }
            return expired
        }
    }
    private func choose(_ incoming: [String], target: InputTarget, anchor: CGPoint, area: CGRect) {
        let dialog = CaptureDialog(incoming: incoming.count, existing: store.items.count, anchor: anchor, area: area)
        self.dialog = dialog; dialogTarget = target
        _ = hotkeys.register(id: 1, key: UInt32(kVK_Escape), modifiers: 0)
        _ = hotkeys.register(id: 2, key: UInt32(kVK_Return), modifiers: 0)
        dialog.selected = { [weak self] choice in
            guard let self = self else { return }
            self.closeDialog()
            if choice == .cancel { return }
            do {
                try self.store.commit(incoming, keepingExisting: choice == .add)
                self.report("\(self.store.items.count) saved blocks · Open the panel to paste.", toast: false)
            } catch { self.report(error.localizedDescription) }
        }
        dialog.panel.orderFrontRegardless()
    }
    private func closeDialog() {
        guard let dialog = dialog else { return }
        self.dialog = nil; dialogTarget = nil
        hotkeys.remove(1); hotkeys.remove(2)
        dialog.panel.orderOut(nil)
    }
    private func togglePopup(at point: CGPoint, target: InputTarget?) {
        guard !busy, dialog == nil else { return }
        if popup != nil { closePopup(); return }
        guard !store.items.isEmpty else { report("No blocks yet. Select text and use your capture shortcut."); return }
        toast?.orderOut(nil); toast = nil
        let popup = BlocksPanel(items: store.items, anchor: point, area: ScreenGeometry.area(near: point))
        self.popup = popup; popupTarget = target
        popup.action = { [weak self] action in self?.handleBlock(action) }
        _ = hotkeys.register(id: 1, key: UInt32(kVK_Escape), modifiers: 0)
        popup.panel.orderFrontRegardless()
    }
    private func closePopup() {
        guard let popup = popup else { return }
        self.popup = nil; popupTarget = nil
        hotkeys.remove(1); popup.close()
    }
    private func handleBlock(_ action: BlockCanvas.Action) {
        switch action {
        case .paste(let index):
            guard store.items.indices.contains(index) else { return }
            let target = popupTarget
            let text = store.items[index]
            closePopup()
            paste(text, target: target)
        case .remove(let index): store.remove(index); updateBlocks()
        case .move(let source, let boundary): store.move(source, toBoundary: boundary); updateBlocks()
        case .merge(let source, let target):
            do { try store.merge(source, into: target); updateBlocks() }
            catch { report(error.localizedDescription) }
        }
    }
    private func updateBlocks() {
        if store.items.isEmpty { closePopup() }
        else { popup?.update(store.items) }
        report("\(store.items.count) saved blocks", toast: false)
    }
    private func paste(_ text: String, target: InputTarget?) {
        if target == nil {
            let board = NSPasteboard.general
            board.clearContents()
            report(board.setString(text, forType: .string) ? "Block copied. Paste it with ⌘V." : "Could not write the clipboard.")
            return
        }
        guard let target = target, AXIsProcessTrusted() else { report("Grant Accessibility in SuperCopy Settings to paste."); return }
        busy = true
        poll(until: ProcessInfo.processInfo.systemUptime + 2) { [weak self] expired in
            guard let self = self else { return true }
            guard NSWorkspace.shared.frontmostApplication?.processIdentifier == target.pid else {
                self.busy = false; self.report("Paste canceled: the target app changed."); return true
            }
            if NSEvent.pressedMouseButtons != 0 || !NativeInput.modifiersReleased() {
                if expired { self.busy = false; self.report("Release the mouse button and modifiers, then try again.") }
                return expired
            }
            self.busy = false
            guard target.stillFocused(checkElement: true) else { self.report("Paste canceled: the target field changed."); return true }
            let board = NSPasteboard.general
            board.clearContents()
            guard board.setString(text, forType: .string) else { self.report("Could not write the clipboard."); return true }
            let written = board.changeCount
            self.busy = true
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.055) { [weak self] in
                guard let self = self else { return }
                self.busy = false
                guard NSPasteboard.general.changeCount == written, target.stillFocused(checkElement: true),
                      NativeInput.modifiersReleased(), NSEvent.pressedMouseButtons == 0 else {
                    self.report("Automatic paste canceled because the clipboard, focus or input changed."); return
                }
                if !NativeInput.command(kVK_ANSI_V, target: target.pid) { self.report("Automatic paste unavailable. The text is on the clipboard.") }
            }
            return true
        }
    }
    private func poll(until deadline: TimeInterval, _ condition: @escaping (Bool) -> Bool) {
        let expired = ProcessInfo.processInfo.systemUptime >= deadline
        if condition(expired) || expired { return }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.045) { [weak self] in self?.poll(until: deadline, condition) }
    }

    private func report(_ text: String, toast show: Bool = true) {
        hint.stringValue = text
        menuStatus?.title = String(text.prefix(80))
        guard show, settings?.isKeyWindow != true else { return }
        toast?.orderOut(nil)
        let point = NSEvent.mouseLocation
        let frame = Placement.near(point, size: PassivePanel.frameSize(for: CGSize(width: 348, height: 68)),
                                   within: ScreenGeometry.area(near: point))
        let toast = PassivePanel(title: "SuperCopy", frame: frame)
        let root = NSView(frame: CGRect(origin: .zero, size: toast.contentRect(forFrameRect: frame).size))
        let label = NSTextField(wrappingLabelWithString: text)
        label.font = .systemFont(ofSize: 12)
        label.frame = root.bounds.insetBy(dx: 11, dy: 8)
        root.addSubview(label); toast.contentView = root
        toast.ignoresMouseEvents = true
        self.toast = toast
        toast.orderFrontRegardless()
        DispatchQueue.main.asyncAfter(deadline: .now() + 2.6) { [weak self, weak toast] in
            if let toast = toast, self?.toast === toast { toast.orderOut(nil); self?.toast = nil }
        }
    }
}

@main
enum SuperCopyApp {
    static func main() {
        let app = NSApplication.shared
        if CommandLine.arguments.contains("--ui-smoke-test") {
            exit(AppSmoke.run())
        }
        let delegate = AppController()
        app.delegate = delegate
        withExtendedLifetime(delegate) { app.run() }
    }
}

import AppKit
import ApplicationServices
import Carbon.HIToolbox

struct Shortcut: Equatable {
    let keyCode: UInt32
    let modifiers: UInt32
    let label: String

    static func == (lhs: Shortcut, rhs: Shortcut) -> Bool {
        lhs.keyCode == rhs.keyCode && lhs.modifiers == rhs.modifiers
    }
    init?(event: NSEvent) {
        let flags = event.modifierFlags
        guard flags.contains(.command) || flags.contains(.control) || flags.contains(.option) else { return nil }
        var mods: UInt32 = 0
        var name = ""
        for (flag, mask, symbol): (NSEvent.ModifierFlags, Int, String) in [
            (.control, controlKey, "⌃"), (.option, optionKey, "⌥"),
            (.shift, shiftKey, "⇧"), (.command, cmdKey, "⌘")
        ] where flags.contains(flag) {
            mods |= UInt32(mask); name += symbol
        }
        let code = UInt32(event.keyCode)
        guard code != UInt32(kVK_Escape) else { return nil }
        if mods == UInt32(cmdKey) && [kVK_ANSI_C, kVK_ANSI_V, kVK_ANSI_Q, kVK_Tab].contains(Int(code)) { return nil }
        let special: [Int: String] = [kVK_Space: "Space", kVK_Tab: "Tab", kVK_Return: "Return",
            kVK_LeftArrow: "←", kVK_RightArrow: "→", kVK_UpArrow: "↑", kVK_DownArrow: "↓",
            kVK_Delete: "⌫", kVK_F1: "F1", kVK_F2: "F2", kVK_F3: "F3", kVK_F4: "F4",
            kVK_F5: "F5", kVK_F6: "F6", kVK_F7: "F7", kVK_F8: "F8", kVK_F9: "F9",
            kVK_F10: "F10", kVK_F11: "F11", kVK_F12: "F12"]
        let character = special[Int(code)] ?? event.charactersIgnoringModifiers?.uppercased() ?? ""
        guard !character.isEmpty, !character.unicodeScalars.contains(where: { $0.value < 32 || (0xE000...0xF8FF).contains($0.value) }) else { return nil }
        keyCode = code; modifiers = mods; label = name + character
    }
}

final class Hotkeys {
    private static let signature: OSType = 0x53435059 // SCPY
    private var handler: EventHandlerRef?
    private var bindings: [UInt32: EventHotKeyRef] = [:]
    var triggered: ((UInt32) -> Void)?
    init() {
        var spec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let status = InstallEventHandler(GetApplicationEventTarget(), { _, event, context in
            guard let event = event, let context = context else { return OSStatus(eventNotHandledErr) }
            var identifier = EventHotKeyID()
            let result = GetEventParameter(event, EventParamName(kEventParamDirectObject), EventParamType(typeEventHotKeyID),
                                           nil, MemoryLayout<EventHotKeyID>.size, nil, &identifier)
            guard result == noErr, identifier.signature == Hotkeys.signature else { return OSStatus(eventNotHandledErr) }
            let owner = Unmanaged<Hotkeys>.fromOpaque(context).takeUnretainedValue()
            let id = identifier.id
            DispatchQueue.main.async { [weak owner] in owner?.triggered?(id) }
            return noErr
        }, 1, &spec, Unmanaged.passUnretained(self).toOpaque(), &handler)
        if status != noErr { handler = nil }
    }
    func register(id: UInt32, key: UInt32, modifiers: UInt32) -> Bool {
        guard handler != nil, bindings[id] == nil else { return false }
        var reference: EventHotKeyRef?
        let identifier = EventHotKeyID(signature: Self.signature, id: id)
        let result = RegisterEventHotKey(key, modifiers, identifier, GetApplicationEventTarget(), 0, &reference)
        guard result == noErr, let reference = reference else { return false }
        bindings[id] = reference
        return true
    }
    func remove(_ id: UInt32) {
        if let reference = bindings.removeValue(forKey: id) { UnregisterEventHotKey(reference) }
    }
    deinit {
        for reference in bindings.values { UnregisterEventHotKey(reference) }
        if let handler = handler { RemoveEventHandler(handler) }
    }
}

final class MouseHook {
    private var tap: CFMachPort?
    private var source: CFRunLoopSource?
    private var swallowed: Set<Int> = []
    // Only mouse button events are observed. Keyboard shortcuts use Carbon.
    var pressed: ((Int, CGPoint) -> Bool)?

    func start() -> Bool {
        if let tap = tap, CFMachPortIsValid(tap) { return true }
        stop()
        let types: [CGEventType] = [.leftMouseDown, .rightMouseDown, .rightMouseUp,
                                    .rightMouseDragged, .otherMouseDown, .otherMouseUp, .otherMouseDragged]
        let mask = types.reduce(CGEventMask(0)) { $0 | (CGEventMask(1) << $1.rawValue) }
        guard let tap = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap, options: .defaultTap,
            eventsOfInterest: mask, callback: { _, type, event, context in
                guard let context = context else { return Unmanaged.passUnretained(event) }
                let hook = Unmanaged<MouseHook>.fromOpaque(context).takeUnretainedValue()
                if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
                    hook.swallowed.removeAll()
                    if let tap = hook.tap { CGEvent.tapEnable(tap: tap, enable: true) }
                    return Unmanaged.passUnretained(event)
                }
                let button = Int(event.getIntegerValueField(.mouseEventButtonNumber))
                if type == .rightMouseUp || type == .otherMouseUp {
                    if hook.swallowed.remove(button) != nil { return nil }
                } else if type == .rightMouseDragged || type == .otherMouseDragged {
                    if hook.swallowed.contains(button) { return nil }
                } else if hook.pressed?(button, event.location) == true {
                    hook.swallowed.insert(button)
                    return nil
                }
                return Unmanaged.passUnretained(event)
            }, userInfo: Unmanaged.passUnretained(self).toOpaque()) else { return false }
        self.tap = tap
        source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        guard let source = source else { stop(); return false }
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        return true
    }
    func stop() {
        if let source = source { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        if let tap = tap { CFMachPortInvalidate(tap) }
        source = nil; tap = nil; swallowed.removeAll()
    }
    deinit { stop() }
}

struct InputTarget {
    let pid: pid_t
    let window: AXUIElement?
    let focused: AXUIElement?

    static func current() -> InputTarget? {
        guard let app = NSWorkspace.shared.frontmostApplication, app.processIdentifier != ProcessInfo.processInfo.processIdentifier else { return nil }
        let element = AXUIElementCreateApplication(app.processIdentifier)
        AXUIElementSetMessagingTimeout(element, 0.25)
        return InputTarget(pid: app.processIdentifier, window: axElement(element, kAXFocusedWindowAttribute),
                           focused: axElement(element, kAXFocusedUIElementAttribute))
    }
    func stillFocused(checkElement: Bool = false) -> Bool {
        guard NSWorkspace.shared.frontmostApplication?.processIdentifier == pid else { return false }
        if window == nil && (!checkElement || focused == nil) { return true }
        let element = AXUIElementCreateApplication(pid)
        AXUIElementSetMessagingTimeout(element, 0.25)
        if let window = window {
            guard let current = axElement(element, kAXFocusedWindowAttribute), CFEqual(window, current) else { return false }
        }
        if checkElement, let focused = focused {
            guard let current = axElement(element, kAXFocusedUIElementAttribute), CFEqual(focused, current) else { return false }
        }
        return true
    }
    func screenFrame() -> CGRect? {
        guard let window = window,
              let p = axValue(window, kAXPositionAttribute, .cgPoint),
              let s = axValue(window, kAXSizeAttribute, .cgSize) else { return nil }
        var position = CGPoint.zero, size = CGSize.zero
        guard AXValueGetValue(p, .cgPoint, &position), AXValueGetValue(s, .cgSize, &size), size.width > 0, size.height > 0 else { return nil }
        return CGRect(x: position.x, y: ScreenGeometry.primaryTop - position.y - size.height, width: size.width, height: size.height)
    }
}

private func axElement(_ element: AXUIElement, _ name: String) -> AXUIElement? {
    var raw: CFTypeRef?
    guard AXUIElementCopyAttributeValue(element, name as CFString, &raw) == .success,
          let raw = raw, CFGetTypeID(raw) == AXUIElementGetTypeID() else { return nil }
    return unsafeBitCast(raw, to: AXUIElement.self)
}
private func axValue(_ element: AXUIElement, _ name: String, _ type: AXValueType) -> AXValue? {
    var raw: CFTypeRef?
    guard AXUIElementCopyAttributeValue(element, name as CFString, &raw) == .success,
          let raw = raw, CFGetTypeID(raw) == AXValueGetTypeID() else { return nil }
    let value = unsafeBitCast(raw, to: AXValue.self)
    return AXValueGetType(value) == type ? value : nil
}

enum ScreenGeometry {
    static var primaryTop: CGFloat { NSScreen.screens.first?.frame.maxY ?? 0 }
    static func fromQuartz(_ p: CGPoint) -> CGPoint { CGPoint(x: p.x, y: primaryTop - p.y) }
    static func area(near point: CGPoint) -> CGRect {
        if let screen = NSScreen.screens.first(where: { $0.frame.contains(point) }) { return screen.visibleFrame }
        return NSScreen.main?.visibleFrame ?? NSScreen.screens.first?.visibleFrame ?? CGRect(x: 0, y: 0, width: 800, height: 600)
    }
    static func captureLocation(target: InputTarget, pointer: CGPoint) -> (CGPoint, CGRect) {
        guard let frame = target.screenFrame() else { return (pointer, area(near: pointer)) }
        if frame.contains(pointer) { return (pointer, area(near: pointer)) }
        let screens = NSScreen.screens
        let screen = screens.max { a, b in
            let ai = a.frame.intersection(frame), bi = b.frame.intersection(frame)
            return (ai.isNull ? 0 : ai.width * ai.height) < (bi.isNull ? 0 : bi.width * bi.height)
        }
        guard let screen = screen, screen.frame.intersects(frame) else { return (pointer, area(near: pointer)) }
        let area = screen.visibleFrame
        return (screen.frame.contains(pointer) ? pointer : CGPoint(x: area.midX, y: area.midY), area)
    }
}

enum NativeInput {
    static func requestAccessibility() {
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
    }
    static func modifiersReleased() -> Bool {
        CGEventSource.flagsState(.combinedSessionState).intersection([.maskCommand, .maskControl, .maskAlternate, .maskShift]).isEmpty
    }
    static func command(_ key: Int, target: pid_t) -> Bool {
        guard AXIsProcessTrusted(),
              let down = CGEvent(keyboardEventSource: nil, virtualKey: CGKeyCode(key), keyDown: true),
              let up = CGEvent(keyboardEventSource: nil, virtualKey: CGKeyCode(key), keyDown: false) else { return false }
        down.flags = .maskCommand; up.flags = .maskCommand
        down.postToPid(target); up.postToPid(target)
        return true
    }
}

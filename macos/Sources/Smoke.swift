import AppKit

enum AppSmoke {
    static func run() -> Int32 {
        // Exercise the real AppKit windows and input routing without global input,
        // clipboard access, permission changes, or an external target application.
        let app = NSApplication.shared
        app.setActivationPolicy(.accessory)
        app.finishLaunching()
        let area = NSScreen.screens.first?.visibleFrame ?? CGRect(x: 0, y: 0, width: 900, height: 700)
        let anchor = CGPoint(x: area.midX - 180, y: area.midY + 100)
        let items = ["John Smith", "上海", "A longer block with spaces", "One\r\nTwo", "Duplicate", "Duplicate"]
        let front = NSWorkspace.shared.frontmostApplication?.processIdentifier
        let popup = BlocksPanel(items: items, anchor: anchor, area: area)
        popup.panel.orderFrontRegardless()
        popup.panel.displayIfNeeded()
        let canvas = popup.canvas
        guard popup.panel.isVisible, !popup.panel.canBecomeKey, area.contains(popup.panel.frame),
              canvas.layout.tiles.count == items.count, canvas.visibleRect.width > 0 else {
            fputs("Non-activating block panel failed to lay out.\n", stderr); return 1
        }
        let origin = canvas.layout.tiles[0]
        let p = canvas.convert(CGPoint(x: origin.midX + 9, y: origin.midY + 9), to: nil)
        var clicked: Int?
        var removed: Int?
        var moved: (Int, Int)?
        var merged: (Int, Int)?
        popup.action = { action in
            switch action {
            case .paste(let index): clicked = index
            case .remove(let index): removed = index
            case .move(let source, let boundary): moved = (source, boundary)
            case .merge(let source, let target): merged = (source, target)
            }
        }
        func event(_ type: NSEvent.EventType, _ point: CGPoint) -> NSEvent {
            NSEvent.mouseEvent(with: type, location: point, modifierFlags: [], timestamp: 0,
                               windowNumber: popup.panel.windowNumber, context: nil, eventNumber: 1, clickCount: 1, pressure: 1)!
        }
        canvas.mouseDown(with: event(.leftMouseDown, p))
        canvas.mouseUp(with: event(.leftMouseUp, p))
        canvas.rightMouseDown(with: event(.rightMouseDown, p))
        canvas.rightMouseUp(with: event(.rightMouseUp, p))
        let second = canvas.layout.tiles[1]
        let edge = canvas.convert(CGPoint(x: second.maxX + 7, y: second.midY + 9), to: nil)
        canvas.mouseDown(with: event(.leftMouseDown, p))
        canvas.mouseDragged(with: event(.leftMouseDragged, edge))
        canvas.mouseUp(with: event(.leftMouseUp, edge))
        let center = canvas.convert(CGPoint(x: second.midX + 9, y: second.midY + 9), to: nil)
        canvas.mouseDown(with: event(.leftMouseDown, p))
        canvas.mouseDragged(with: event(.leftMouseDragged, center))
        Thread.sleep(forTimeInterval: DragSession.dwell + 0.03)
        canvas.mouseUp(with: event(.leftMouseUp, center))
        let activeApp = NSWorkspace.shared.frontmostApplication?.processIdentifier
        popup.close()
        let choice = CaptureDialog(incoming: 6, existing: 4, anchor: anchor, area: area)
        var added = false
        choice.selected = { added = $0 == .add }
        choice.panel.orderFrontRegardless()
        (choice.panel.contentView?.subviews.compactMap { $0 as? NSButton }.first { $0.title == "Add to front" })?.performClick(nil)
        choice.panel.orderOut(nil)
        guard clicked == 0, removed == 0, moved?.0 == 0, moved?.1 == 2, merged?.0 == 0, merged?.1 == 1, added,
              activeApp == front, area.contains(choice.panel.frame) else {
            fputs("AppKit smoke test failed: click=\(String(describing: clicked)) remove=\(String(describing: removed)) move=\(String(describing: moved)) merge=\(String(describing: merged)) add=\(added) focus unchanged=\(activeApp == front)\n", stderr)
            return 1
        }
        print("PASS: AppKit panel layout, front-app focus, click, right-click, reorder, timed merge and capture dialog")
        return 0
    }
}

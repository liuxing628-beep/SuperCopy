import AppKit

final class FirstClickButton: NSButton {
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
}

final class PassivePanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }

    init(title: String, frame: CGRect) {
        super.init(contentRect: .zero, styleMask: [.titled, .nonactivatingPanel], backing: .buffered, defer: false)
        self.title = title
        isReleasedWhenClosed = false
        hidesOnDeactivate = false
        level = .floating
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .transient]
        acceptsMouseMovedEvents = true
        setFrame(frame, display: false)
    }

    static func frameSize(for content: CGSize) -> CGSize {
        NSWindow.frameRect(forContentRect: CGRect(origin: .zero, size: content), styleMask: [.titled, .nonactivatingPanel]).size
    }
}

final class CaptureDialog: NSObject {
    enum Choice { case add, replace, cancel }
    let panel: PassivePanel
    var selected: ((Choice) -> Void)?

    init(incoming: Int, existing: Int, anchor: CGPoint, area: CGRect) {
        let width = min(495, area.width)
        let frame = Placement.near(anchor, size: PassivePanel.frameSize(for: CGSize(width: width, height: 132)), within: area)
        panel = PassivePanel(title: "SuperCopy · New content", frame: frame)
        super.init()
        let root = NSView(frame: CGRect(origin: .zero, size: panel.contentRect(forFrameRect: frame).size))
        let body = NSTextField(wrappingLabelWithString:
            "Captured \(incoming) new blocks. You have \(existing) saved.\nAdd the new blocks at the front, or replace the saved set?")
        body.font = .systemFont(ofSize: 13)
        body.frame = CGRect(x: 16, y: 55, width: root.bounds.width - 32, height: max(0, root.bounds.height - 69))
        root.addSubview(body)
        let entries: [(String, Selector)] = [
            ("Add to front", #selector(add)), ("Replace all", #selector(replace)), ("Cancel", #selector(cancel))
        ]
        let buttonWidth = floor((root.bounds.width - 40) / 3)
        for (i, entry) in entries.enumerated() {
            let button = FirstClickButton(title: entry.0, target: self, action: entry.1)
            button.bezelStyle = .rounded
            button.frame = CGRect(x: 12 + CGFloat(i) * (buttonWidth + 8), y: 14, width: buttonWidth, height: 30)
            root.addSubview(button)
        }
        panel.contentView = root
    }
    @objc private func add() { selected?(.add) }
    @objc private func replace() { selected?(.replace) }
    @objc private func cancel() { selected?(.cancel) }
}

final class BlocksPanel {
    let panel: PassivePanel
    let canvas: BlockCanvas
    let header = NSTextField(labelWithString: "")
    let footer = NSTextField(wrappingLabelWithString: "")
    private let scroll = NSScrollView()
    var action: ((BlockCanvas.Action) -> Void)? { get { canvas.action } set { canvas.action = newValue } }

    init(items: [String], anchor: CGPoint, area: CGRect) {
        let width = min(820, area.width)
        canvas = BlockCanvas(frame: .zero)
        canvas.update(items, width: width - 16)
        // Size to content for short lists; keep a scrollable viewport for long lists.
        let ideal = max(352, min(width, canvas.idealWidth + 16))
        canvas.update(items, width: min(width, ideal) - 16)
        let content = CGSize(width: min(width, ideal), height: min(486, max(166, canvas.frame.height + 77)))
        let frame = Placement.near(anchor, size: PassivePanel.frameSize(for: content), within: area)
        panel = PassivePanel(title: "SuperCopy · Clipboard Blocks", frame: frame)
        let root = NSView(frame: CGRect(origin: .zero, size: panel.contentRect(forFrameRect: frame).size))
        header.font = .systemFont(ofSize: 12, weight: .semibold)
        header.frame = CGRect(x: 12, y: root.bounds.height - 25, width: root.bounds.width - 24, height: 18)
        footer.font = .systemFont(ofSize: 11)
        footer.textColor = .secondaryLabelColor
        footer.frame = CGRect(x: 12, y: 7, width: root.bounds.width - 24, height: 34)
        footer.stringValue = "Click: paste · Right-click: remove\nDrag edges: reorder · Hold over center: merge"
        scroll.frame = CGRect(x: 0, y: 47, width: root.bounds.width, height: max(1, root.bounds.height - 78))
        scroll.hasVerticalScroller = true
        scroll.autohidesScrollers = true
        scroll.scrollerStyle = .legacy
        scroll.drawsBackground = false
        scroll.documentView = canvas
        root.addSubview(scroll); root.addSubview(header); root.addSubview(footer)
        panel.contentView = root
        update(items)
    }
    func update(_ items: [String]) {
        header.stringValue = "\(items.count) saved \(items.count == 1 ? "block" : "blocks")"
        canvas.update(items, width: max(1, scroll.contentSize.width))
    }
    func close() {
        canvas.cancelDrag()
        panel.orderOut(nil)
    }
}

final class BlockCanvas: NSView {
    enum Action { case paste(Int), remove(Int), move(Int, Int), merge(Int, Int) }
    var action: ((Action) -> Void)?
    private(set) var items: [String] = []
    private var previews: [String] = []
    private(set) var layout = TileLayout(sizes: [], width: 1, gap: 8)
    private let pad: CGFloat = 9, gap: CGFloat = 8
    private var hover: Int?
    private var rightStart: Int?
    private var drag: DragSession?
    private var point = CGPoint.zero
    private var timer: Timer?
    private var tracking: NSTrackingArea?
    private var measureWidth: CGFloat = 0
    private var measureSizes: [CGSize] = []
    private var measureCache: [String: CGSize] = [:]
    private let bodyFont = NSFont.systemFont(ofSize: 13)
    private let labelFont = NSFont.monospacedDigitSystemFont(ofSize: 10, weight: .semibold)
    override var isFlipped: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override var acceptsFirstResponder: Bool { false }
    var idealWidth: CGFloat {
        let area = measureSizes.reduce(CGFloat(0)) { $0 + ($1.width + gap) * ($1.height + gap) }
        return max(335, sqrt(area * 2.15))
    }

    func update(_ items: [String], width: CGFloat) {
        cancelDrag()
        let scrollPoint = enclosingScrollView?.contentView.bounds.origin ?? .zero
        self.items = items
        previews = items.map { String($0.prefix(320)) }
        let maxWidth = min(360, max(1, width - pad * 2))
        if measureWidth != maxWidth { measureCache.removeAll(); measureWidth = maxWidth }
        if measureCache.count > BlockStore.maxBlocks { measureCache.removeAll() }
        measureSizes = previews.map { text in
            if let size = measureCache[text] { return size }
            let font: [NSAttributedString.Key: Any] = [.font: bodyFont]
            let textSize = (text as NSString).size(withAttributes: font)
            let w = min(maxWidth, max(107, ceil(textSize.width) + 22))
            let h = (text as NSString).boundingRect(with: CGSize(width: max(1, w - 20), height: .greatestFiniteMagnitude),
                    options: [.usesLineFragmentOrigin, .usesFontLeading], attributes: font).height
            let size = CGSize(width: w, height: max(52, min(80, ceil(h) + 29)))
            measureCache[text] = size
            return size
        }
        layout = TileLayout(sizes: measureSizes, width: max(1, width - pad * 2), gap: gap)
        setFrameSize(CGSize(width: width, height: max(layout.height + pad * 2, enclosingScrollView?.contentSize.height ?? 0)))
        if let scroll = enclosingScrollView {
            scroll.contentView.scroll(to: CGPoint(x: 0, y: min(scrollPoint.y, max(0, frame.height - scroll.contentSize.height))))
            scroll.reflectScrolledClipView(scroll.contentView)
        }
        hover = nil; toolTip = nil
        needsDisplay = true
    }
    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let tracking = tracking { removeTrackingArea(tracking) }
        let area = NSTrackingArea(rect: .zero, options: [.mouseMoved, .mouseEnteredAndExited, .activeAlways, .inVisibleRect],
                                  owner: self, userInfo: nil)
        addTrackingArea(area); tracking = area
    }
    private func documentPoint(_ p: CGPoint) -> CGPoint { CGPoint(x: p.x - pad, y: p.y - pad) }
    private func eventPoint(_ event: NSEvent) -> CGPoint { convert(event.locationInWindow, from: nil) }
    private func hit(_ p: CGPoint) -> Int? { visibleRect.contains(p) ? layout.hit(documentPoint(p)) : nil }
    private var now: TimeInterval { ProcessInfo.processInfo.systemUptime }
    override func mouseMoved(with event: NSEvent) {
        let next = hit(eventPoint(event))
        guard next != hover else { return }
        hover = next
        toolTip = next.map { String(items[$0].prefix(3000)) }
        needsDisplay = true
    }
    override func mouseExited(with event: NSEvent) { hover = nil; toolTip = nil; needsDisplay = true }
    override func mouseDown(with event: NSEvent) {
        cancelDrag()
        let p = eventPoint(event)
        if let index = hit(p) { drag = DragSession(source: index, start: p); point = p }
    }
    override func mouseDragged(with event: NSEvent) {
        updateDrag(at: eventPoint(event))
        guard drag?.dragging == true, timer == nil else { return }
        toolTip = nil; hover = nil
        let tick = Timer(timeInterval: 0.045, repeats: true) { [weak self] _ in self?.dragTick() }
        RunLoop.main.add(tick, forMode: .common)
        RunLoop.main.add(tick, forMode: .eventTracking)
        timer = tick
    }
    override func mouseUp(with event: NSEvent) {
        let p = eventPoint(event)
        updateDrag(at: p)
        let drop = drag?.drop(inside: visibleRect.contains(p), hit: hit(p), boundary: layout.insertion(documentPoint(p)), now: now)
        cancelDrag()
        switch drop {
        case .paste(let i): action?(.paste(i))
        case .move(let source, let boundary): action?(.move(source, boundary))
        case .merge(let source, let target): action?(.merge(source, target))
        default: break
        }
    }
    override func rightMouseDown(with event: NSEvent) {
        cancelDrag()
        rightStart = hit(eventPoint(event))
    }
    override func rightMouseUp(with event: NSEvent) {
        let index = rightStart; rightStart = nil
        if let index = index, hit(eventPoint(event)) == index { action?(.remove(index)) }
    }
    private func updateDrag(at p: CGPoint) {
        guard var session = drag else { return }
        point = p
        let candidate = visibleRect.contains(p) ? layout.mergeTarget(documentPoint(p), source: session.source) : nil
        session.update(point: p, candidate: candidate, now: now)
        drag = session; needsDisplay = true
    }
    private func dragTick() {
        guard let window = window, let scroll = enclosingScrollView, drag?.dragging == true else { return }
        let pointer = window.convertPoint(fromScreen: NSEvent.mouseLocation)
        var p = convert(pointer, from: nil)
        let visible = visibleRect
        if visible.contains(p) {
            let delta: CGFloat = p.y < visible.minY + 24 ? -22 : p.y > visible.maxY - 24 ? 22 : 0
            if delta != 0 {
                let y = max(0, min(visible.minY + delta, frame.height - visible.height))
                scroll.contentView.scroll(to: CGPoint(x: 0, y: y))
                scroll.reflectScrolledClipView(scroll.contentView)
                p = convert(pointer, from: nil)
            }
        }
        updateDrag(at: p)
    }
    func cancelDrag() {
        timer?.invalidate(); timer = nil; drag = nil; rightStart = nil; needsDisplay = true
    }

    override func draw(_ dirtyRect: NSRect) {
        NSColor.windowBackgroundColor.setFill(); dirtyRect.fill()
        let endY = dirtyRect.maxY
        for i in layout.firstVisible(dirtyRect.minY - pad)..<layout.tiles.count {
            let tile = layout.tiles[i].offsetBy(dx: pad, dy: pad)
            if tile.minY > endY { break }
            let candidate = drag?.candidate == i
            let ready = candidate && drag?.ready(now: now) == true
            let highlighted = i == hover || (drag?.dragging == true && i == drag?.source)
            let tint: NSColor = candidate ? (ready ? .systemGreen : .systemOrange) : .controlAccentColor
            let fill = (candidate || highlighted) ? tint.withAlphaComponent(0.14) : .controlBackgroundColor
            let outline = (candidate || highlighted) ? tint : .separatorColor
            let shape = NSBezierPath(roundedRect: tile.insetBy(dx: 0.5, dy: 0.5), xRadius: 6, yRadius: 6)
            fill.setFill(); shape.fill()
            outline.setStroke(); shape.lineWidth = candidate ? 2 : 1; shape.stroke()
            let title = candidate ? (ready ? "Merge" : "Hold…") : String(i + 1)
            (title as NSString).draw(in: CGRect(x: tile.minX + 10, y: tile.minY + 6, width: tile.width - 20, height: 14),
                                    withAttributes: [.font: labelFont, .foregroundColor: candidate ? tint : NSColor.secondaryLabelColor])
            drawText(previews[i], in: CGRect(x: tile.minX + 10, y: tile.minY + 22, width: tile.width - 20, height: tile.height - 26))
        }
        if let drag = drag, drag.dragging, visibleRect.contains(point) {
            if drag.candidate == nil, !layout.tiles.isEmpty {
                let boundary = layout.insertion(documentPoint(point))
                let before = boundary < layout.tiles.count
                let tile = layout.tiles[before ? boundary : layout.tiles.count - 1]
                let x = (before ? tile.minX - gap / 2 : tile.maxX + gap / 2) + pad
                NSColor.controlAccentColor.setFill()
                CGRect(x: x - 1.5, y: tile.minY + pad, width: 3, height: tile.height).fill()
            }
            let text = drag.candidate == nil ? previews[drag.source] : (drag.ready(now: now) ? "Release to merge" : "Hold to merge")
            let width = min(204, visibleRect.width - 8)
            let r = CGRect(x: max(visibleRect.minX + 4, min(point.x + 12, visibleRect.maxX - width - 4)),
                           y: max(visibleRect.minY + 4, min(point.y + 14, visibleRect.maxY - 44)), width: width, height: 40)
            NSColor.controlBackgroundColor.setFill()
            let ghost = NSBezierPath(roundedRect: r, xRadius: 6, yRadius: 6)
            ghost.fill()
            NSColor.controlAccentColor.setStroke(); ghost.stroke()
            drawText(text, in: r.insetBy(dx: 8, dy: 5))
        }
    }
    private func drawText(_ text: String, in rect: CGRect) {
        guard rect.width > 0, rect.height > 0 else { return }
        let paragraph = NSMutableParagraphStyle()
        paragraph.lineBreakMode = .byTruncatingTail
        (text as NSString).draw(with: rect, options: [.usesLineFragmentOrigin, .truncatesLastVisibleLine],
                               attributes: [.font: bodyFont, .foregroundColor: NSColor.labelColor, .paragraphStyle: paragraph])
    }
    deinit { timer?.invalidate() }
}

import Foundation
import CoreGraphics

struct BlockStore {
    static let maxCharacters = 2_000_000
    static let maxBlocks = 50_000
    private(set) var items: [String] = []
    enum Problem: LocalizedError {
        case empty, tooLong, tooMany
        var errorDescription: String? {
            switch self {
            case .empty: return "No non-empty text was copied. Saved blocks are unchanged."
            case .tooLong: return "The text exceeds 2,000,000 characters. Select less text or use Replace all."
            case .tooMany: return "The set exceeds 50,000 blocks. Select less text or use Replace all."
            }
        }
    }
    private static func blank(_ text: String) -> Bool {
        text.unicodeScalars.allSatisfy { $0.properties.isWhitespace }
    }
    static func parse(_ text: String) throws -> [String] {
        guard text.utf16.count <= maxCharacters else { throw Problem.tooLong }
        var result: [String] = []
        for run in text.unicodeScalars.split(whereSeparator: {
            switch $0.value {
            case 9, 10, 13, 0x85, 0x2028, 0x2029: return true
            default: return false
            }
        }) {
            let block = String(run)
            guard !blank(block) else { continue }
            guard result.count < maxBlocks else { throw Problem.tooMany }
            result.append(block)
        }
        guard !result.isEmpty else { throw Problem.empty }
        return result
    }
    mutating func commit(_ incoming: [String], keepingExisting: Bool) throws {
        guard !incoming.isEmpty, !incoming.contains(where: Self.blank) else { throw Problem.empty }
        let existing = keepingExisting ? items : []
        guard incoming.count + existing.count <= Self.maxBlocks else { throw Problem.tooMany }
        guard incoming.reduce(0, { $0 + $1.utf16.count }) + existing.reduce(0, { $0 + $1.utf16.count }) <= Self.maxCharacters else {
            throw Problem.tooLong
        }
        items = incoming + existing
    }
    mutating func remove(_ index: Int) {
        if items.indices.contains(index) { items.remove(at: index) }
    }
    mutating func move(_ source: Int, toBoundary boundary: Int) {
        guard items.indices.contains(source), (0...items.count).contains(boundary) else { return }
        let target = boundary > source ? boundary - 1 : boundary
        guard source != target else { return }
        let value = items.remove(at: source)
        items.insert(value, at: target)
    }
    mutating func merge(_ source: Int, into target: Int) throws {
        guard source != target, items.indices.contains(source), items.indices.contains(target) else { return }
        guard items.reduce(2, { $0 + $1.utf16.count }) <= Self.maxCharacters else { throw Problem.tooLong }
        items[target] += "\r\n" + items[source]
        items.remove(at: source)
    }
}

struct TileLayout {
    let tiles: [CGRect]
    let height: CGFloat
    init(sizes: [CGSize], width: CGFloat, gap: CGFloat) {
        var result: [CGRect] = []
        var start = 0, x: CGFloat = 0, y: CGFloat = 0, rowHeight: CGFloat = 0
        func finishRow() {
            for i in start..<result.count { result[i].size.height = rowHeight }
        }
        for size in sizes {
            let w = min(max(1, width), max(1, size.width)), h = max(1, size.height)
            if x > 0 && x + w > width {
                finishRow()
                start = result.count; x = 0; y += rowHeight + gap; rowHeight = 0
            }
            result.append(CGRect(x: x, y: y, width: w, height: h))
            x += w + gap
            rowHeight = max(rowHeight, h)
        }
        finishRow()
        tiles = result
        height = y + rowHeight
    }
    func firstVisible(_ y: CGFloat) -> Int {
        var low = 0, high = tiles.count
        while low < high {
            let mid = (low + high) / 2
            if tiles[mid].maxY <= y { low = mid + 1 } else { high = mid }
        }
        return low
    }
    func hit(_ point: CGPoint) -> Int? {
        var i = firstVisible(point.y)
        while i < tiles.count && tiles[i].minY <= point.y {
            if tiles[i].contains(point) { return i }
            i += 1
        }
        return nil
    }
    func mergeTarget(_ point: CGPoint, source: Int) -> Int? {
        guard let i = hit(point), i != source else { return nil }
        let r = tiles[i]
        return r.insetBy(dx: r.width / 5, dy: r.height / 5).contains(point) ? i : nil
    }
    func insertion(_ point: CGPoint) -> Int {
        var i = firstVisible(point.y)
        guard i < tiles.count else { return tiles.count }
        if i > 0 && point.y < tiles[i].minY && point.y - tiles[i - 1].maxY < tiles[i].minY - point.y { i -= 1 }
        let y = tiles[i].minY
        while i > 0 && tiles[i - 1].minY == y { i -= 1 }
        while i < tiles.count && tiles[i].minY == y {
            if point.x < tiles[i].midX { return i }
            i += 1
        }
        return i
    }
}

struct DragSession {
    enum Drop: Equatable { case none, paste(Int), move(Int, Int), merge(Int, Int) }
    static let dwell: TimeInterval = 0.45
    let source: Int
    let start: CGPoint
    private(set) var dragging = false
    private(set) var candidate: Int?
    private var entered: TimeInterval = 0
    mutating func update(point: CGPoint, candidate next: Int?, now: TimeInterval) {
        if !dragging && (abs(point.x - start.x) >= 4 || abs(point.y - start.y) >= 4) { dragging = true }
        let next = dragging && next != source ? next : nil
        if next != candidate { candidate = next; entered = now }
    }
    func ready(now: TimeInterval) -> Bool { candidate != nil && now - entered >= Self.dwell }
    func drop(inside: Bool, hit: Int?, boundary: Int, now: TimeInterval) -> Drop {
        guard inside else { return .none }
        if !dragging { return hit == source ? .paste(source) : .none }
        if let target = candidate { return ready(now: now) ? .merge(source, target) : .none }
        return .move(source, boundary)
    }
}

enum Placement {
    static func near(_ anchor: CGPoint, size: CGSize, within area: CGRect) -> CGRect {
        let width = min(size.width, area.width), height = min(size.height, area.height), gap: CGFloat = 12
        var x = anchor.x + gap, y = anchor.y - gap - height
        if x + width > area.maxX { x = anchor.x - gap - width }
        if y < area.minY { y = anchor.y + gap }
        return CGRect(x: max(area.minX, min(x, area.maxX - width)),
                      y: max(area.minY, min(y, area.maxY - height)), width: width, height: height)
    }
}

import Foundation
import CoreGraphics

@main
struct CoreTests {
    struct Fixture: Decodable {
        struct Operation: Decodable { let type: String, source: Int; let target: Int? }
        let name: String, initial: String, incoming: String?, addToFront: Bool?, expected: [String]
        let operations: [Operation]?
    }
    static func main() throws {
        var checks = 0
        func expect(_ condition: @autoclosure () -> Bool, _ description: String) {
            guard condition() else { fatalError(description) }
            checks += 1
        }
        let fixtures = try JSONDecoder().decode([Fixture].self, from: Data(contentsOf: URL(fileURLWithPath: CommandLine.arguments[1])))
        for fixture in fixtures {
            var store = BlockStore()
            try store.commit(BlockStore.parse(fixture.initial), keepingExisting: false)
            for operation in fixture.operations ?? [] {
                switch operation.type {
                case "move": store.move(operation.source, toBoundary: operation.target!)
                case "remove": store.remove(operation.source)
                case "merge": try store.merge(operation.source, into: operation.target!)
                default: fatalError("Unknown operation")
                }
            }
            if let incoming = fixture.incoming {
                try store.commit(BlockStore.parse(incoming), keepingExisting: fixture.addToFront ?? false)
            }
            expect(store.items == fixture.expected, fixture.name)
        }
        var store = BlockStore()
        try store.commit(Array(repeating: "X", count: BlockStore.maxBlocks), keepingExisting: false)
        do { try store.commit(["Y"], keepingExisting: true); fatalError("Block cap not enforced") }
        catch BlockStore.Problem.tooMany { expect(store.items.count == BlockStore.maxBlocks && store.items.first == "X", "Oversized addition is atomic") }
        try store.commit([String(repeating: "x", count: BlockStore.maxCharacters - 1), "y"], keepingExisting: false)
        do { try store.merge(1, into: 0); fatalError("Merge cap not enforced") }
        catch BlockStore.Problem.tooLong { expect(store.items.count == 2 && store.items.last == "y", "Oversized merge is atomic") }
        do { _ = try BlockStore.parse(" \t\r\n　"); fatalError("Accepted whitespace capture") }
        catch BlockStore.Problem.empty { checks += 1 }
        let sizes = [CGSize(width: 110, height: 60), CGSize(width: 155, height: 82), CGSize(width: 105, height: 50)]
        let layout = TileLayout(sizes: sizes, width: 290, gap: 8)
        expect(layout.tiles[0].height == 82 && layout.tiles[2].minY == 90 && layout.height == 140, "Wrapped row geometry")
        expect(layout.firstVisible(85) == 2 && layout.hit(CGPoint(x: 114, y: 40)) == nil, "Visible-range and gaps")
        expect(layout.mergeTarget(CGPoint(x: 180, y: 40), source: 0) == 1, "Merge on another block center")
        expect(layout.mergeTarget(CGPoint(x: 121, y: 40), source: 0) == nil, "Reorder at edges")
        expect(layout.insertion(CGPoint(x: 280, y: 41)) == 2, "Right edge inserts after row")
        var drag = DragSession(source: 0, start: CGPoint(x: 40, y: 40))
        drag.update(point: CGPoint(x: 41, y: 41), candidate: nil, now: 0)
        expect(drag.drop(inside: true, hit: 0, boundary: 0, now: 0.1) == .paste(0), "Small pointer jitter still clicks")
        drag.update(point: CGPoint(x: 180, y: 40), candidate: 1, now: 1)
        expect(drag.drop(inside: true, hit: 1, boundary: 1, now: 1.4) == .none, "Early center release cancels")
        expect(drag.drop(inside: true, hit: 1, boundary: 1, now: 1.5) == .merge(0, 1), "Hold then release merges")
        drag.update(point: CGPoint(x: 122, y: 40), candidate: nil, now: 1.6)
        expect(drag.drop(inside: true, hit: 1, boundary: 1, now: 1.7) == .move(0, 1), "Leaving center clears merge")
        expect(drag.drop(inside: false, hit: nil, boundary: 1, now: 2) == .none, "Outside release never pastes")
        for screen in [CGRect(x: -1920, y: -200, width: 1920, height: 1030),
                       CGRect(x: 0, y: 1120, width: 1920, height: 1080),
                       CGRect(x: 1600, y: 0, width: 410, height: 240)] {
            let frame = Placement.near(CGPoint(x: screen.maxX - 2, y: screen.minY + 2), size: CGSize(width: 480, height: 280), within: screen)
            expect(screen.contains(frame), "Dialog fits source display")
        }
        print("PASS: \(checks) macOS core checks, including \(fixtures.count) shared Windows/macOS cases")
    }
}

using System;
using System.Linq;
using System.Drawing;
using System.Runtime.InteropServices;
using SuperCopy;

class Tests
{
    static int checks;
    static void Expect(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static void Main()
    {
        var store = new BlockStore();
        store.Replace("名称\t数量\t价格\r\n摄像头\t12\t99.50\r\n");
        Expect(store.Items.SequenceEqual(new[] { "名称", "数量", "价格", "摄像头", "12", "99.50" }), "Excel TSV/CRLF");
        store.Replace("  第一段\n\n第二段\t第三段　第四段\u00a0第五段  ");
        Expect(store.Items.SequenceEqual(new[] { "  第一段", "第二段", "第三段　第四段\u00a0第五段  " }), "Spaces and Unicode spaces preserved");
        store.Replace("A\tA\tB,中文\thttps://example.com/x?y=1\t🐈");
        Expect(store.Items.SequenceEqual(new[] { "A", "A", "B,中文", "https://example.com/x?y=1", "🐈" }), "Order, duplicates, punctuation, emoji");
        store.Replace("John Smith\tNew York Office\r\n型号 A  B\t  保留两端空格  ");
        Expect(store.Items.SequenceEqual(new[] { "John Smith", "New York Office", "型号 A  B", "  保留两端空格  " }), "Excel cells containing spaces remain intact");
        store.Replace("第一行\r第二行\n第三行\r\n第四行\u0085第五行\u2028第六行\u2029第七行");
        Expect(store.Items.Length == 7, "CR, LF, CRLF and Unicode line breaks");
        store.Replace("\t\t A B \t   \tC D\r\n\r\n");
        Expect(store.Items.SequenceEqual(new[] { " A B ", "C D" }), "Empty cells omitted, significant spaces preserved");
        store.Replace("a b　c\u00a0d");
        Expect(store.Items.SequenceEqual(new[] { "a b　c\u00a0d" }), "Space-only separators do not split a block");
        var previous = store.Items;
        foreach (var invalid in new[] { null, "", " \r\n\t", new string('a', BlockStore.MaxCharacters + 1), string.Concat(Enumerable.Repeat("x\t", BlockStore.MaxBlocks + 1)) })
        {
            bool failed = false;
            try { store.Replace(invalid); } catch (ArgumentException) { failed = true; }
            Expect(failed && Object.ReferenceEquals(store.Items, previous), "Failed capture preserves previous blocks");
        }
        store.Replace(string.Concat(Enumerable.Repeat("x\t", BlockStore.MaxBlocks)));
        Expect(store.Items.Length == BlockStore.MaxBlocks, "Maximum block boundary");
        store.Replace(new string('中', BlockStore.MaxCharacters));
        Expect(store.Items.Length == 1 && store.Items[0].Length == BlockStore.MaxCharacters, "Maximum text boundary");
        Expect(Marshal.SizeOf(typeof(Native.Input)) == (IntPtr.Size == 8 ? 40 : 28), "Win32 INPUT layout");
        Expect(Marshal.SizeOf(typeof(Native.MouseInfo)) == (IntPtr.Size == 8 ? 32 : 24), "Win32 MSLLHOOKSTRUCT layout");
        store.Replace("A\tB\tA");
        var parsed = BlockStore.Parse("New York\tC");
        Expect(store.Items.SequenceEqual(new[] { "A", "B", "A" }), "Parsing new capture does not mutate saved blocks before user confirmation");
        store.Commit(parsed, true);
        Expect(store.Items.SequenceEqual(new[] { "A", "B", "A", "New York", "C" }), "Append preserves order, duplicates and spaces");
        store.RemoveAt(2);
        Expect(store.Items.SequenceEqual(new[] { "A", "B", "New York", "C" }), "Removing one duplicate preserves the other occurrence");
        store.RemoveAt(0); store.RemoveAt(store.Items.Length - 1);
        Expect(store.Items.SequenceEqual(new[] { "B", "New York" }), "Remove first and last block");
        var beforeInvalidRemoval = store.Items;
        try { store.RemoveAt(100); } catch (ArgumentOutOfRangeException) { }
        Expect(Object.ReferenceEquals(store.Items, beforeInvalidRemoval), "Invalid removal preserves saved set");
        store.Commit(new[] { "replacement" }, false);
        Expect(store.Items.SequenceEqual(new[] { "replacement" }), "Replace discards old blocks");
        store.RemoveAt(0);
        Expect(store.Items.Length == 0, "Removing final block leaves empty set");
        store.Commit(Enumerable.Repeat("x", BlockStore.MaxBlocks).ToArray(), false);
        var full = store.Items;
        bool tooMany = false;
        try { store.Commit(new[] { "extra" }, true); } catch (ArgumentException) { tooMany = true; }
        Expect(tooMany && Object.ReferenceEquals(full, store.Items), "Oversized append atomically preserves existing blocks");
        store.Replace(new string('x', BlockStore.MaxCharacters));
        full = store.Items; bool tooLong = false;
        try { store.Commit(new[] { "extra" }, true); } catch (ArgumentException) { tooLong = true; }
        Expect(tooLong && Object.ReferenceEquals(full, store.Items), "Append enforces combined character limit");
        store.Commit(new[] { "small" }, false);
        Expect(store.Items.SequenceEqual(new[] { "small" }), "Replace remains possible after rejected append");
        string error;
        Expect(Hotkey.IsValid(6, 0x43, out error) && Hotkey.IsValid(3, 0x77, out error), "Custom capture and panel combinations accepted");
        Expect(!Hotkey.IsValid(2, 0x43, out error) && !Hotkey.IsValid(2, 0x56, out error), "Native copy/paste shortcuts cannot cause synthetic input recursion");
        Expect(!Hotkey.IsValid(0, 0x43, out error) && !Hotkey.IsValid(4, 0x43, out error), "Ordinary typing and Shift-only combinations are not captured globally");
        Expect(!Hotkey.IsValid(3, 0x11, out error) && !Hotkey.IsValid(1, 0x73, out error), "Modifier-only and Alt+F4 shortcuts rejected");
        Expect(new Hotkey(6, 0x43).SameAs(new Hotkey(6, 0x43)) && !new Hotkey(6, 0x43).SameAs(new Hotkey(3, 0x77)), "Shortcut identity detects conflicting capture and panel assignments");
        var grid = TileLayout.Create(new[] { new Size(100, 40), new Size(100, 60), new Size(100, 40) }, 220, 8);
        Expect(grid.Bounds[0].Top == grid.Bounds[1].Top && grid.Bounds[2].Top >= grid.Bounds[0].Bottom + 8, "Multiple columns with automatic row wrapping");
        Expect(grid.Bounds[0].Height == 60 && grid.Bounds[1].Height == 60, "Row height accommodates long content");
        Expect(grid.HitTest(new Point(104, 20)) == -1, "Gaps cannot select a block");
        Expect(grid.HitTest(new Point(10, grid.Bounds[2].Top + 5)) == 2, "Scrolled document coordinates resolve the original block");
        var tiny = TileLayout.Create(new[] { new Size(1000, 70), new Size(100, 40) }, 37, 8);
        Expect(tiny.Bounds.All(r => r.Right <= 37 && r.Width > 0), "Oversized tiles fit narrow screens");
        var empty = TileLayout.Create(new Size[0], 500, 8);
        Expect(empty.Height == 0 && empty.HitTest(Point.Empty) == -1, "Empty layout");
        var random = new Random(1234);
        bool geometryValid = true, hitValid = true, cullValid = true;
        for (int run = 0; run < 40; run++) {
            int width = random.Next(20, 900);
            var sizes = Enumerable.Range(0, 100).Select(i => new Size(random.Next(40, 500), random.Next(20, 130))).ToArray();
            var tiles = TileLayout.Create(sizes, width, 8);
            for (int i = 0; i < tiles.Bounds.Length; i++) {
                var r = tiles.Bounds[i];
                geometryValid &= r.Left >= 0 && r.Right <= width && r.Top >= 0 && r.Bottom <= tiles.Height;
                hitValid &= tiles.HitTest(new Point(r.Left + r.Width / 2, r.Top + r.Height / 2)) == i;
                for (int j = 0; j < i; j++) geometryValid &= !r.IntersectsWith(tiles.Bounds[j]);
                if (i > 0) geometryValid &= r.Top > tiles.Bounds[i - 1].Top || r.Left > tiles.Bounds[i - 1].Left;
            }
            int top = random.Next(0, tiles.Height + 1);
            int first = tiles.FirstVisible(top);
            cullValid &= tiles.Bounds.Take(first).All(r => r.Bottom <= top);
            cullValid &= first == tiles.Bounds.Length || tiles.Bounds[first].Bottom > top;
        }
        Expect(geometryValid, "Randomized layouts preserve order without overlap or overflow");
        Expect(hitValid, "Every tile center selects its original item after wrapping");
        Expect(cullValid, "Visible-row lookup does not omit visible blocks");
        var large = TileLayout.Create(Enumerable.Repeat(new Size(110, 60), BlockStore.MaxBlocks).ToArray(), 800, 8);
        var last = large.Bounds[BlockStore.MaxBlocks - 1];
        Expect(large.HitTest(new Point(last.Left + 1, last.Top + 1)) == BlockStore.MaxBlocks - 1, "50,000-block layout remains selectable to the last row");
        store.Replace("A\tB\tC\tD");
        store.Move(0, 4);
        Expect(store.Items.SequenceEqual(new[] { "B", "C", "D", "A" }), "Drag first block to end");
        store.Move(3, 0);
        Expect(store.Items.SequenceEqual(new[] { "A", "B", "C", "D" }), "Drag last block to beginning");
        store.Move(1, 3);
        Expect(store.Items.SequenceEqual(new[] { "A", "C", "B", "D" }), "Forward move uses original insertion boundaries");
        var unchanged = store.Items;
        store.Move(2, 2); store.Move(2, 3);
        Expect(Object.ReferenceEquals(unchanged, store.Items), "Dropping before or after source is a no-op");
        bool badMove = false;
        try { store.Move(0, 5); } catch (ArgumentOutOfRangeException) { badMove = true; }
        Expect(badMove && Object.ReferenceEquals(unchanged, store.Items), "Invalid drag cannot mutate saved blocks");
        string duplicateOne = new string('a', 1), duplicateTwo = new string('a', 1);
        store.Commit(new[] { duplicateOne, "B", duplicateTwo }, false);
        store.Move(2, 0);
        Expect(Object.ReferenceEquals(store.Items[0], duplicateTwo) && Object.ReferenceEquals(store.Items[1], duplicateOne), "Drag moves just the selected duplicate occurrence");
        store.Commit(new[] { "appended" }, true);
        Expect(store.Items.Last() == "appended" && Object.ReferenceEquals(store.Items[0], duplicateTwo), "Appending preserves custom block order");
        bool orderValid = true;
        store.Commit(Enumerable.Range(0, 30).Select(i => "item " + i).ToArray(), false);
        for (int run = 0; run < 200; run++) {
            var before = store.Items;
            int from = random.Next(before.Length), slot = random.Next(before.Length + 1);
            string moved = before[from];
            var others = before.Where((value, index) => index != from).ToArray();
            store.Move(from, slot);
            int destination = Array.IndexOf(store.Items, moved);
            orderValid &= store.Items.Length == before.Length && destination == (slot > from ? slot - 1 : slot);
            orderValid &= store.Items.Where((value, index) => index != destination).SequenceEqual(others);
        }
        Expect(orderValid, "Repeated moves preserve all content and relative order of unaffected blocks");
        Expect(grid.InsertionAt(new Point(-5, 20)) == 0 && grid.InsertionAt(new Point(500, 20)) == 2, "Drop at row edges resolves beginning or row end");
        Expect(grid.InsertionAt(new Point(20, grid.Bounds[2].Top + 10)) == 2 && grid.InsertionAt(new Point(80, grid.Bounds[2].Top + 10)) == 3, "Drop target distinguishes left/right halves of a wrapped tile");
        Expect(grid.InsertionAt(new Point(0, grid.Height + 10)) == 3 && empty.InsertionAt(Point.Empty) == 0, "Drop below all blocks resolves end, including empty layout");
        var gesture = new DragGesture();
        gesture.Begin(1, new Point(20, 20), new Size(8, 8)); gesture.Move(new Point(22, 22));
        Expect(gesture.Complete(1, 1, true).Action == DragAction.Paste, "Small pointer jitter still pastes on click");
        gesture.Begin(1, new Point(20, 20), new Size(8, 8)); gesture.Move(new Point(50, 20)); gesture.Move(new Point(20, 20));
        Expect(gesture.Complete(1, 1, true).Action == DragAction.Move, "Dragging back to the source never pastes");
        gesture.Begin(1, new Point(20, 20), new Size(8, 8)); gesture.Move(new Point(200, 200));
        Expect(gesture.Complete(-1, -1, false).Action == DragAction.None, "Outside drop cancels without paste or move");
        gesture.Begin(1, Point.Empty, new Size(8, 8)); gesture.Move(new Point(50, 50)); gesture.Cancel();
        Expect(gesture.Complete(1, 1, true).Action == DragAction.None && !gesture.Active, "Lost mouse capture cancels a drag without paste");
        var extended = new Rectangle(1920, 0, 2560, 1400);
        var pointer = new Point(2400, 300);
        Expect(WindowPlacement.CaptureAnchor(extended, pointer) == pointer, "Use the pointer when it is on the source monitor");
        var anchor = WindowPlacement.CaptureAnchor(extended, new Point(300, 300));
        Expect(extended.Contains(anchor), "Keyboard capture stays on the source monitor when mouse is elsewhere");
        var confirmation = WindowPlacement.NearAnchor(pointer, new Size(590, 220), extended, 16);
        Expect(confirmation.Location == new Point(2416, 316) && extended.Contains(confirmation), "Confirmation appears beside the pointer on the extended monitor");
        confirmation = WindowPlacement.NearAnchor(new Point(4475, 1395), new Size(590, 220), extended, 16);
        Expect(extended.Contains(confirmation) && confirmation.Right < 4475 && confirmation.Bottom < 1395, "Near bottom-right corner, open to the left and above without covering the taskbar");
        var leftDisplay = new Rectangle(-1920, -180, 1920, 1040);
        confirmation = WindowPlacement.NearAnchor(new Point(-1800, -150), new Size(590, 220), leftDisplay, 16);
        Expect(leftDisplay.Contains(confirmation) && confirmation.X < 0 && confirmation.Y < 0, "Support negative monitor coordinates without jumping to the primary screen");
        var upperDisplay = new Rectangle(0, -1200, 1920, 1160);
        confirmation = WindowPlacement.NearAnchor(new Point(1900, -60), new Size(900, 350), upperDisplay, 16);
        Expect(upperDisplay.Contains(confirmation), "Support a monitor above the primary screen with a bottom taskbar");
        var tinyDisplay = new Rectangle(0, 0, 400, 200);
        confirmation = WindowPlacement.NearAnchor(new Point(390, 190), new Size(900, 350), tinyDisplay, 16);
        Expect(tinyDisplay.Contains(confirmation) && confirmation.Size == tinyDisplay.Size, "Fit oversized scaled confirmation to the work area");
        bool placementsInside = true;
        for (int run = 0; run < 300; run++) {
            var work = new Rectangle(random.Next(-4000, 3000), random.Next(-2000, 2000), random.Next(400, 3000), random.Next(250, 1800));
            var mouse = new Point(random.Next(-4000, 6000), random.Next(-2000, 4000));
            var origin = WindowPlacement.CaptureAnchor(work, mouse);
            var placed = WindowPlacement.NearAnchor(origin, new Size(random.Next(300, 1200), random.Next(150, 500)), work, 16);
            placementsInside &= work.Contains(placed) && work.Contains(origin);
        }
        Expect(placementsInside, "Randomized multi-monitor layouts keep the full confirmation in the source work area");
        store.Replace("A\tB\tC\tD");
        store.Merge(3, 1);
        Expect(store.Items.SequenceEqual(new[] { "A", "B\r\nD", "C" }), "Merge target then source at target position when source is later");
        store.Merge(0, 2);
        Expect(store.Items.SequenceEqual(new[] { "B\r\nD", "C\r\nA" }), "Merge preserves unaffected order when source is earlier");
        store.Merge(1, 0);
        Expect(store.Items.SequenceEqual(new[] { "B\r\nD\r\nC\r\nA" }), "Already merged blocks can be merged without losing internal line breaks");
        store.Commit(new[] { "new" }, true);
        Expect(store.Items.Length == 2 && store.Items[0] == "B\r\nD\r\nC\r\nA", "Appending does not re-split a merged block");
        store.RemoveAt(0);
        Expect(store.Items.SequenceEqual(new[] { "new" }), "Merged block can be removed as one item");
        store.Replace("duplicate\tkeep\tduplicate"); store.Merge(2, 0);
        Expect(store.Items.SequenceEqual(new[] { "duplicate\r\nduplicate", "keep" }), "Merge affects only the selected duplicate occurrences");
        var beforeMerge = store.Items; store.Merge(0, 0);
        Expect(Object.ReferenceEquals(store.Items, beforeMerge), "Self merge is a no-op");
        bool badMerge = false;
        try { store.Merge(0, 9); } catch (ArgumentOutOfRangeException) { badMerge = true; }
        Expect(badMerge && Object.ReferenceEquals(store.Items, beforeMerge), "Invalid merge atomically preserves all blocks");
        store.Commit(new[] { new string('x', BlockStore.MaxCharacters - 1), "y" }, false);
        beforeMerge = store.Items; bool mergeTooLong = false;
        try { store.Merge(1, 0); } catch (ArgumentException) { mergeTooLong = true; }
        Expect(mergeTooLong && Object.ReferenceEquals(store.Items, beforeMerge), "Merge separator is included in combined character limit");
        store.Commit(new[] { new string('x', BlockStore.MaxCharacters - 3), "y" }, false); store.Merge(1, 0);
        Expect(store.Items.Length == 1 && store.Items[0].Length == BlockStore.MaxCharacters, "Merge exactly at character limit succeeds");
        Expect(grid.MergeTargetAt(new Point(158, 30), 0) == 1, "Dropping in another tile's central area offers merge");
        Expect(grid.MergeTargetAt(new Point(110, 1), 0) == -1 && grid.MergeTargetAt(new Point(104, 20), 0) == -1, "Edges and gaps remain sorting zones");
        Expect(grid.MergeTargetAt(new Point(50, 30), 0) == -1, "Source tile cannot become its own merge target");
        var mergeHover = new MergeHover();
        mergeHover.Update(1, 1000); mergeHover.Update(1, 1449);
        Expect(mergeHover.Target == -1 && mergeHover.Candidate == 1, "Brief overlap does not arm a merge");
        mergeHover.Update(1, 1450);
        Expect(mergeHover.Target == 1, "Continuous 450 ms hover arms merge");
        mergeHover.Update(2, 1451);
        Expect(mergeHover.Target == -1 && mergeHover.Candidate == 2, "Changing tiles requires a new dwell");
        mergeHover.Update(2, 1901); mergeHover.Update(-1, 1902);
        Expect(mergeHover.Target == -1 && mergeHover.Candidate == -1, "Leaving target center cancels a ready merge");
        gesture.Begin(0, Point.Empty, new Size(8, 8)); gesture.Move(new Point(50, 50));
        var mergeResult = gesture.Complete(1, 1, true, 1);
        Expect(mergeResult.Action == DragAction.Merge && mergeResult.Source == 0 && mergeResult.Target == 1, "Ready center drop emits merge instead of paste or reorder");
        gesture.Begin(0, Point.Empty, new Size(8, 8)); gesture.Move(new Point(50, 50));
        Expect(gesture.Complete(1, 1, false, 1).Action == DragAction.None, "Outside or pending drop cannot merge or paste");
        bool mergeContentsValid = true;
        for (int run = 0; run < 30; run++) {
            string[] original = Enumerable.Range(0, 12).Select(i => "item " + i).ToArray();
            store.Commit(original, false);
            while (store.Items.Length > 1) {
                int count = store.Items.Length, source = random.Next(count), target = random.Next(count - 1);
                if (target >= source) target++;
                store.Merge(source, target);
                mergeContentsValid &= store.Items.Length == count - 1;
            }
            mergeContentsValid &= store.Items[0].Split(new[] { "\r\n" }, StringSplitOptions.None).OrderBy(x => x).SequenceEqual(original.OrderBy(x => x));
        }
        Expect(mergeContentsValid, "Repeated arbitrary merges retain every original item exactly once");
        Console.WriteLine("PASS: " + checks + " checks");
    }
}

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
        Console.WriteLine("PASS: " + checks + " checks");
    }
}

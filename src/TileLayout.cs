using System;
using System.Drawing;

namespace SuperCopy
{
    // Pure geometry: keep original reading order, wrapping measured tiles into rows.
    internal sealed class TileLayout
    {
        internal Rectangle[] Bounds;
        internal int Height;
        internal static TileLayout Create(Size[] preferred, int width, int gap)
        {
            width = Math.Max(1, width);
            gap = Math.Max(0, gap);
            var result = new TileLayout { Bounds = new Rectangle[preferred.Length] };
            int start = 0, x = 0, y = 0, rowHeight = 0;
            for (int i = 0; i < preferred.Length; i++) {
                int w = Math.Min(width, Math.Max(1, preferred[i].Width));
                int h = Math.Max(1, preferred[i].Height);
                if (x > 0 && x + w > width) {
                    FinishRow(result.Bounds, start, i, rowHeight);
                    y += rowHeight + gap;
                    start = i; x = 0; rowHeight = 0;
                }
                result.Bounds[i] = new Rectangle(x, y, w, h);
                x += w + gap;
                rowHeight = Math.Max(rowHeight, h);
            }
            FinishRow(result.Bounds, start, preferred.Length, rowHeight);
            result.Height = preferred.Length == 0 ? 0 : y + rowHeight;
            return result;
        }
        private static void FinishRow(Rectangle[] bounds, int start, int end, int height)
        {
            for (int i = start; i < end; i++) {
                Rectangle r = bounds[i]; r.Height = height; bounds[i] = r;
            }
        }
        // Rows have equal heights within a row, so bottom coordinates are sorted.
        internal int FirstVisible(int top)
        {
            int lo = 0, hi = Bounds.Length;
            while (lo < hi) {
                int mid = lo + (hi - lo) / 2;
                if (Bounds[mid].Bottom <= top) lo = mid + 1; else hi = mid;
            }
            return lo;
        }
        internal int InsertionAt(Point point)
        {
            if (Bounds.Length == 0) return 0;
            int index = FirstVisible(point.Y);
            if (index == Bounds.Length) return Bounds.Length;
            if (point.Y < Bounds[index].Top && index > 0 &&
                point.Y - Bounds[index - 1].Bottom < Bounds[index].Top - point.Y) index--;
            int rowTop = Bounds[index].Top;
            while (index > 0 && Bounds[index - 1].Top == rowTop) index--;
            while (index < Bounds.Length && Bounds[index].Top == rowTop) {
                Rectangle r = Bounds[index];
                if (point.X < r.Left + r.Width / 2) return index;
                index++;
            }
            return index;
        }
        internal int HitTest(Point point)
        {
            for (int i = FirstVisible(point.Y); i < Bounds.Length && Bounds[i].Top <= point.Y; i++)
                if (Bounds[i].Contains(point)) return i;
            return -1;
        }
    }
}

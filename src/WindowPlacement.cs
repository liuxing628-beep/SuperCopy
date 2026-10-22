using System;
using System.Drawing;

namespace SuperCopy
{
    internal static class WindowPlacement
    {
        internal static Point CaptureAnchor(Rectangle sourceArea, Point pointer)
        {
            // Keyboard selection can leave the mouse on a different monitor.
            // Keep the confirmation with the source application in that case.
            return sourceArea.Contains(pointer) ? pointer :
                new Point(sourceArea.Left + sourceArea.Width / 2, sourceArea.Top + sourceArea.Height / 2);
        }
        internal static Rectangle NearAnchor(Point anchor, Size requested, Rectangle area, int gap)
        {
            int width = Math.Max(1, Math.Min(requested.Width, area.Width));
            int height = Math.Max(1, Math.Min(requested.Height, area.Height));
            gap = Math.Max(0, gap);
            int x = anchor.X + gap, y = anchor.Y + gap;
            if (x + width > area.Right) x = anchor.X - gap - width;
            if (y + height > area.Bottom) y = anchor.Y - gap - height;
            x = Math.Max(area.Left, Math.Min(x, area.Right - width));
            y = Math.Max(area.Top, Math.Min(y, area.Bottom - height));
            return new Rectangle(x, y, width, height);
        }
    }
}

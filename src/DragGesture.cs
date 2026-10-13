using System;
using System.Drawing;

namespace SuperCopy
{
    internal enum DragAction { None, Paste, Move }
    internal struct DragResult
    {
        internal DragAction Action;
        internal int Source, Insertion;
    }
    // A drag stays a drag even if it returns to its starting point. This prevents
    // mouse-up from pasting after an attempted reorder or a canceled outside drop.
    internal sealed class DragGesture
    {
        internal int Source = -1;
        internal bool Dragging;
        internal bool Active { get { return Source >= 0; } }
        private Point origin;
        private Size threshold;
        internal void Begin(int source, Point point, Size size)
        {
            Cancel();
            Source = source; origin = point;
            threshold = new Size(Math.Max(1, size.Width / 2), Math.Max(1, size.Height / 2));
        }
        internal void Move(Point point)
        {
            if (!Active || Dragging) return;
            if (Math.Abs((long)point.X - origin.X) >= threshold.Width ||
                Math.Abs((long)point.Y - origin.Y) >= threshold.Height) Dragging = true;
        }
        internal DragResult Complete(int hit, int insertion, bool inside)
        {
            var result = new DragResult { Source = Source, Insertion = insertion };
            if (Active && inside) {
                if (Dragging) result.Action = DragAction.Move;
                else if (hit == Source) result.Action = DragAction.Paste;
            }
            Cancel();
            return result;
        }
        internal void Cancel() { Source = -1; Dragging = false; }
    }
}

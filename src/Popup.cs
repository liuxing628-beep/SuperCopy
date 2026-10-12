using System;
using System.Drawing;
using System.Windows.Forms;

namespace SuperCopy
{
    internal sealed class BlockPopup : Form
    {
        private readonly TileCanvas tiles;
        private readonly Label title;
        private readonly Label footer;
        private string[] items;
        private bool escapeAvailable;
        private bool sized;
        internal event Action<string> Selected;
        internal event Action<int> Removed;
        internal BlockPopup(string[] values, Point position)
        {
            items = values;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10f);
            Text = "SuperCopy · Clipboard Blocks";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Location = position;
            ClientSize = new Size(580, 360);
            title = new Label { Dock = DockStyle.Top, Height = 42, Padding = new Padding(10, 10, 0, 0) };
            footer = new Label { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(10, 6, 0, 0),
                ForeColor = Color.DimGray, Text = "Left-click: paste · Right-click: remove · Scroll for more" };
            tiles = new TileCanvas(values) { Dock = DockStyle.Fill, Font = Font };
            tiles.Chosen += Choose;
            tiles.RemoveRequested += delegate(int index) { if (Removed != null) Removed(index); };
            Controls.Add(tiles);
            Controls.Add(title);
            Controls.Add(footer);
            Load += delegate { FitToContent(position); };
        }
        private void FitToContent(Point position)
        {
            if (sized) return;
            sized = true;
            Rectangle area = Screen.FromPoint(position).WorkingArea;
            int borderWidth = Width - ClientSize.Width, borderHeight = Height - ClientSize.Height;
            float scale;
            using (Graphics graphics = CreateGraphics()) scale = graphics.DpiX / 96f;
            int maxWidth = Math.Max(1, Math.Min((int)(840 * scale), area.Width - borderWidth - 12));
            int minWidth = Math.Min(maxWidth, (int)(360 * scale));
            int bodyWidth = tiles.Prepare(minWidth, maxWidth, scale);
            int bodyHeight = tiles.MeasureHeight(bodyWidth);
            int maxBodyHeight = Math.Max(1, Math.Min((int)(480 * scale),
                area.Height - title.Height - footer.Height - borderHeight - 12));
            ClientSize = new Size(bodyWidth, title.Height + footer.Height +
                Math.Min(maxBodyHeight, Math.Max((int)(86 * scale), bodyHeight)));
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(Math.Max(area.Left, Math.Min(position.X, area.Right - Width)),
                Math.Max(area.Top, Math.Min(position.Y, area.Bottom - Height)));
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000; return cp; }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
            base.WndProc(ref m);
        }
        internal void SetEscapeAvailable(bool available)
        {
            escapeAvailable = available;
            title.Text = items.Length + " blocks · " + (available ? "Esc to close" : "Click outside to close");
        }
        internal void UpdateItems(string[] values)
        {
            items = values;
            tiles.UpdateValues(values);
            SetEscapeAvailable(escapeAvailable);
        }
        private void Choose(int index)
        {
            string value = items[index];
            Close();
            if (Selected != null) Selected(value);
        }

        // One virtual canvas rather than one control per block: 50,000 blocks only
        // paint the visible rows. Mouse handling never focuses this window.
        private sealed class TileCanvas : Control
        {
            private string[] values;
            private string[] previews;
            private readonly ToolTip tip = new ToolTip();
            private readonly PassiveScrollBar scroll = new PassiveScrollBar();
            private Size[] preferred;
            private TileLayout layout;
            private Font numberFont;
            private int gap = 8, padding = 10, numberHeight = 18;
            private int hover = -1, pressed = -1;
            private bool updating;
            private float currentScale = 1f;
            private MouseButtons pressedButton;
            internal event Action<int> Chosen;
            internal event Action<int> RemoveRequested;
            internal TileCanvas(string[] values)
            {
                this.values = values;
                previews = new string[values.Length];
                for (int i = 0; i < values.Length; i++)
                    previews[i] = values[i].Length > 600 ? values[i].Substring(0, 600) + "…" : values[i];
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                SetStyle(ControlStyles.Selectable, false);
                TabStop = false;
                BackColor = Color.FromArgb(245, 247, 251);
                scroll.Visible = false;
                scroll.ValueChanged += delegate { if (!updating) { SetHover(-1); pressed = -1; Invalidate(); } };
                Controls.Add(scroll);
            }
            internal void UpdateValues(string[] incoming)
            {
                // Hide stale tooltips before replacing arrays; retain the scroll position.
                SetHover(-1); pressed = -1;
                values = incoming;
                previews = new string[incoming.Length];
                for (int i = 0; i < incoming.Length; i++) previews[i] = incoming[i].Length > 600 ? incoming[i].Substring(0, 600) + "…" : incoming[i];
                Prepare(ClientSize.Width, ClientSize.Width, currentScale);
            }
            internal int Prepare(int minWidth, int maxWidth, float scale)
            {
                currentScale = scale;
                gap = Math.Max(4, (int)(8 * scale));
                padding = Math.Max(5, (int)(10 * scale));
                numberHeight = Math.Max(Font.Height, (int)(18 * scale));
                if (numberFont != null) numberFont.Dispose();
                numberFont = new Font(Font.FontFamily, Math.Max(7f, Font.Size - 1f));
                preferred = new Size[values.Length];
                int maxTile = Math.Max(1, Math.Min((int)(390 * scale), maxWidth - 2 * gap - scroll.Width));
                int minTile = Math.Min(maxTile, (int)(104 * scale));
                double totalArea = 0;
                int widest = 0;
                for (int i = 0; i < values.Length; i++) {
                    int measured = TextRenderer.MeasureText(previews[i], Font, new Size(10000, 10000),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
                    int width = Math.Max(minTile, Math.Min(maxTile, measured + 2 * padding));
                    int textWidth = Math.Max(1, width - 2 * padding);
                    int height = TextRenderer.MeasureText(previews[i], Font, new Size(textWidth, 10000),
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                    height = Math.Max(Font.Height, Math.Min(Font.Height * 3, height));
                    preferred[i] = new Size(width, height + numberHeight + padding * 2);
                    totalArea += (width + gap) * (double)(preferred[i].Height + gap);
                    widest = Math.Max(widest, width);
                }
                // Grow towards a comfortable landscape aspect ratio as content grows.
                int ideal = (int)Math.Min(maxWidth, Math.Sqrt(totalArea * 1.9) + 2 * gap + scroll.Width);
                Reflow();
                return Math.Min(maxWidth, Math.Max(minWidth, Math.Max(ideal, widest + 2 * gap + scroll.Width)));
            }
            internal int MeasureHeight(int width)
            {
                return TileLayout.Create(preferred, Math.Max(1, width - 2 * gap - scroll.Width), gap).Height + 2 * gap;
            }
            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                Reflow();
            }
            private void Reflow()
            {
                if (preferred == null || updating) return;
                updating = true;
                try {
                    layout = TileLayout.Create(preferred, Math.Max(1, ClientSize.Width - 2 * gap - scroll.Width), gap);
                    scroll.Bounds = new Rectangle(Math.Max(0, ClientSize.Width - scroll.Width), 0, scroll.Width, ClientSize.Height);
                    int fullHeight = layout.Height + 2 * gap;
                    scroll.Visible = fullHeight > ClientSize.Height;
                    scroll.Minimum = 0;
                    scroll.Maximum = Math.Max(0, fullHeight - 1);
                    scroll.LargeChange = Math.Max(1, ClientSize.Height);
                    scroll.SmallChange = Math.Max(1, Font.Height * 3);
                    scroll.Value = Math.Min(scroll.Value, Math.Max(0, fullHeight - ClientSize.Height));
                } finally { updating = false; }
                SetHover(-1);
                Invalidate();
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (layout == null) return;
                int offset = scroll.Visible ? scroll.Value : 0;
                int top = offset - gap;
                for (int i = layout.FirstVisible(top); i < layout.Bounds.Length; i++) {
                    Rectangle r = layout.Bounds[i];
                    if (r.Top > offset + ClientSize.Height) break;
                    r.Offset(gap, gap - offset);
                    bool active = i == hover;
                    using (var background = new SolidBrush(active ? Color.FromArgb(219, 234, 254) : Color.White))
                        e.Graphics.FillRectangle(background, r);
                    using (var border = new Pen(active ? Color.FromArgb(59, 130, 246) : Color.FromArgb(218, 225, 235)))
                        e.Graphics.DrawRectangle(border, r.X, r.Y, r.Width - 1, r.Height - 1);
                    Rectangle number = new Rectangle(r.X + padding, r.Y + padding / 2, Math.Max(1, r.Width - 2 * padding), numberHeight);
                    TextRenderer.DrawText(e.Graphics, (i + 1).ToString(), numberFont, number, Color.SlateGray,
                        TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                    Rectangle body = new Rectangle(r.X + padding, r.Y + numberHeight + padding,
                        Math.Max(1, r.Width - 2 * padding), Math.Max(1, r.Height - numberHeight - 2 * padding));
                    TextRenderer.DrawText(e.Graphics, previews[i], Font, body,
                        active ? Color.FromArgb(29, 78, 216) : Color.FromArgb(30, 41, 59),
                        TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }
            private int Hit(Point p)
            {
                if (layout == null || !ClientRectangle.Contains(p) || p.X >= ClientSize.Width - scroll.Width) return -1;
                return layout.HitTest(new Point(p.X - gap, p.Y - gap + (scroll.Visible ? scroll.Value : 0)));
            }
            private void SetHover(int index)
            {
                if (index == hover) return;
                hover = index;
                tip.SetToolTip(this, index < 0 ? "" : values[index].Substring(0, Math.Min(3000, values[index].Length)));
                Invalidate();
            }
            protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); SetHover(Hit(e.Location)); }
            protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); SetHover(-1); }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                pressedButton = e.Button;
                pressed = e.Button == MouseButtons.Left || e.Button == MouseButtons.Right ? Hit(e.Location) : -1;
            }
            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                int index = Hit(e.Location), down = pressed;
                pressed = -1;
                if (index < 0 || index != down || e.Button != pressedButton) return;
                if (e.Button == MouseButtons.Left && Chosen != null) Chosen(index);
                else if (e.Button == MouseButtons.Right && RemoveRequested != null) RemoveRequested(index);
            }
            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                if (!scroll.Visible || SystemInformation.MouseWheelScrollLines == 0) return;
                int step = SystemInformation.MouseWheelScrollLines < 0 ? scroll.LargeChange :
                    Font.Height * SystemInformation.MouseWheelScrollLines;
                int change = (int)((long)e.Delta * step / 120);
                int max = Math.Max(0, scroll.Maximum - scroll.LargeChange + 1);
                scroll.Value = Math.Max(0, Math.Min(max, scroll.Value - change));
                SetHover(Hit(PointToClient(Cursor.Position)));
            }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
                base.WndProc(ref m);
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) { tip.Dispose(); if (numberFont != null) numberFont.Dispose(); }
                base.Dispose(disposing);
            }
            private sealed class PassiveScrollBar : VScrollBar
            {
                internal PassiveScrollBar() { SetStyle(ControlStyles.Selectable, false); TabStop = false; }
                protected override void WndProc(ref Message m)
                {
                    if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
                    base.WndProc(ref m);
                }
            }
        }
    }
}

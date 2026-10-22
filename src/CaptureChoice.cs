using System.Drawing;
using System.Windows.Forms;

namespace SuperCopy
{
    internal sealed class CaptureChoice : Form
    {
        internal CaptureChoice(int added, int existing, Point anchor)
        {
            Text = "SuperCopy · New content";
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;
            Location = anchor;
            ClientSize = new Size(570, 180);
            ShowInTaskbar = false; TopMost = true;
            Controls.Add(new Label { Left = 20, Top = 20, Width = 530, Height = 65,
                Text = "Captured " + added + " new blocks. You currently have " + existing + " saved blocks.\nAppend the new blocks, or replace the entire saved set?" });
            var append = new Button { Left = 20, Top = 108, Width = 170, Height = 40, Text = "Append to existing", DialogResult = DialogResult.Yes };
            var replace = new Button { Left = 202, Top = 108, Width = 170, Height = 40, Text = "Replace all", DialogResult = DialogResult.No };
            var cancel = new Button { Left = 384, Top = 108, Width = 166, Height = 40, Text = "Cancel", DialogResult = DialogResult.Cancel };
            Controls.AddRange(new Control[] { append, replace, cancel });
            AcceptButton = append; CancelButton = cancel;
            Load += delegate {
                // Fit after WinForms has applied font/DPI scaling. Re-evaluate the
                // screen in case the monitor configuration changed during copying.
                Rectangle area = Screen.FromPoint(anchor).WorkingArea;
                Size contentSize = ClientSize;
                Rectangle placement = WindowPlacement.NearAnchor(anchor, Size, area, 16);
                if (placement.Size != Size) {
                    AutoScroll = true;
                    AutoScrollMinSize = contentSize;
                }
                Bounds = placement;
            };
        }
    }
}

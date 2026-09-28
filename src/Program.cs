using System;
using System.ComponentModel;
using System.Globalization;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SuperCopy
{
    internal static class Program
    {
        [STAThread] private static void Main()
        {
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            bool created;
            using (var mutex = new Mutex(true, @"Local\SuperCopy.ClipboardBlocks", out created))
            {
                if (!created) { MessageBox.Show("SuperCopy is already running. Double-click its system tray icon to open Settings.", "SuperCopy"); return; }
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { using (var main = new MainForm()) Application.Run(main); }
                catch (Exception ex) { MessageBox.Show("SuperCopy could not start: " + ex.Message, "SuperCopy", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly BlockStore store = new BlockStore();
        private readonly NotifyIcon tray;
        private readonly Label status;
        private readonly Label binding;
        private readonly Button learnButton;
        private readonly Native.MouseProc hookProc;
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private MouseListener mouseListener;
        private bool alternateRegistered, escapeRegistered, disposed;
        private IntPtr popupTarget;
        private volatile MouseButtons trigger = MouseButtons.None;
        private MouseButtons swallowed = MouseButtons.None;
        private volatile bool learning, exiting;
        private bool registered, sent;
        private DateTime learnDeadline, captureDeadline, writeDeadline;
        private IntPtr sourceWindow;
        private uint sequence;
        private bool capturing;
        private string pendingWrite;
        private IntPtr pasteTarget;
        private bool pendingPaste;
        private DateTime pasteNotBefore, pasteDeadline;
        private uint pasteSequence;
        private BlockPopup popup;

        internal MainForm()
        {
            Text = "SuperCopy 1.4 · Clipboard Blocks";
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(650, 415);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            var intro = new Label { Left = 24, Top = 20, Width = 602, Height = 112,
                Text = "1. Press a middle, right, or side mouse button to bind it.\n2. Select text or Excel cells, then press and release Ctrl+Shift+C.\n3. Press your bound button, hover over a block, and click to paste.\n4. Open the panel again to reuse the same saved blocks." };
            binding = new Label { Left = 24, Top = 137, Width = 602, Height = 52, ForeColor = Color.FromArgb(29, 78, 216) };
            var note = new Label { Left = 24, Top = 195, Width = 602, Height = 68, ForeColor = Color.DimGray,
                Text = "Split on tabs and line breaks; keep spaces. Backup shortcut: Ctrl+Alt+F8.\nA side button is recommended. Its usual action is replaced while running.\nPress Esc to cancel binding. Blocks and the binding are cleared on exit." };
            learnButton = new Button { Left = 24, Top = 278, Width = 190, Height = 36, Text = "Rebind mouse button" };
            var hide = new Button { Left = 226, Top = 278, Width = 190, Height = 36, Text = "Run in background" };
            var quit = new Button { Left = 428, Top = 278, Width = 190, Height = 36, Text = "Exit SuperCopy" };
            status = new Label { Left = 24, Top = 329, Width = 602, Height = 68, Text = "No blocks captured yet." };
            Controls.AddRange(new Control[] { intro, binding, note, learnButton, hide, quit, status });
            learnButton.Click += delegate { BeginLearning(); };
            hide.Click += delegate { CancelLearning(); Hide(); };
            quit.Click += delegate { Exit(); };
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) CancelLearning(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Settings / Rebind button", null, delegate { Show(); WindowState = FormWindowState.Normal; Activate(); });
            menu.Items.Add("View saved blocks", null, delegate { ShowBlocks(Cursor.Position, IntPtr.Zero); });
            menu.Items.Add("Exit", null, delegate { Exit(); });
            tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "SuperCopy · No blocks captured", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { Show(); Activate(); };
            hookProc = MouseHook;
            timer.Interval = 40;
            timer.Tick += Tick;
            Load += delegate {
                registered = Native.RegisterHotKey(Handle, 1, 0x4000 | 0x0002 | 0x0004, 0x43);
                if (!registered) throw new InvalidOperationException("Ctrl+Shift+C is in use by another app. Free this shortcut and restart SuperCopy.");
                alternateRegistered = Native.RegisterHotKey(Handle, 2, 0x4000 | 0x0002 | 0x0001, 0x77);
                mouseListener = new MouseListener(hookProc);
                if (!alternateRegistered) status.Text = "Ctrl+Alt+F8 is in use by another app. The bound mouse button is still available.";
                timer.Start();
            };
            Shown += delegate { BeginLearning(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; CancelLearning(); Hide(); }
            };
        }
        private static string ButtonName(MouseButtons button)
        {
            switch (button) {
                case MouseButtons.Middle: return "Middle button";
                case MouseButtons.Right: return "Right button";
                case MouseButtons.XButton1: return "Side button 1 (Back)";
                case MouseButtons.XButton2: return "Side button 2 (Forward)";
                default: return "Not bound";
            }
        }
        private void BeginLearning()
        {
            if (popup != null) popup.Close();
            learning = true;
            learnDeadline = DateTime.UtcNow.AddSeconds(30);
            binding.Text = "Listening: press the middle, right, or side button you want to use.\nYou have 30 seconds. Press Esc to cancel.";
            learnButton.Enabled = false;
        }
        private void CancelLearning()
        {
            learning = false;
            learnButton.Enabled = true;
            binding.Text = "Panel button: " + ButtonName(trigger);
        }
        private void QueueUi(Action action)
        {
            if (exiting) return;
            try { BeginInvoke((Action)delegate { if (!exiting && !IsDisposed) action(); }); }
            catch (InvalidOperationException) { }
        }
        private void CloseOutside(Point point)
        {
            if (popup != null && !popup.Bounds.Contains(point)) popup.Close();
        }
        private IntPtr MouseHook(int code, IntPtr message, IntPtr data)
        {
            if (exiting || code < 0) return Native.CallNextHookEx(IntPtr.Zero, code, message, data);
            int msg = message.ToInt32();
            bool down = msg == 0x204 || msg == 0x207 || msg == 0x20B;
            bool up = msg == 0x205 || msg == 0x208 || msg == 0x20C;
            if (!down && !up && msg != 0x201) return Native.CallNextHookEx(IntPtr.Zero, code, message, data);
            var info = (Native.MouseInfo)Marshal.PtrToStructure(data, typeof(Native.MouseInfo));
            // Accept driver-generated/remapped buttons too. SuperCopy does not inject mouse events.
            Point eventPoint = new Point(info.Point.X, info.Point.Y);
            if (msg == 0x201) {
                QueueUi(delegate { CloseOutside(eventPoint); });
                return Native.CallNextHookEx(IntPtr.Zero, code, message, data);
            }
            MouseButtons button = (msg == 0x204 || msg == 0x205) ? MouseButtons.Right :
                (msg == 0x207 || msg == 0x208) ? MouseButtons.Middle :
                ((info.Data >> 16) == 1 ? MouseButtons.XButton1 : MouseButtons.XButton2);
            if (up && (swallowed & button) != 0) { swallowed &= ~button; return new IntPtr(1); }
            if (down && learning)
            {
                trigger = button;
                learning = false;
                swallowed |= button;
                QueueUi(delegate {
                    CancelLearning(); Hide();
                    Notify("Bound: " + ButtonName(trigger) + ". Select content and press Ctrl+Shift+C to capture it.");
                });
                return new IntPtr(1);
            }
            if (down && button == trigger)
            {
                swallowed |= button;
                Point position = new Point(info.Point.X, info.Point.Y);
                IntPtr target = Native.GetForegroundWindow();
                QueueUi(delegate { ShowBlocks(position, target); });
                return new IntPtr(1);
            }
            if (down) QueueUi(delegate { CloseOutside(eventPoint); });
            return Native.CallNextHookEx(IntPtr.Zero, code, message, data);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 3) {
                if (popup != null) popup.Close();
                return;
            }
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 2) {
                ShowBlocks(Cursor.Position, Native.GetForegroundWindow());
                return;
            }
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 1)
            {
                if (capturing || pendingWrite != null || pendingPaste) { Notify("The clipboard is busy. Please try again in a moment."); return; }
                if (popup != null || Native.GetForegroundWindow() == Handle) { Notify("First select the content to copy in Excel or another app."); return; }
                capturing = true; sent = false;
                sourceWindow = Native.GetForegroundWindow();
                captureDeadline = DateTime.UtcNow.AddSeconds(5);
                return;
            }
            base.WndProc(ref m);
        }
        private void Tick(object sender, EventArgs e)
        {
            if (learning && (DateTime.UtcNow >= learnDeadline || (Native.GetAsyncKeyState(0x1B) & 0x8000) != 0)) CancelLearning();
            if (popup != null && popupTarget != IntPtr.Zero && Native.GetForegroundWindow() != popupTarget) popup.Close();
            if (pendingWrite != null) {
                try {
                    Clipboard.SetText(pendingWrite, TextDataFormat.UnicodeText);
                    pendingWrite = null;
                    if (pasteTarget == IntPtr.Zero || !Native.IsWindow(pasteTarget)) {
                        Notify("Copied. To paste automatically, place the caret in your target app and open the panel with your bound mouse button.");
                    } else {
                        pasteSequence = Native.GetClipboardSequenceNumber();
                        pendingPaste = true;
                        pasteNotBefore = DateTime.UtcNow.AddMilliseconds(120);
                        pasteDeadline = DateTime.UtcNow.AddSeconds(2);
                    }
                } catch (ExternalException) {
                    if (DateTime.UtcNow >= writeDeadline) { pendingWrite = null; Notify("Another app is using the clipboard. Please select the block again."); }
                }
                return;
            }
            if (pendingPaste) {
                if (!Native.IsWindow(pasteTarget) || Native.GetForegroundWindow() != pasteTarget) {
                    pendingPaste = false;
                    Notify("The target window is unavailable or focus has changed. The block was copied; press Ctrl+V in your target app.");
                    return;
                }
                if (Native.GetClipboardSequenceNumber() != pasteSequence) {
                    pendingPaste = false;
                    Notify("The clipboard changed before pasting. Paste canceled; please select the block again.");
                    return;
                }
                if (DateTime.UtcNow >= pasteDeadline) {
                    pendingPaste = false;
                    Notify("Timed out waiting for buttons to be released. The block was copied; release the buttons, then press Ctrl+V.");
                    return;
                }
                if (DateTime.UtcNow < pasteNotBefore || !Native.PasteInputReleased()) return;
                pendingPaste = false;
                if (Native.PasteSelection())
                    status.Text = "Paste sent to the target window. All " + store.Items.Length + " saved blocks are unchanged.";
                else Notify("Could not send the paste shortcut. The block was copied; use Ctrl+V. Check that both apps run with the same privilege level.");
                return;
            }
            if (!capturing) return;
            if (Native.GetForegroundWindow() != sourceWindow) { FailCapture("The active window changed during capture. Return to your selection and press Ctrl+Shift+C again."); return; }
            if (DateTime.UtcNow >= captureDeadline) { FailCapture("No new text was copied. Check your selection and try again. Saved blocks are unchanged."); return; }
            if (!sent) {
                if (!Native.ModifiersReleased()) return;
                sequence = Native.GetClipboardSequenceNumber();
                if (!Native.CopySelection()) { FailCapture("Could not send the copy shortcut. If the target app runs as administrator, run SuperCopy with the same privileges."); return; }
                sent = true;
                captureDeadline = DateTime.UtcNow.AddSeconds(3);
                return;
            }
            if (Native.GetClipboardSequenceNumber() == sequence) return;
            try {
                // Do not replace saved blocks unless a new, valid text copy has completed.
                if (!Clipboard.ContainsText(TextDataFormat.UnicodeText)) return;
                string text = Clipboard.GetText(TextDataFormat.UnicodeText);
                store.Replace(text);
                capturing = false;
                tray.Text = "SuperCopy · " + store.Items.Length + " blocks";
                Notify("Captured " + store.Items.Length + " blocks; open the panel with " + ButtonName(trigger) + ".");
            } catch (ExternalException) { /* Clipboard lock is retried by the timer. */ }
              catch (ArgumentException ex) { FailCapture(ex.Message); }
        }
        private void FailCapture(string message) { capturing = false; Notify(message); }
        private void ShowBlocks(Point position, IntPtr target)
        {
            if (learning) return;
            if (capturing || pendingWrite != null || pendingPaste) { Notify("The clipboard is busy. Please open the panel again in a moment."); return; }
            if (popup != null) { popup.Close(); return; }
            if (store.Items.Length == 0) { Notify("Select content in Excel or another app, then press Ctrl+Shift+C to capture blocks."); return; }
            var window = new BlockPopup(store.Items, position);
            popup = window;
            window.Selected += delegate(string text) {
                // The popup never takes focus from Excel, including in-cell editing.
                // Do not activate a stale target if the user switched applications.
                pasteTarget = target == Handle ? IntPtr.Zero : target;
                pendingWrite = text;
                writeDeadline = DateTime.UtcNow.AddSeconds(2);
            };
            popupTarget = target;
            escapeRegistered = Native.RegisterHotKey(Handle, 3, 0x4000, 0x1B);
            window.SetEscapeAvailable(escapeRegistered);
            window.FormClosed += delegate {
                if (popup == window) {
                    popup = null;
                    if (escapeRegistered) Native.UnregisterHotKey(Handle, 3);
                    escapeRegistered = false;
                }
            };
            window.Show();
        }
        private void Notify(string message)
        {
            status.Text = message;
            tray.ShowBalloonTip(2500, "SuperCopy", message, ToolTipIcon.Info);
        }
        private void Exit() { exiting = true; Close(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed) {
                disposed = true;
                exiting = true;
                timer.Stop(); timer.Dispose();
                if (mouseListener != null) mouseListener.Dispose();
                if (alternateRegistered) Native.UnregisterHotKey(Handle, 2);
                if (registered) { Native.UnregisterHotKey(Handle, 1); registered = false; }
                if (popup != null) popup.Close();
                tray.Visible = false;
                tray.ContextMenuStrip.Dispose(); tray.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

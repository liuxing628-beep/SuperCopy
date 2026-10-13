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
        private bool alternateRegistered, escapeRegistered, disposed, choosing, setupComplete;
        private Hotkey captureHotkey, panelHotkey;
        private readonly ShortcutBox captureBox = new ShortcutBox();
        private readonly ShortcutBox panelBox = new ShortcutBox();
        private readonly Button panelApply = new Button();
        private sealed class PopupRegion { internal Rectangle Bounds; }
        private volatile PopupRegion popupRegion;
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
            Text = "SuperCopy 1.6 · Clipboard Blocks";
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(680, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Controls.Add(new Label { Left = 24, Top = 18, Width = 632, Height = 36,
                Text = "Set up your capture shortcut first, then choose how to open the panel." });
            Controls.Add(new Label { Left = 24, Top = 60, Width = 632, Height = 26, Text = "Step 1 — Super Copy shortcut" });
            captureBox.SetBounds(24, 92, 414, 30);
            var captureApply = new Button { Left = 452, Top = 87, Width = 200, Height = 36, Text = "Use capture shortcut" };
            Controls.Add(new Label { Left = 24, Top = 143, Width = 632, Height = 26, Text = "Step 2 — Open panel shortcut (keyboard or mouse)" });
            panelBox.SetBounds(24, 175, 414, 30); panelBox.Enabled = false;
            panelApply.SetBounds(452, 170, 200, 36); panelApply.Text = "Use panel shortcut"; panelApply.Enabled = false;
            learnButton = new Button { Left = 24, Top = 217, Width = 246, Height = 36, Text = "Or bind a mouse button", Enabled = false };
            binding = new Label { Left = 24, Top = 270, Width = 632, Height = 55, ForeColor = Color.FromArgb(29, 78, 216) };
            var note = new Label { Left = 24, Top = 329, Width = 632, Height = 65, ForeColor = Color.DimGray,
                Text = "Use Ctrl or Alt with a key (Shift is optional). Shortcuts apply until you exit.\nCapture asks whether to append or replace. Tabs and line breaks split blocks.\nClick to paste; hold and drag to reorder; right-click to remove a block." };
            var hide = new Button { Left = 24, Top = 406, Width = 200, Height = 36, Text = "Run in background" };
            var quit = new Button { Left = 242, Top = 406, Width = 200, Height = 36, Text = "Exit SuperCopy" };
            status = new Label { Left = 24, Top = 455, Width = 632, Height = 54, Text = "Step 1: click the capture field and press your preferred shortcut." };
            Controls.AddRange(new Control[] { captureBox, captureApply, panelBox, panelApply, binding, note, learnButton, hide, quit, status });
            captureBox.ValidationMessage += delegate(string message) { status.Text = message; };
            panelBox.ValidationMessage += delegate(string message) { status.Text = message; };
            captureApply.Click += delegate { UseCaptureShortcut(); };
            panelApply.Click += delegate { UsePanelShortcut(); };
            learnButton.Click += delegate { BeginLearning(); };
            hide.Click += delegate {
                CancelLearning();
                if (!setupComplete) { status.Text = "Complete both setup steps before running in background."; return; }
                Hide();
            };
            quit.Click += delegate { Exit(); };
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) CancelLearning(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Settings / Change shortcuts", null, delegate { Show(); WindowState = FormWindowState.Normal; Activate(); });
            menu.Items.Add("View saved blocks", null, delegate { ShowBlocks(Cursor.Position, IntPtr.Zero); });
            menu.Items.Add("Exit", null, delegate { Exit(); });
            tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "SuperCopy · No blocks captured", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { Show(); Activate(); };
            hookProc = MouseHook;
            timer.Interval = 40;
            timer.Tick += Tick;
            Load += delegate {
                mouseListener = new MouseListener(hookProc);
                timer.Start();
                UpdateBinding();
            };
            Shown += delegate { captureBox.Focus(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; CancelLearning();
                    if (setupComplete) Hide(); else status.Text = "Complete both setup steps, or click Exit SuperCopy."; }
            };
        }
        private bool InstallShortcut(int id, Hotkey candidate, Hotkey previous)
        {
            if (candidate == null) { status.Text = "Click the shortcut field and press a key combination first."; return false; }
            Hotkey other = id == 1 ? panelHotkey : captureHotkey;
            if (candidate.SameAs(other)) { status.Text = "Capture and panel shortcuts must be different."; return false; }
            if (previous != null) Native.UnregisterHotKey(Handle, id);
            if (Native.RegisterHotKey(Handle, id, 0x4000 | candidate.Modifiers, candidate.Key)) return true;
            if (previous != null && !Native.RegisterHotKey(Handle, id, 0x4000 | previous.Modifiers, previous.Key)) {
                if (id == 1) { captureHotkey = null; registered = false; setupComplete = false; }
                else { panelHotkey = null; alternateRegistered = false; setupComplete = trigger != MouseButtons.None; }
                UpdateBinding();
            }
            status.Text = "That shortcut is unavailable or already used by another app. Choose another combination.";
            return false;
        }
        private void UseCaptureShortcut()
        {
            if (capturing || choosing || pendingWrite != null || pendingPaste) { status.Text = "Finish the current clipboard action first."; return; }
            CancelLearning();
            if (popup != null) popup.Close();
            if (!InstallShortcut(1, captureBox.Candidate, captureHotkey)) return;
            captureHotkey = captureBox.Candidate; registered = true;
            panelBox.Enabled = panelApply.Enabled = learnButton.Enabled = true;
            UpdateBinding();
            status.Text = "Capture shortcut saved. Step 2: choose a keyboard shortcut or bind a mouse button.";
            panelBox.Focus();
        }
        private void UsePanelShortcut()
        {
            if (captureHotkey == null) return;
            if (capturing || choosing || pendingWrite != null || pendingPaste) { status.Text = "Finish the current clipboard action first."; return; }
            CancelLearning();
            if (popup != null) popup.Close();
            if (!InstallShortcut(2, panelBox.Candidate, panelHotkey)) return;
            panelHotkey = panelBox.Candidate; alternateRegistered = true;
            trigger = MouseButtons.None; setupComplete = true;
            UpdateBinding(); Hide();
            Notify("Ready. Capture: " + ShortcutBox.DisplayName(captureHotkey) + "; open panel: " + ShortcutBox.DisplayName(panelHotkey) + ".");
        }
        private string PanelName() { return panelHotkey != null ? ShortcutBox.DisplayName(panelHotkey) : ButtonName(trigger); }
        private void UpdateBinding()
        {
            binding.Text = "Super Copy: " + ShortcutBox.DisplayName(captureHotkey) + "\nOpen panel: " + PanelName();
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
            if (captureHotkey == null || capturing || choosing || pendingWrite != null || pendingPaste) return;
            if (popup != null) popup.Close();
            learning = true;
            learnDeadline = DateTime.UtcNow.AddSeconds(30);
            binding.Text = "Listening: press the middle, right, or side button you want to use.\nYou have 30 seconds. Press Esc to cancel.";
            learnButton.Enabled = false;
        }
        private void CancelLearning()
        {
            learning = false;
            learnButton.Enabled = captureHotkey != null;
            UpdateBinding();
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
                    if (alternateRegistered) Native.UnregisterHotKey(Handle, 2);
                    alternateRegistered = false; panelHotkey = null; setupComplete = true;
                    CancelLearning(); Hide();
                    Notify("Ready. Capture: " + ShortcutBox.DisplayName(captureHotkey) + "; open panel: " + ButtonName(trigger) + ".");
                });
                return new IntPtr(1);
            }
            // When right-button activation is selected, right-clicks inside the
            // panel must reach the tile canvas for removal rather than toggle it.
            PopupRegion region = popupRegion;
            if (button == MouseButtons.Right && region != null && region.Bounds.Contains(eventPoint))
                return Native.CallNextHookEx(IntPtr.Zero, code, message, data);
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
            if (m.Msg == 0x0312 && (m.WParam.ToInt32() == 1 || m.WParam.ToInt32() == 2) && Visible && ContainsFocus) {
                var box = ActiveControl as ShortcutBox;
                if (box != null) {
                    box.Candidate = m.WParam.ToInt32() == 1 ? captureHotkey : panelHotkey;
                    box.Text = ShortcutBox.DisplayName(box.Candidate);
                }
                status.Text = "Shortcut recorded. Click the corresponding Use shortcut button.";
                return;
            }
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
                if (choosing || capturing || pendingWrite != null || pendingPaste) { Notify("The clipboard is busy. Please try again in a moment."); return; }
                if (!setupComplete || captureHotkey == null) { Notify("Complete both shortcut setup steps first."); return; }
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
                        Notify("Copied. To paste automatically, place the caret in your target app and open the panel with your configured panel shortcut.");
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
            if (Native.GetForegroundWindow() != sourceWindow) { FailCapture("The active window changed during capture. Return to your selection and use your Super Copy shortcut again."); return; }
            if (DateTime.UtcNow >= captureDeadline) { FailCapture("No new text was copied. Check your selection and try again. Saved blocks are unchanged."); return; }
            if (!sent) {
                if (!Native.CaptureInputReleased(captureHotkey.Key)) return;
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
                string[] incoming = BlockStore.Parse(text);
                capturing = false;
                choosing = true;
                try {
                    using (var choice = new CaptureChoice(incoming.Length, store.Items.Length)) {
                        DialogResult result = choice.ShowDialog(this);
                        if (result == DialogResult.Yes || result == DialogResult.No) {
                            store.Commit(incoming, result == DialogResult.Yes);
                            UpdateTray();
                            Notify((result == DialogResult.Yes ? "Appended " : "Replaced with ") + incoming.Length +
                                " blocks. Saved total: " + store.Items.Length + ". Open with " + PanelName() + ".");
                        } else status.Text = "Capture canceled. Saved blocks are unchanged.";
                    }
                } finally {
                    choosing = false;
                    if (Native.IsWindow(sourceWindow)) Native.SetForegroundWindow(sourceWindow);
                }
            } catch (ExternalException) { /* Clipboard lock is retried by the timer. */ }
              catch (ArgumentException ex) { FailCapture(ex.Message); }
        }
        private void FailCapture(string message) { capturing = false; Notify(message); }
        private void ShowBlocks(Point position, IntPtr target)
        {
            if (learning || choosing) return;
            if (!setupComplete) { Notify("Complete both shortcut setup steps first."); return; }
            if (capturing || pendingWrite != null || pendingPaste) { Notify("The clipboard is busy. Please open the panel again in a moment."); return; }
            if (popup != null) { popup.Close(); return; }
            if (store.Items.Length == 0) { Notify("Select content in Excel or another app, then use " + ShortcutBox.DisplayName(captureHotkey) + " to capture blocks."); return; }
            var window = new BlockPopup(store.Items, position);
            popup = window;
            window.Selected += delegate(string text) {
                // The popup never takes focus from Excel, including in-cell editing.
                // Do not activate a stale target if the user switched applications.
                pasteTarget = target == Handle ? IntPtr.Zero : target;
                pendingWrite = text;
                writeDeadline = DateTime.UtcNow.AddSeconds(2);
            };
            window.Removed += delegate(int index) {
                store.RemoveAt(index);
                UpdateTray();
                status.Text = "Removed one block. " + store.Items.Length + " blocks remain.";
                if (store.Items.Length == 0) window.Close(); else window.UpdateItems(store.Items);
            };
            window.Reordered += delegate(int source, int insertion) {
                store.Move(source, insertion);
                window.UpdateItems(store.Items);
                status.Text = "Block order updated. The remaining blocks have been rearranged.";
            };
            window.LocationChanged += delegate { popupRegion = new PopupRegion { Bounds = window.Bounds }; };
            window.SizeChanged += delegate { popupRegion = new PopupRegion { Bounds = window.Bounds }; };
            popupTarget = target;
            escapeRegistered = Native.RegisterHotKey(Handle, 3, 0x4000, 0x1B);
            window.SetEscapeAvailable(escapeRegistered);
            window.FormClosed += delegate {
                if (popup == window) {
                    popup = null; popupRegion = null;
                    if (escapeRegistered) Native.UnregisterHotKey(Handle, 3);
                    escapeRegistered = false;
                }
            };
            window.Show();
            popupRegion = new PopupRegion { Bounds = window.Bounds };
        }
        private void UpdateTray() { tray.Text = "SuperCopy · " + store.Items.Length + " blocks"; }
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

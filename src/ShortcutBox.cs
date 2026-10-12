using System;
using System.Windows.Forms;

namespace SuperCopy
{
    internal sealed class ShortcutBox : TextBox
    {
        internal Hotkey Candidate;
        internal event Action<string> ValidationMessage;
        internal ShortcutBox() { ReadOnly = true; Text = "Click here, then press a shortcut"; }
        internal static string DisplayName(Hotkey key)
        {
            if (key == null) return "Not set";
            string result = ((key.Modifiers & 2) != 0 ? "Ctrl+" : "") +
                ((key.Modifiers & 1) != 0 ? "Alt+" : "") + ((key.Modifiers & 4) != 0 ? "Shift+" : "");
            string name = ((Keys)key.Key).ToString();
            if (name.Length == 2 && name[0] == 'D' && Char.IsDigit(name[1])) name = name.Substring(1);
            return result + name;
        }
        protected override bool ProcessCmdKey(ref Message message, Keys data)
        {
            uint key = (uint)(data & Keys.KeyCode);
            if (key == 0x1B) { Candidate = null; Text = "Click here, then press a shortcut"; return true; }
            if (key == 0x10 || key == 0x11 || key == 0x12 || key == 0x5B || key == 0x5C) return true;
            uint modifiers = ((data & Keys.Control) != 0 ? 2u : 0u) |
                ((data & Keys.Alt) != 0 ? 1u : 0u) | ((data & Keys.Shift) != 0 ? 4u : 0u);
            string error;
            if (!Hotkey.IsValid(modifiers, key, out error)) {
                if (ValidationMessage != null) ValidationMessage(error);
                return true;
            }
            Candidate = new Hotkey(modifiers, key);
            Text = DisplayName(Candidate);
            if (ValidationMessage != null) ValidationMessage("Click Use shortcut to apply " + Text + ".");
            return true;
        }
    }
}

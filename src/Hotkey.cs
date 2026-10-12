using System;

namespace SuperCopy
{
    internal sealed class Hotkey
    {
        internal readonly uint Modifiers, Key;
        internal Hotkey(uint modifiers, uint key) { Modifiers = modifiers; Key = key; }
        internal bool SameAs(Hotkey other) { return other != null && Modifiers == other.Modifiers && Key == other.Key; }
        internal static bool IsValid(uint modifiers, uint key, out string error)
        {
            error = null;
            if ((modifiers & ~7u) != 0 || (modifiers & 3u) == 0) {
                error = "Use Ctrl or Alt with a key; Shift is optional."; return false;
            }
            if (key < 0x20 || key > 0xFE || key == 0x5B || key == 0x5C || key == 0x5D) {
                error = "Choose a letter, number, function key, or punctuation key."; return false;
            }
            if (modifiers == 2 && (key == 0x43 || key == 0x56)) {
                error = "Ctrl+C and Ctrl+V are reserved for copying and pasting."; return false;
            }
            if (modifiers == 1 && key == 0x73) {
                error = "Alt+F4 is reserved for closing windows."; return false;
            }
            return true;
        }
    }
}

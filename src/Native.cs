using System;
using System.Runtime.InteropServices;

namespace SuperCopy
{
    internal static class Native
    {
        internal delegate IntPtr MouseProc(int code, IntPtr message, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct MouseInfo
        { public Point Point; public uint Data, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct MouseInput
        { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput
        { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Explicit)] internal struct InputUnion
        { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
        [StructLayout(LayoutKind.Sequential)] internal struct Input
        { public uint Type; public InputUnion Value; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int id, MouseProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
        internal static bool CopySelection() { return SendControlKey(0x43); }
        internal static bool PasteSelection() { return SendControlKey(0x56); }
        private static bool SendControlKey(ushort key)
        {
            Input[] inputs = new Input[4];
            ushort[] keys = { 0x11, key, key, 0x11 };
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i].Type = 1;
                inputs[i].Value.Keyboard.Key = keys[i];
                inputs[i].Value.Keyboard.Flags = i >= 2 ? 2u : 0u;
            }
            return SendInput(4, inputs, Marshal.SizeOf(typeof(Input))) == 4;
        }
        internal static bool PasteInputReleased()
        {
            int[] keys = { 0x01, 0x02, 0x04, 0x05, 0x06, 0x0D, 0x11, 0x10, 0x12, 0x5B, 0x5C };
            foreach (int key in keys) if ((GetAsyncKeyState(key) & 0x8000) != 0) return false;
            return true;
        }
        internal static bool ModifiersReleased()
        {
            return (GetAsyncKeyState(0x11) & 0x8000) == 0 &&
                (GetAsyncKeyState(0x10) & 0x8000) == 0 &&
                (GetAsyncKeyState(0x43) & 0x8000) == 0 &&
                (GetAsyncKeyState(0x12) & 0x8000) == 0;
        }
    }
}

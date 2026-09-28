using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SuperCopy
{
    // Excel may block the STA UI thread while rendering delayed clipboard data.
    // Keep the low-level hook's message pump independent of all clipboard/UI work.
    internal sealed class MouseListener : IDisposable
    {
        private readonly Native.MouseProc callback;
        private readonly Thread thread;
        private readonly ManualResetEvent ready = new ManualResetEvent(false);
        private Control dispatcher;
        private Exception failure;
        private bool disposed;
        internal MouseListener(Native.MouseProc callback)
        {
            this.callback = callback;
            thread = new Thread(Run) { IsBackground = true, Name = "SuperCopy mouse listener" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.WaitOne();
            if (failure != null) { ready.Dispose(); throw failure; }
        }
        private void Run()
        {
            IntPtr hook = IntPtr.Zero;
            try {
                using (var control = new Control()) {
                    dispatcher = control;
                    IntPtr handle = control.Handle;
                    hook = Native.SetWindowsHookEx(14, callback, Native.GetModuleHandle(null), 0);
                    if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not start the mouse listener");
                    ready.Set();
                    Application.Run();
                }
            } catch (Exception ex) { failure = ex; ready.Set(); }
            finally { if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook); }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (thread.IsAlive && dispatcher != null) {
                try { dispatcher.BeginInvoke((Action)Application.ExitThread); }
                catch (InvalidOperationException) { }
                thread.Join(1500);
            }
            if (!thread.IsAlive) ready.Dispose();
        }
    }
}

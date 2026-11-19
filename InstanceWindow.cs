using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace CursorSeam
{
    internal static class InstanceWindow
    {
        private const string WindowProperty = "CursorSeam.MainWindow.{6027102e-56ca-4b76-9c99-748dd6b85261}";
        internal static readonly int RestoreMessage = RegisterWindowMessage(WindowProperty + ".Restore");

        private delegate bool WindowCallback(IntPtr window, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetProp(IntPtr window, string name, IntPtr value);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr RemoveProp(IntPtr window, string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetProp(IntPtr window, string name);
        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr data);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint processId);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);

        internal static void Mark(IntPtr window) { SetProp(window, WindowProperty, new IntPtr(1)); }
        internal static void Unmark(IntPtr window) { RemoveProp(window, WindowProperty); }

        internal static IntPtr Find()
        {
            IntPtr match = IntPtr.Zero;
            EnumWindows((window, _) =>
            {
                if (GetProp(window, WindowProperty) == IntPtr.Zero) return true;
                match = window;
                return false;
            }, IntPtr.Zero);
            return match;
        }

        internal static bool RequestRestore()
        {
            if (RestoreMessage == 0) return false;
            var wait = Stopwatch.StartNew();
            do
            {
                IntPtr window = Find();
                if (window != IntPtr.Zero)
                {
                    uint processId;
                    GetWindowThreadProcessId(window, out processId);
                    if (processId != 0) AllowSetForegroundWindow(processId);
                    if (PostMessage(window, RestoreMessage, IntPtr.Zero, IntPtr.Zero)) return true;
                }
                Thread.Sleep(40);
            } while (wait.ElapsedMilliseconds < 2000);
            return false;
        }
    }
}

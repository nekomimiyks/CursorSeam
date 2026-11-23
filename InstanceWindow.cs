using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace CursorSeam
{
    internal static class InstanceWindow
    {
        internal const string MutexName = @"Local\CursorSeam";
        internal const string CloseForUpdateArgument = "--close-for-update";
        internal const string SilentUpdateArgument = "--silent-update";
        private const string WindowProperty = "CursorSeam.MainWindow.{6027102e-56ca-4b76-9c99-748dd6b85261}";
        internal static readonly int RestoreMessage = RegisterWindowMessage(WindowProperty + ".Restore");
        internal static readonly int CloseForUpdateMessage = RegisterWindowMessage(WindowProperty + ".CloseForUpdate");

        private delegate bool WindowCallback(IntPtr window, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetProp(IntPtr window, string name, IntPtr value);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr RemoveProp(IntPtr window, string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetProp(IntPtr window, string name);
        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr data);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint processId);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr window, int message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);

        internal static void Mark(IntPtr window) { SetProp(window, WindowProperty, new IntPtr(2)); }
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
                    AllowForeground(window);
                    if (PostMessage(window, RestoreMessage, IntPtr.Zero, IntPtr.Zero)) return true;
                }
                Thread.Sleep(40);
            } while (wait.ElapsedMilliseconds < 2000);
            return false;
        }

        internal static bool RequestCloseForUpdate(bool interactive)
        {
            if (WaitForExit(0)) return true;
            IntPtr window = Find();
            if (window == IntPtr.Zero || GetProp(window, WindowProperty) != new IntPtr(2) || CloseForUpdateMessage == 0)
                return false;
            if (interactive) AllowForeground(window);
            const uint abortIfHung = 2, noTimeoutWhileResponsive = 8;
            IntPtr accepted;
            IntPtr sent = SendMessageTimeout(window, CloseForUpdateMessage, interactive ? new IntPtr(1) : IntPtr.Zero,
                IntPtr.Zero, abortIfHung | (interactive ? noTimeoutWhileResponsive : 0), 2500, out accepted);
            return sent != IntPtr.Zero && accepted == new IntPtr(1) && WaitForExit(5000);
        }

        private static void AllowForeground(IntPtr window)
        {
            uint processId;
            GetWindowThreadProcessId(window, out processId);
            if (processId != 0) AllowSetForegroundWindow(processId);
        }

        private static bool WaitForExit(int milliseconds)
        {
            Mutex mutex;
            if (!Mutex.TryOpenExisting(MutexName, out mutex)) return true;
            using (mutex)
            {
                try { if (!mutex.WaitOne(milliseconds)) return false; }
                catch (AbandonedMutexException) { }
                mutex.ReleaseMutex();
                return true;
            }
        }
    }
}

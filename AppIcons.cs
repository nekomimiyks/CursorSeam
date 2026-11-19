using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CursorSeam
{
    internal static class AppIcons
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        internal static Icon Load(int size)
        {
            using (var stream = typeof(AppIcons).Assembly.GetManifestResourceStream("CursorSeam.AppIcon.ico"))
                return new Icon(stream, size, size);
        }

        internal static int TraySize()
        {
            const int smallIconWidth = 49;
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            uint dpi = taskbar == IntPtr.Zero ? 0 : GetDpiForWindow(taskbar);
            int width = dpi == 0 ? 0 : GetSystemMetricsForDpi(smallIconWidth, dpi);
            return width > 0 ? width : SystemInformation.SmallIconSize.Width;
        }
    }
}

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CursorSeam
{
    internal static class BoardStyle
    {
        private enum Theme { Light, Dark, HighContrast }

        private static Theme theme = ReadTheme();
        internal static bool HighContrast { get { return theme == Theme.HighContrast; } }
        internal static Color Surface { get { return Choose(0xF8FAFA, 0x1E2322, SystemColors.Window); } }
        internal static Color Canvas { get { return Choose(0xE7EFF1, 0x242C2A, SystemColors.Window); } }
        internal static Color Ink { get { return Choose(0x17364C, 0xEAF1EF, SystemColors.WindowText); } }
        internal static Color Muted { get { return Choose(0x47616C, 0xB4C4BF, SystemColors.WindowText); } }
        internal static Color Accent { get { return Choose(0x107276, 0x82D2B7, SystemColors.WindowText); } }
        internal static Color PrimaryButton { get { return Choose(0x107276, 0x11795A, SystemColors.Highlight); } }
        internal static Color QuietButton { get { return Choose(0xE5EDEF, 0x303A37, SystemColors.Control); } }
        internal static Color DisabledButton { get { return Choose(0xEBF0F0, 0x262D2B, SystemColors.Control); } }
        internal static Color DisabledInk { get { return Choose(0x47616C, 0x91A19C, SystemColors.GrayText); } }
        internal static Color Field { get { return Choose(0xFFFFFF, 0x252B29, SystemColors.Window); } }
        internal static Color FieldBorder { get { return Choose(0x608681, 0x82B8A8, SystemColors.WindowText); } }
        internal static Color ToggleOn { get { return Choose(0x116B51, 0x11795A, SystemColors.Highlight); } }
        internal static Color ToggleOff { get { return Choose(0x52636C, 0x535F69, SystemColors.ControlDark); } }
        internal static Color Dots { get { return Choose(0xBBCDD4, 0x3C504A, SystemColors.WindowText); } }
        internal static Color DisplayFill { get { return Choose(0xE9F2F5, 0x303D39, SystemColors.Window); } }
        internal static Color SelectedDisplayFill { get { return Choose(0xD2EBE4, 0x315348, SystemColors.Window); } }
        internal static readonly Color RulerAccent = Color.FromArgb(103, 226, 195);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        private static Color Choose(int light, int dark, Color highContrast)
        {
            if (HighContrast) return highContrast;
            return Color.FromArgb(unchecked((int)0xFF000000) | (theme == Theme.Dark ? dark : light));
        }

        private static Theme ReadTheme()
        {
            if (SystemInformation.HighContrast) return Theme.HighContrast;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    if (key != null && object.Equals(key.GetValue("AppsUseLightTheme"), 0)) return Theme.Dark;
            }
            catch (SecurityException) { }
            catch (UnauthorizedAccessException) { }
            return Theme.Light;
        }

        internal static bool RefreshTheme()
        {
            Theme updated = ReadTheme();
            if (updated == theme) return false;
            theme = updated;
            return true;
        }

        internal static void ApplyTitleBar(IntPtr handle)
        {
            const int useImmersiveDarkMode = 20;
            int dark = theme == Theme.Dark ? 1 : 0;
            DwmSetWindowAttribute(handle, useImmersiveDarkMode, ref dark, sizeof(int));
        }

        internal static bool IsThemeChange(int message)
        {
            return message == 0x0015 || message == 0x001A || message == 0x031A; // System colors, settings or theme
        }

        internal static void StyleMenu(ContextMenuStrip menu)
        {
            if (theme == Theme.Dark) menu.Renderer = DarkMenuRenderer;
            else menu.RenderMode = ToolStripRenderMode.System;
            menu.BackColor = theme == Theme.Dark ? Field : SystemColors.Menu;
            foreach (ToolStripItem item in menu.Items) item.ForeColor = theme == Theme.Dark ? Ink : SystemColors.MenuText;
        }

        private static readonly ToolStripRenderer DarkMenuRenderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) { RoundedEdges = false };

        private sealed class DarkMenuColors : ProfessionalColorTable
        {
            internal DarkMenuColors() { UseSystemColors = false; }
            public override Color ToolStripDropDownBackground { get { return Field; } }
            public override Color MenuBorder { get { return FieldBorder; } }
            public override Color MenuItemBorder { get { return FieldBorder; } }
            public override Color MenuItemSelected { get { return QuietButton; } }
            public override Color CheckBackground { get { return QuietButton; } }
            public override Color CheckSelectedBackground { get { return QuietButton; } }
            public override Color ImageMarginGradientBegin { get { return Field; } }
            public override Color ImageMarginGradientMiddle { get { return Field; } }
            public override Color ImageMarginGradientEnd { get { return Field; } }
        }

        internal static Font CreateFont(float size, FontStyle style, GraphicsUnit unit)
        {
            using (Font system = SystemFonts.MessageBoxFont)
                return new Font(system.Name, size, style, unit);
        }

        internal static Size MeasureText(IDeviceContext graphics, string text, Font font, TextFormatFlags flags, int width = int.MaxValue)
        {
            var available = new Size(width, int.MaxValue);
            return graphics == null ? TextRenderer.MeasureText(text, font, available, flags) :
                TextRenderer.MeasureText(graphics, text, font, available, flags);
        }

        internal static GraphicsPath Rounded(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (diameter <= 0) return path;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class BoardButton : Button
    {
        private bool primary, hover, pressed;
        private readonly bool outlined;

        internal bool Primary
        {
            get { return primary; }
            set { if (primary != value) { primary = value; Invalidate(); } }
        }

        internal BoardButton(string textKey, bool primary, bool outlined = false)
        {
            this.primary = primary;
            this.outlined = outlined;
            Text = UiText.Get(textKey);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MinimumSize = new Size(92, 36);
            Margin = new Padding(3, 0, 3, 5);
            Padding = new Padding(8, 1, 8, 1);
            FlatStyle = FlatStyle.Flat;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // A buffered owner-drawn button must cover the pixels outside its rounded shape as well.
            using (var surface = new SolidBrush(Parent == null ? BoardStyle.Surface : Parent.BackColor))
                e.Graphics.FillRectangle(surface, ClientRectangle);
            if (BoardStyle.HighContrast) { base.OnPaint(e); return; }
            Color background = Enabled ? (Primary ? BoardStyle.PrimaryButton : outlined ? BoardStyle.Field : BoardStyle.QuietButton) : BoardStyle.DisabledButton;
            Color foreground = Enabled ? (Primary ? Color.White : BoardStyle.Ink) : BoardStyle.DisabledInk;
            if (Enabled && (hover || pressed)) background = ControlPaint.Dark(background, pressed ? 0.12f : 0.06f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = BoardStyle.Rounded(new RectangleF(1.5f, 1.5f, Width - 3, Height - 3), 8))
            using (var fill = new SolidBrush(background))
            {
                e.Graphics.FillPath(fill, shape);
                if (outlined)
                    using (var border = new Pen(BoardStyle.FieldBorder)) e.Graphics.DrawPath(border, shape);
            }
            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
            TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -8, 0), foreground, flags);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -4), foreground, background);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = e.Button == MouseButtons.Left; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) { pressed = false; Invalidate(); } }
    }
}

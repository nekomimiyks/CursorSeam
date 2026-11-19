using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CursorSeam
{
    internal static class BoardStyle
    {
        internal static readonly Color Surface = Color.FromArgb(248, 250, 250);
        internal static readonly Color Canvas = Color.FromArgb(231, 239, 241);
        internal static readonly Color Ink = Color.FromArgb(23, 54, 76);
        internal static readonly Color Muted = Color.FromArgb(71, 97, 108);
        internal static readonly Color Accent = Color.FromArgb(16, 114, 118);
        internal static readonly Color QuietButton = Color.FromArgb(229, 237, 239);
        internal static readonly Color RulerAccent = Color.FromArgb(103, 226, 195);

        internal static Font CreateFont(float size, FontStyle style, GraphicsUnit unit)
        {
            using (Font system = SystemFonts.MessageBoxFont)
                return new Font(system.Name, size, style, unit);
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

        internal bool Primary
        {
            get { return primary; }
            set { if (primary != value) { primary = value; Invalidate(); } }
        }

        internal BoardButton(string textKey, bool primary)
        {
            this.primary = primary;
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
            if (SystemInformation.HighContrast) { base.OnPaint(e); return; }
            Color background = Enabled ? (Primary ? BoardStyle.Accent : BoardStyle.QuietButton) : Color.FromArgb(235, 240, 240);
            Color foreground = Enabled ? (Primary ? Color.White : BoardStyle.Ink) : BoardStyle.Muted;
            if (Enabled && (hover || pressed)) background = ControlPaint.Dark(background, pressed ? 0.12f : 0.06f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = BoardStyle.Rounded(new RectangleF(1.5f, 1.5f, Width - 3, Height - 3), 8))
            using (var fill = new SolidBrush(background)) e.Graphics.FillPath(fill, shape);
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

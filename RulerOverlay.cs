using System;
using System.Drawing;
using System.Windows.Forms;

namespace CursorSeam
{
    internal sealed class RulerOverlay : Form
    {
        private Display display;

        internal RulerOverlay(Display display)
        {
            this.display = display;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = TransparencyKey = Color.Magenta;
            DoubleBuffered = true;
            Bounds = display.Pixels;
        }

        internal void UpdateDisplay(Display current)
        {
            if (display.Area.Size == current.Area.Size) return;
            display = current;
            Invalidate();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                // Layered, mouse-transparent, non-activating, and hidden from Alt+Tab.
                parameters.ExStyle |= 0x00080000 | 0x00000020 | 0x08000000 | 0x00000080;
                return parameters;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawRulers(e.Graphics, ClientSize, display.Area.Size);
        }

        internal static float TickPosition(int millimeters, int pixels, float totalMillimeters)
        {
            return millimeters * pixels / totalMillimeters;
        }

        internal static void DrawRulers(Graphics graphics, Size pixels, SizeF millimeters)
        {
            if (pixels.Width < 60 || pixels.Height < 60 || millimeters.IsEmpty) return;
            int stroke = Math.Max(1, (int)Math.Round(graphics.DpiX / 96));
            int inset = stroke + 2;
            using (var shadow = new Pen(Color.FromArgb(20, 27, 36), stroke + 2))
            using (var line = new Pen(BoardStyle.RulerAccent, stroke))
            using (var font = BoardStyle.CreateFont(10f * stroke, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                DrawAxis(graphics, true, pixels.Width, pixels.Height, millimeters.Width, inset, shadow, line, font);
                DrawAxis(graphics, false, pixels.Height, pixels.Width, millimeters.Height, inset, shadow, line, font);
            }
        }

        private static void DrawAxis(Graphics graphics, bool horizontal, int length, int breadth, float millimeters,
                                     int inset, Pen shadow, Pen line, Font font)
        {
            int near = inset, far = breadth - 1 - inset;
            DrawLine(graphics, horizontal, shadow, inset, near, length - 1 - inset, near);
            DrawLine(graphics, horizontal, shadow, inset, far, length - 1 - inset, far);
            DrawLine(graphics, horizontal, line, inset, near, length - 1 - inset, near);
            DrawLine(graphics, horizontal, line, inset, far, length - 1 - inset, far);
            float pixelsPerMillimeter = length / millimeters;
            int stroke = (int)line.Width;
            int step = pixelsPerMillimeter >= 2 ? 1 : pixelsPerMillimeter >= 0.4f ? 5 : 10;
            int labelStep = pixelsPerMillimeter >= 1.3f ? 50 : pixelsPerMillimeter >= 0.65f ? 100 : 200;
            for (int mm = step; mm < millimeters; mm += step)
            {
                int along = (int)Math.Round(TickPosition(mm, length, millimeters));
                if (along < inset * 2 || along >= length - inset * 2) continue;
                int tick = (mm % 10 == 0 ? 13 : mm % 5 == 0 ? 8 : 4) * stroke;
                DrawLine(graphics, horizontal, shadow, along, near, along, near + tick);
                DrawLine(graphics, horizontal, shadow, along, far, along, far - tick);
                DrawLine(graphics, horizontal, line, along, near, along, near + tick);
                DrawLine(graphics, horizontal, line, along, far, along, far - tick);
                if (mm % labelStep != 0 || along < 35 || along >= length - 35) continue;
                string text = UiText.Format("RulerCentimeters", mm / 10);
                int gap = tick + 4;
                Size size = TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding);
                DrawLabel(graphics, horizontal, text, font, size, along, near + gap, false);
                DrawLabel(graphics, horizontal, text, font, size, along, far - gap, true);
            }
        }

        private static void DrawLine(Graphics graphics, bool horizontal, Pen pen, int a, int b, int c, int d)
        {
            if (horizontal) graphics.DrawLine(pen, a, b, c, d);
            else graphics.DrawLine(pen, b, a, d, c);
        }

        private static void DrawLabel(Graphics graphics, bool horizontal, string text, Font font, Size size, int along, int across, bool far)
        {
            Point position = horizontal ? new Point(along - size.Width / 2, across - (far ? size.Height : 0))
                                        : new Point(across - (far ? size.Width : 0), along - size.Height / 2);
            TextRenderer.DrawText(graphics, text, font, position, BoardStyle.RulerAccent, Color.FromArgb(20, 27, 36), TextFormatFlags.NoPadding);
        }
    }
}

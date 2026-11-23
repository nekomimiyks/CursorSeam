using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CursorSeam
{
    internal sealed class BoardToggle : CheckBox
    {
        internal BoardToggle()
        {
            AutoSize = true;
            Anchor = AnchorStyles.Left;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        private int ScalePixels(int pixels) { return (pixels * DeviceDpi + 48) / 96; }

        public override Size GetPreferredSize(Size proposedSize)
        {
            if (BoardStyle.HighContrast) return base.GetPreferredSize(proposedSize);
            Size label = TextRenderer.MeasureText(Text, Font);
            return new Size(ScalePixels(68) + label.Width, Math.Max(ScalePixels(36), label.Height));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (BoardStyle.HighContrast) { base.OnPaint(e); return; }
            int trackWidth = ScalePixels(58), trackHeight = ScalePixels(24), inset = ScalePixels(3);
            int top = (Height - trackHeight) / 2, thumb = trackHeight - inset * 2;
            var track = new Rectangle(1, top, trackWidth, trackHeight);
            Color background = !Enabled ? BoardStyle.DisabledButton : Checked ? BoardStyle.ToggleOn : BoardStyle.ToggleOff;
            Color thumbColor = Enabled ? Color.White : BoardStyle.DisabledInk;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = BoardStyle.Rounded(track, trackHeight / 2f))
            using (var fill = new SolidBrush(background))
                e.Graphics.FillPath(fill, path);
            using (var fill = new SolidBrush(thumbColor))
                e.Graphics.FillEllipse(fill, Checked ? track.Right - inset - thumb : track.Left + inset, top + inset, thumb, thumb);

            var state = new Rectangle(Checked ? track.Left + inset : track.Left + thumb + inset, top, trackWidth - thumb - inset * 2, trackHeight);
            TextRenderer.DrawText(e.Graphics, Checked ? "ON" : "OFF", Font, state, thumbColor,
                TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            var label = new Rectangle(ScalePixels(68), 0, Width - ScalePixels(68), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, label, Enabled ? BoardStyle.Ink : BoardStyle.DisabledInk,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1), BoardStyle.Ink, BackColor);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
        }
    }

    internal sealed class BoardLanguagePicker : Button
    {
        private readonly ContextMenuStrip choicesMenu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };
        internal string SelectedLanguage { get; private set; }
        internal event EventHandler SelectionChanged;

        internal BoardLanguagePicker()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Anchor = AnchorStyles.Left;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.ButtonDropDown;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            choicesMenu.VisibleChanged += (sender, e) => Invalidate();
        }

        internal void SetChoices(IEnumerable<KeyValuePair<string, string>> choices, string selected)
        {
            while (choicesMenu.Items.Count != 0)
            {
                ToolStripItem item = choicesMenu.Items[0];
                choicesMenu.Items.RemoveAt(0);
                item.Dispose();
            }
            foreach (var choice in choices)
            {
                var option = new ToolStripMenuItem(choice.Value) { Tag = choice.Key, Checked = choice.Key == selected };
                choicesMenu.Items.Add(option);
                if (option.Checked) Text = choice.Value;
                option.Click += (sender, e) =>
                {
                    if (SelectedLanguage == choice.Key) return;
                    SelectedLanguage = choice.Key;
                    var changed = SelectionChanged;
                    if (changed != null) changed(this, EventArgs.Empty);
                };
            }
            SelectedLanguage = selected;
            choicesMenu.AccessibleName = AccessibleName;
            if (Parent != null) Parent.PerformLayout();
        }

        private int ScalePixels(int pixels) { return (pixels * DeviceDpi + 48) / 96; }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int longest = choicesMenu == null || choicesMenu.Items.Count == 0 ? 0 :
                choicesMenu.Items.Cast<ToolStripItem>().Max(item => TextRenderer.MeasureText(item.Text, Font).Width);
            return new Size(Math.Max(ScalePixels(250), longest + ScalePixels(46)), Math.Max(ScalePixels(36), Font.Height + ScalePixels(14)));
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (choicesMenu.Items.Count == 0) return;
            choicesMenu.Font = Font;
            choicesMenu.MinimumSize = new Size(Width, 0);
            BoardStyle.StyleMenu(choicesMenu);
            choicesMenu.Show(this, new Point(0, Height));
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Enabled && (keyData == Keys.Down || keyData == (Keys.Alt | Keys.Down) || keyData == Keys.F4))
            {
                PerformClick();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var frame = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (var path = BoardStyle.Rounded(frame, ScalePixels(5)))
            using (var fill = new SolidBrush(choicesMenu.Visible ? BoardStyle.QuietButton : BoardStyle.Field))
            using (var border = new Pen(Focused ? BoardStyle.Accent : BoardStyle.FieldBorder))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            int x = Width - ScalePixels(18), y = Height / 2, arrow = ScalePixels(4);
            using (var pen = new Pen(BoardStyle.Muted, ScalePixels(2)))
                g.DrawLines(pen, new[] { new Point(x - arrow, y - 2), new Point(x, y + 2), new Point(x + arrow, y - 2) });
            int left = ScalePixels(14);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(left, 0, Width - left - ScalePixels(34), Height), BoardStyle.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -4, -3), BoardStyle.Ink, BoardStyle.Field);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) choicesMenu.Dispose();
            base.Dispose(disposing);
        }
    }
}

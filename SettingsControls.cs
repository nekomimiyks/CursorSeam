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
        private const TextFormatFlags TextFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        private const TextFormatFlags LabelFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        private bool hovered;

        internal BoardToggle()
        {
            AutoSize = true;
            Anchor = AnchorStyles.Left;
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
        }

        private int ScalePixels(int pixels) { return (pixels * DeviceDpi + 48) / 96; }

        private int TrackHeight { get { return Math.Max(ScalePixels(24), Font.Height + ScalePixels(6)); } }
        private int GetTrackWidth(IDeviceContext graphics)
        {
            int on = BoardStyle.MeasureText(graphics, "ON", Font, TextFlags).Width;
            int off = BoardStyle.MeasureText(graphics, "OFF", Font, TextFlags).Width;
            return Math.Max(ScalePixels(58), TrackHeight + Math.Max(on, off) + ScalePixels(6));
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            if (BoardStyle.HighContrast) return base.GetPreferredSize(proposedSize);
            using (Graphics graphics = IsHandleCreated ? CreateGraphics() : null)
            {
                int trackSpace = GetTrackWidth(graphics) + ScalePixels(14);
                int labelWidth = MaximumSize.Width == 0 ? int.MaxValue : Math.Max(1, MaximumSize.Width - trackSpace);
                Size label = BoardStyle.MeasureText(graphics, Text, Font, LabelFlags, labelWidth);
                return new Size(trackSpace + Math.Min(label.Width, labelWidth),
                    Math.Max(ScalePixels(40), Math.Max(TrackHeight, label.Height) + ScalePixels(8)));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor);
            if (BoardStyle.HighContrast) { base.OnPaint(e); return; }
            int trackWidth = GetTrackWidth(e.Graphics), trackHeight = TrackHeight, inset = ScalePixels(3);
            int top = (Height - trackHeight) / 2, thumb = trackHeight - inset * 2;
            var track = new Rectangle(1, top, trackWidth, trackHeight);
            Color background = !Enabled ? BoardStyle.DisabledButton : Checked ? BoardStyle.ToggleOn : BoardStyle.ToggleOff;
            if (Enabled && hovered) background = ControlPaint.Dark(background, 0.06f);
            Color thumbColor = Enabled ? Color.White : BoardStyle.DisabledInk;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = BoardStyle.Rounded(track, trackHeight / 2f))
            using (var fill = new SolidBrush(background))
                e.Graphics.FillPath(fill, path);
            using (var fill = new SolidBrush(thumbColor))
                e.Graphics.FillEllipse(fill, Checked ? track.Right - inset - thumb : track.Left + inset, top + inset, thumb, thumb);

            var state = new Rectangle(Checked ? track.Left + inset : track.Left + thumb + inset, top, trackWidth - thumb - inset * 2, trackHeight);
            TextRenderer.DrawText(e.Graphics, Checked ? "ON" : "OFF", Font, state, thumbColor,
                TextFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            int labelStart = trackWidth + ScalePixels(12);
            int labelWidth = Math.Max(1, Width - labelStart - ScalePixels(2));
            Size text = BoardStyle.MeasureText(e.Graphics, Text, Font, LabelFlags, labelWidth);
            var label = new Rectangle(labelStart, Math.Max(0, (Height - text.Height) / 2), labelWidth, text.Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, label, Enabled ? BoardStyle.Ink : BoardStyle.DisabledInk,
                LabelFlags);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1), BoardStyle.Ink, BackColor);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            if (Parent != null) Parent.PerformLayout(this, "PreferredSize");
            Invalidate();
        }
    }

    internal sealed class BoardLanguagePicker : Button
    {
        private const TextFormatFlags TextFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        private readonly ContextMenuStrip choicesMenu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };
        internal string SelectedLanguage { get; private set; }
        internal event EventHandler SelectionChanged;

        internal BoardLanguagePicker()
        {
            AutoSize = false;
            Anchor = AnchorStyles.Left;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.ButtonDropDown;
            FlatStyle = FlatStyle.Flat;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
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
            FitSelection();
        }

        private int ScalePixels(int pixels) { return (pixels * DeviceDpi + 48) / 96; }

        public override Size GetPreferredSize(Size proposedSize)
        {
            using (Graphics graphics = IsHandleCreated ? CreateGraphics() : null)
            {
                int longest = choicesMenu == null || choicesMenu.Items.Count == 0 ? 0 :
                    choicesMenu.Items.Cast<ToolStripItem>().Max(item => BoardStyle.MeasureText(graphics, item.Text, Font, TextFlags).Width);
                return new Size(Math.Max(ScalePixels(300), longest + ScalePixels(54)), Math.Max(ScalePixels(40), Font.Height + ScalePixels(14)));
            }
        }

        private void FitSelection() { Size = GetPreferredSize(Size.Empty); Invalidate(); }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); FitSelection(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); FitSelection(); }
        protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); FitSelection(); }

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
            g.Clear(Parent == null ? BackColor : Parent.BackColor);
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
            TextRenderer.DrawText(g, Text, Font, new Rectangle(left, 0, Math.Max(0, Width - left - ScalePixels(34)), Height), BoardStyle.Ink,
                TextFlags | TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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

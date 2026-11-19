using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using DisplayLayout = CursorSeam.Layout;

namespace CursorSeam
{
    internal sealed class LayoutView : Control
    {
        internal event Action GeometryChanged, StateChanged, EditCommitted;
        internal Display Selected { get; private set; }
        internal bool ResizingEnabled { get; private set; }
        internal bool IsDragging { get { return dragging; } }
        internal Layout Model { get; private set; }

        private List<Display> displays = new List<Display>();
        private float scale = 1;
        private PointF origin;
        private Point start;
        private Display dragTarget;
        private Point resizeEdges;
        private PointF resizeAnchor;
        private RectangleF originalArea;
        private bool originalEstimated;
        private bool dragging;
        private Seam hovered;
        private float hoverPosition;
        private TextureBrush dots;
        private Font numberFont;

        internal LayoutView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = BoardStyle.Surface;
            ForeColor = BoardStyle.Ink;
            AccessibleName = UiText.Get("LayoutAccessibleName");
        }

        internal void SetDisplays(List<Display> value)
        {
            CancelDrag();
            displays = value;
            Selected = displays.FirstOrDefault(d => Selected != null && d.Id == Selected.Id) ?? displays.FirstOrDefault();
            RefreshLayout();
        }

        internal void RefreshLayout(bool stateChanged = true)
        {
            hovered = null;
            Model = new Layout(displays);
            if (!dragging) Fit();
            Invalidate();
            if (GeometryChanged != null) GeometryChanged();
            if (stateChanged && StateChanged != null) StateChanged();
        }

        internal void SetResizeMode(bool enabled)
        {
            if (ResizingEnabled == enabled) return;
            CancelDrag();
            ResizingEnabled = enabled;
            Cursor = Cursors.Default;
            Invalidate();
        }

        internal void CancelDrag()
        {
            if (!Capture) return;
            Capture = false;
        }

        private void Fit()
        {
            if (displays.Count == 0 || Width <= 0 || Height <= 0) return;
            RectangleF world = displays.Select(d => d.Area).Aggregate(RectangleF.Union);
            float padding = Math.Min(80f, Math.Min(Width, Height) / 3f);
            scale = Math.Min((Width - padding) / world.Width, (Height - padding) / world.Height);
            scale = Math.Max(0.001f, scale);
            origin = new PointF((Width - world.Width * scale) / 2 - world.X * scale,
                                (Height - world.Height * scale) / 2 - world.Y * scale);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); if (!dragging) Fit(); }

        private PointF ToView(float x, float y) { return new PointF(origin.X + x * scale, origin.Y + y * scale); }
        private RectangleF ToView(RectangleF area) { return new RectangleF(ToView(area.X, area.Y), new SizeF(area.Width * scale, area.Height * scale)); }
        private PointF ToWorld(Point pt) { return new PointF((pt.X - origin.X) / scale, (pt.Y - origin.Y) / scale); }

        private static TextureBrush CreateDots()
        {
            using (var tile = new Bitmap(20, 20))
            using (var graphics = Graphics.FromImage(tile))
            using (var dot = new SolidBrush(Color.FromArgb(187, 205, 212)))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillEllipse(dot, 15, 15, 2, 2);
                return new TextureBrush(tile, WrapMode.Tile);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && dots != null) { dots.Dispose(); dots = null; }
            if (disposing) ReleaseNumberFont();
            base.Dispose(disposing);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            ReleaseNumberFont();
            base.OnFontChanged(e);
        }

        private void ReleaseNumberFont()
        {
            if (numberFont != null) { numberFont.Dispose(); numberFont = null; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var background = new SolidBrush(BoardStyle.Canvas))
            using (var shape = BoardStyle.Rounded(ClientRectangle, 12)) g.FillPath(background, shape);
            if (dots == null) dots = CreateDots();
            g.FillRectangle(dots, ClientRectangle);
            if (displays.Count == 0)
            {
                TextRenderer.DrawText(g, UiText.Get("NoDisplays"), Font, ClientRectangle, BoardStyle.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            foreach (Display display in displays)
                if (display != Selected) DrawDisplay(g, display);
            if (Selected != null) DrawDisplay(g, Selected);
            if (Model == null) return;
            using (var line = new Pen(BoardStyle.Accent, 5))
            using (var separated = new Pen(BoardStyle.Accent, 2) { DashStyle = DashStyle.Dash })
            {
                line.StartCap = line.EndCap = LineCap.Round;
                foreach (Seam seam in Model.Seams)
                {
                    if (seam.Edge != Side.Right && seam.Edge != Side.Bottom) continue;
                    PointF a = SeamPoint(seam, seam.From), b = SeamPoint(seam, seam.To);
                    if (seam.Distance <= 0.1f) g.DrawLine(line, a, b);
                    else
                    {
                        float offset = (DisplayLayout.Edge(seam.B.Area, DisplayLayout.Opposite(seam.Edge)) - DisplayLayout.Edge(seam.A.Area, seam.Edge)) * scale;
                        PointF destination = seam.Vertical ? new PointF(offset, 0) : new PointF(0, offset);
                        var middle = new PointF((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                        g.DrawLine(separated, a, b);
                        g.DrawLine(separated, a.X + destination.X, a.Y + destination.Y, b.X + destination.X, b.Y + destination.Y);
                        g.DrawLine(separated, middle.X, middle.Y, middle.X + destination.X, middle.Y + destination.Y);
                    }
                }
            }
            if (hovered != null)
            {
                PointF center = SeamPoint(hovered, hoverPosition);
                using (var guide = new Pen(BoardStyle.Ink, 1) { DashStyle = DashStyle.Dash })
                using (var marker = new SolidBrush(BoardStyle.Ink))
                {
                    RectangleF a = ToView(hovered.A.Area), b = ToView(hovered.B.Area);
                    if (hovered.Vertical) g.DrawLine(guide, a.Left, center.Y, b.Right, center.Y);
                    else g.DrawLine(guide, center.X, a.Top, center.X, b.Bottom);
                    g.FillEllipse(marker, center.X - 4, center.Y - 4, 8, 8);
                }
            }
            if (ResizingEnabled && Selected != null) DrawResizeGrips(g, ToView(Selected.Area));
        }

        private void DrawDisplay(Graphics g, Display display)
        {
            RectangleF box = ToView(display.Area);
            bool selected = display == Selected;
            using (var fill = new SolidBrush(selected ? Color.FromArgb(210, 235, 228) : Color.FromArgb(233, 242, 245)))
            using (var border = new Pen(selected ? BoardStyle.Accent : BoardStyle.Ink, selected ? 3 : 2))
            using (var shape = BoardStyle.Rounded(box, 4))
            {
                g.FillPath(fill, shape);
                g.DrawPath(border, shape);
            }
            if (box.Width < 22 || box.Height < 22) return;
            TextFormatFlags format = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
            if (box.Width < 100 || box.Height < 66)
            {
                TextRenderer.DrawText(g, display.Number.ToString(), Font, Rectangle.Round(box), BoardStyle.Ink, format);
                return;
            }
            Rectangle number = Rectangle.Round(new RectangleF(box.Left + 6, box.Top + box.Height / 2 - 26, box.Width - 12, 34));
            Rectangle caption = Rectangle.Round(new RectangleF(box.Left + 6, number.Bottom + 1, box.Width - 12, 20));
            if (numberFont == null) numberFont = new Font(Font.Name, Font.Size * 2, FontStyle.Bold);
            TextRenderer.DrawText(g, display.Number.ToString(), numberFont, number, BoardStyle.Ink, format);
            TextRenderer.DrawText(g, display.Estimated ? UiText.Get("Estimated") : display.DisplayName, Font, caption, BoardStyle.Muted, format);
        }

        private static void DrawResizeGrips(Graphics g, RectangleF box)
        {
            if (box.Width < 22 || box.Height < 22) return;
            float[] xs = { box.Left, box.Left + box.Width / 2, box.Right };
            float[] ys = { box.Top, box.Top + box.Height / 2, box.Bottom };
            using (var fill = new SolidBrush(BoardStyle.Accent))
            using (var outline = new Pen(BoardStyle.Surface, 1.4f))
            for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++)
            {
                if (row == 1 && column == 1) continue;
                float extent = row != 1 && column != 1 ? 10 : 8;
                using (var grip = BoardStyle.Rounded(new RectangleF(xs[column] - extent / 2, ys[row] - extent / 2, extent, extent), 2.5f))
                {
                    g.FillPath(fill, grip);
                    g.DrawPath(outline, grip);
                }
            }
        }

        private PointF SeamPoint(Seam seam, float along)
        {
            float edge = DisplayLayout.Edge(seam.A.Area, seam.Edge);
            return seam.Vertical ? ToView(edge, along) : ToView(along, edge);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            PointF world = ToWorld(e.Location);
            Display hit = HitDisplay(world);
            if (hit == null) return;
            Selected = dragTarget = hit;
            if (StateChanged != null) StateChanged();
            resizeEdges = GetResizeEdges(hit, world);
            if (!resizeEdges.IsEmpty)
                resizeAnchor = new PointF(resizeEdges.X == 0 ? AttachmentAnchor(hit, Side.Left, Side.Right) : resizeEdges.X < 0 ? 1 : 0,
                                          resizeEdges.Y == 0 ? AttachmentAnchor(hit, Side.Top, Side.Bottom) : resizeEdges.Y < 0 ? 1 : 0);
            originalArea = hit.Area;
            originalEstimated = hit.Estimated;
            start = e.Location;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            PointF world = ToWorld(e.Location);
            if (Capture && dragTarget != null && e.Button == MouseButtons.Left)
            {
                if (!dragging && Math.Abs(e.X - start.X) + Math.Abs(e.Y - start.Y) < 4) return;
                UpdateDrag(e.Location, (ModifierKeys & Keys.Shift) != 0);
                return;
            }
            Display hit = HitDisplay(world);
            Point edges = GetResizeEdges(hit, world);
            if (hit == null) Cursor = Cursors.Default;
            else if (edges.IsEmpty) Cursor = Cursors.SizeAll;
            else if (edges.X == 0) Cursor = Cursors.SizeNS;
            else if (edges.Y == 0) Cursor = Cursors.SizeWE;
            else Cursor = edges.X == edges.Y ? Cursors.SizeNWSE : Cursors.SizeNESW;
            Seam previous = hovered;
            hovered = HitSeam(world);
            if (hovered != null) hoverPosition = hovered.Vertical ? world.Y : world.X;
            if (previous != null || hovered != null) Invalidate();
        }

        private Seam HitSeam(PointF world)
        {
            if (Model == null) return null;
            foreach (Seam seam in Model.Seams)
            {
                if (seam.Edge != Side.Right && seam.Edge != Side.Bottom) continue;
                float along = seam.Vertical ? world.Y : world.X;
                float cross = seam.Vertical ? world.X : world.Y;
                if (along >= seam.From && along < seam.To && Math.Abs(cross - DisplayLayout.Edge(seam.A.Area, seam.Edge)) * scale <= 8)
                    return seam;
            }
            return null;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hovered != null) { hovered = null; Invalidate(); }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || dragTarget == null) return;
            bool changed = false;
            if (dragging)
            {
                bool shift = (ModifierKeys & Keys.Shift) != 0;
                UpdateDrag(e.Location, shift);
                if (!shift && !resizeEdges.IsEmpty && dragTarget.Area != originalArea)
                    dragTarget.Area = Snap(dragTarget, dragTarget.Area, 15 / scale);
                changed = dragTarget.Area != originalArea;
                dragTarget.Estimated = originalEstimated && (resizeEdges.IsEmpty || !changed);
                dragging = false;
                RefreshLayout();
            }
            dragTarget = null;
            Capture = false;
            if (changed && EditCommitted != null) EditCommitted();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && dragTarget != null)
            {
                dragTarget.Area = originalArea;
                dragTarget.Estimated = originalEstimated;
                dragTarget = null;
                dragging = false;
                RefreshLayout();
            }
        }

        private void UpdateDrag(Point location, bool shift)
        {
            bool wasDragging = dragging;
            dragging = true;
            var movement = new PointF((location.X - start.X) / scale, (location.Y - start.Y) / scale);
            RectangleF area = originalArea;
            if (resizeEdges.IsEmpty)
            {
                area.Offset(movement);
                area = shift ? DisplayLayout.SnapToNeighbor(area, Neighbors(dragTarget), 22 / scale, true) : Snap(dragTarget, area, 15 / scale);
            }
            else
            {
                area = DisplayLayout.ResizeProportionally(originalArea, resizeEdges, movement, resizeAnchor);
                if (shift) area = DisplayLayout.SnapResize(originalArea, area, resizeEdges, resizeAnchor, Neighbors(dragTarget), 22 / scale);
            }
            if (wasDragging && dragTarget.Area == area) return;
            bool estimated = originalEstimated && (resizeEdges.IsEmpty || area.Size == originalArea.Size);
            bool stateChanged = !wasDragging || dragTarget.Estimated != estimated;
            dragTarget.Area = area;
            dragTarget.Estimated = estimated;
            RefreshLayout(stateChanged);
        }

        private Display HitDisplay(PointF point)
        {
            if (Selected != null && TargetContains(Selected, point)) return Selected;
            for (int i = displays.Count - 1; i >= 0; i--)
                if (TargetContains(displays[i], point)) return displays[i];
            return null;
        }

        private bool TargetContains(Display display, PointF point)
        {
            RectangleF area = display.Area;
            if (ResizingEnabled) area.Inflate(6 / scale, 6 / scale);
            return area.Contains(point);
        }

        private Point GetResizeEdges(Display display, PointF point)
        {
            if (!ResizingEnabled || display == null) return Point.Empty;
            RectangleF area = display.Area;
            float x = Math.Min(9 / scale, area.Width / 3), y = Math.Min(9 / scale, area.Height / 3);
            return new Point(point.X - area.Left < x ? -1 : area.Right - point.X <= x ? 1 : 0,
                             point.Y - area.Top < y ? -1 : area.Bottom - point.Y <= y ? 1 : 0);
        }

        private float AttachmentAnchor(Display display, Side near, Side far)
        {
            bool attachedNear = Model.Seams.Any(s => s.A.Id == display.Id && s.Edge == near);
            bool attachedFar = Model.Seams.Any(s => s.A.Id == display.Id && s.Edge == far);
            return attachedNear == attachedFar ? 0.5f : attachedNear ? 0 : 1;
        }

        private RectangleF Snap(Display display, RectangleF area, float tolerance)
        {
            return DisplayLayout.SnapToNeighbor(area, Neighbors(display), Math.Min(tolerance, 45), false);
        }

        private IEnumerable<RectangleF> Neighbors(Display display)
        {
            foreach (Display other in displays)
                if (other != display) yield return other.Area;
        }
    }
}

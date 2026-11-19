using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace CursorSeam
{
    internal enum Side { Left, Right, Top, Bottom }

    internal sealed class Display
    {
        internal string Id, Name;
        internal int Number;
        internal bool Primary, Estimated;
        internal Rectangle Pixels;
        internal RectangleF Area;

        internal string DisplayName { get { return string.IsNullOrWhiteSpace(Name) ? UiText.Get("DisplayFallback") : Name; } }
        internal string Caption { get { return UiText.Format("DisplayCaption", Number, DisplayName); } }
        internal Display Copy() { return (Display)MemberwiseClone(); }
    }

    internal sealed class Seam
    {
        internal Display A, B;
        internal Side Edge;
        internal float From, To;
        internal float Distance;
        internal bool Vertical { get { return Edge == Side.Left || Edge == Side.Right; } }
    }

    internal sealed class Layout
    {
        internal readonly List<Display> Displays;
        internal readonly List<Seam> Seams = new List<Seam>();
        internal readonly Rectangle Desktop;
        internal readonly bool AllDisplaysReachable;

        internal Layout(IEnumerable<Display> displays)
        {
            Displays = displays.Select(display => display.Copy()).ToList();
            if (Displays.Count > 0) Desktop = Displays.Select(d => d.Pixels).Aggregate(Rectangle.Union);
            var candidates = new List<Seam>();
            for (int a = 0; a < Displays.Count; a++)
            for (int b = a + 1; b < Displays.Count; b++)
            {
                Seam candidate = FacingEdges(Displays[a], Displays[b]);
                if (candidate != null) candidates.Add(candidate);
            }
            foreach (Seam candidate in candidates.OrderBy(s => s.Distance)) ReserveOpenIntervals(candidate);
            AllDisplaysReachable = Connected();
        }

        private static Seam FacingEdges(Display a, Display b)
        {
            RectangleF first = a.Area, second = b.Area;
            if (first.Contains(second) || second.Contains(first)) return null;
            float left = first.Left - second.Right, right = second.Left - first.Right;
            float top = first.Top - second.Bottom, bottom = second.Top - first.Bottom;
            float horizontalGap = Math.Max(left, right), verticalGap = Math.Max(top, bottom);
            bool overlapping = horizontalGap < 0 && verticalGap < 0;
            bool horizontal = overlapping
                ? horizontalGap / (first.Width + second.Width) >= verticalGap / (first.Height + second.Height)
                : horizontalGap >= verticalGap;
            float from = horizontal ? Math.Max(first.Top, second.Top) : Math.Max(first.Left, second.Left);
            float to = horizontal ? Math.Min(first.Bottom, second.Bottom) : Math.Min(first.Right, second.Right);
            if (to - from <= 0.1f) return null;
            Side side = horizontal ? (right >= left ? Side.Right : Side.Left) : (bottom >= top ? Side.Bottom : Side.Top);
            return new Seam { A = a, B = b, Edge = side, From = from, To = to, Distance = Math.Abs(horizontal ? horizontalGap : verticalGap) };
        }

        private void ReserveOpenIntervals(Seam candidate)
        {
            Side opposite = Opposite(candidate.Edge);
            // The closest screen gets each section first. Reserve both sides so the crossing is reversible.
            var occupied = Seams.Where(s => (s.A == candidate.A && s.Edge == candidate.Edge) || (s.A == candidate.B && s.Edge == opposite))
                .Where(s => s.To > candidate.From && s.From < candidate.To).OrderBy(s => s.From).ToArray();
            float from = candidate.From;
            foreach (Seam seam in occupied)
            {
                AddSeamPair(candidate, from, seam.From);
                from = Math.Max(from, seam.To);
            }
            AddSeamPair(candidate, from, candidate.To);
        }

        private void AddSeamPair(Seam candidate, float from, float to)
        {
            if (to - from <= 0.1f) return;
            Seams.Add(new Seam { A = candidate.A, B = candidate.B, Edge = candidate.Edge, From = from, To = to, Distance = candidate.Distance });
            Seams.Add(new Seam { A = candidate.B, B = candidate.A, Edge = Opposite(candidate.Edge), From = from, To = to, Distance = candidate.Distance });
        }

        private bool Connected()
        {
            if (Displays.Count < 2) return false;
            var reachable = new HashSet<Display> { Displays[0] };
            int oldCount;
            do
            {
                oldCount = reachable.Count;
                foreach (Seam seam in Seams)
                    if (reachable.Contains(seam.A)) reachable.Add(seam.B);
            } while (oldCount != reachable.Count);
            return reachable.Count == Displays.Count;
        }

        internal static float Edge(RectangleF rect, Side edge)
        {
            switch (edge)
            {
                case Side.Left: return rect.Left;
                case Side.Right: return rect.Right;
                case Side.Top: return rect.Top;
                default: return rect.Bottom;
            }
        }

        internal static Side Opposite(Side side) { return (Side)((int)side ^ 1); }
        internal static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }

        internal static RectangleF SnapToNeighbor(RectangleF area, IEnumerable<RectangleF> neighbors, float tolerance, bool alignEdges)
        {
            float best = float.PositiveInfinity;
            RectangleF result = area;
            float horizontal = float.PositiveInfinity, vertical = float.PositiveInfinity;
            foreach (RectangleF neighbor in neighbors)
            {
                for (Side side = Side.Left; side <= Side.Bottom; side++)
                {
                    float normal = Edge(neighbor, Opposite(side)) - Edge(area, side);
                    bool verticalSide = side == Side.Left || side == Side.Right;
                    float overlap = AxisOverlap(area, neighbor, !verticalSide);
                    if (Math.Abs(normal) < tolerance && Math.Abs(normal) < best && overlap > 0.1f)
                    {
                        best = Math.Abs(normal);
                        result = area;
                        result.Offset(verticalSide ? normal : 0, verticalSide ? 0 : normal);
                    }
                    if (!alignEdges || Math.Abs(normal) > tolerance * 2 || overlap < -tolerance) continue;
                    if (verticalSide) FindParallelOffset(area.Top, area.Bottom, neighbor.Top, neighbor.Bottom, tolerance, ref vertical);
                    else FindParallelOffset(area.Left, area.Right, neighbor.Left, neighbor.Right, tolerance, ref horizontal);
                }
            }
            if (!float.IsInfinity(horizontal)) result.X = area.X + horizontal;
            if (!float.IsInfinity(vertical)) result.Y = area.Y + vertical;
            return result;
        }

        private static void FindParallelOffset(float first, float last, float neighborFirst, float neighborLast, float tolerance, ref float best)
        {
            ConsiderOffset(neighborFirst - first, tolerance, ref best);
            ConsiderOffset(neighborLast - last, tolerance, ref best);
            ConsiderOffset(neighborFirst - last, tolerance, ref best);
            ConsiderOffset(neighborLast - first, tolerance, ref best);
        }

        private static void ConsiderOffset(float delta, float tolerance, ref float best)
        {
            if (Math.Abs(delta) <= tolerance && Math.Abs(delta) < Math.Abs(best)) best = delta;
        }

        private static float AxisOverlap(RectangleF a, RectangleF b, bool xAxis)
        {
            return xAxis ? Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left)
                         : Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
        }

        internal static RectangleF ResizeProportionally(RectangleF original, Point edges, PointF movement, PointF anchor)
        {
            float horizontal = original.Width * edges.X;
            float vertical = original.Height * edges.Y;
            float stretch = 1 + (horizontal * movement.X + vertical * movement.Y) / (horizontal * horizontal + vertical * vertical);
            return ResizeAtScale(original, anchor, stretch);
        }

        private static RectangleF ResizeAtScale(RectangleF original, PointF anchor, float stretch)
        {
            float minimum = Math.Max(40 / original.Width, 40 / original.Height);
            float maximum = Math.Min(10000 / original.Width, 10000 / original.Height);
            stretch = Clamp(stretch, minimum, maximum);
            var size = new SizeF(original.Width * stretch, original.Height * stretch);
            return new RectangleF(original.X + (original.Width - size.Width) * anchor.X,
                                  original.Y + (original.Height - size.Height) * anchor.Y, size.Width, size.Height);
        }

        internal static RectangleF SnapResize(RectangleF original, RectangleF area, Point edges, PointF anchor,
                                              IEnumerable<RectangleF> neighbors, float tolerance)
        {
            RectangleF result = area;
            float stretch = area.Width / original.Width;
            float best = float.PositiveInfinity;
            foreach (RectangleF neighbor in neighbors)
            {
                if (edges.X != 0 && Math.Max(AxisOverlap(area, neighbor, false), AxisOverlap(original, neighbor, false)) >= -tolerance)
                {
                    Side side = edges.X < 0 ? Side.Left : Side.Right;
                    float rate = original.Width * (edges.X < 0 ? -anchor.X : 1 - anchor.X);
                    TrySnapResize(original, area, anchor, stretch, side, rate, neighbor.Left, tolerance, ref best, ref result);
                    TrySnapResize(original, area, anchor, stretch, side, rate, neighbor.Right, tolerance, ref best, ref result);
                }
                if (edges.Y != 0 && Math.Max(AxisOverlap(area, neighbor, true), AxisOverlap(original, neighbor, true)) >= -tolerance)
                {
                    Side side = edges.Y < 0 ? Side.Top : Side.Bottom;
                    float rate = original.Height * (edges.Y < 0 ? -anchor.Y : 1 - anchor.Y);
                    TrySnapResize(original, area, anchor, stretch, side, rate, neighbor.Top, tolerance, ref best, ref result);
                    TrySnapResize(original, area, anchor, stretch, side, rate, neighbor.Bottom, tolerance, ref best, ref result);
                }
            }
            return result;
        }

        private static void TrySnapResize(RectangleF original, RectangleF area, PointF anchor, float stretch, Side side,
                                          float rate, float guide, float tolerance, ref float best, ref RectangleF result)
        {
            float distance = guide - Edge(area, side);
            if (Math.Abs(distance) > tolerance) return;
            float adjustment = distance / rate;
            if (Math.Abs(adjustment) >= best) return;
            RectangleF snapped = ResizeAtScale(original, anchor, stretch + adjustment);
            if (Math.Abs(Edge(snapped, side) - guide) > 0.1f) return;
            best = Math.Abs(adjustment);
            result = snapped;
        }

        internal Display DisplayAt(Point point)
        {
            foreach (Display display in Displays)
                if (display.Pixels.Contains(point)) return display;
            return null;
        }

        internal static void Arrange(IList<Display> displays)
        {
            if (displays.Count == 0) return;
            var placed = new List<Display> { displays.FirstOrDefault(d => d.Primary) ?? displays[0] };
            Display first = placed[0];
            first.Area = new RectangleF(0, 0, first.Area.Width, first.Area.Height);
            var pending = displays.Where(d => d != first).ToList();
            while (pending.Count > 0)
            {
                var options = from a in placed from b in pending
                              let distanceX = Math.Max(0, Math.Max(a.Pixels.Left - b.Pixels.Right, b.Pixels.Left - a.Pixels.Right))
                              let distanceY = Math.Max(0, Math.Max(a.Pixels.Top - b.Pixels.Bottom, b.Pixels.Top - a.Pixels.Bottom))
                              orderby distanceX + distanceY, Math.Abs(a.Pixels.Left - b.Pixels.Left) + Math.Abs(a.Pixels.Top - b.Pixels.Top)
                              select new { A = a, B = b };
                var nearest = options.First();
                Rectangle aPx = nearest.A.Pixels, bPx = nearest.B.Pixels;
                RectangleF aArea = nearest.A.Area, bArea = nearest.B.Area;
                float x = (bPx.Left - aPx.Left) * aArea.Width / aPx.Width;
                float y = (bPx.Top - aPx.Top) * aArea.Height / aPx.Height;
                if (aPx.Right <= bPx.Left || bPx.Right <= aPx.Left)
                {
                    bArea.X = aPx.Right <= bPx.Left ? aArea.Right : aArea.Left - bArea.Width;
                    bArea.Y = Clamp(aArea.Top + y, aArea.Top - bArea.Height + 10, aArea.Bottom - 10);
                }
                else
                {
                    bArea.Y = aPx.Bottom <= bPx.Top ? aArea.Bottom : aArea.Top - bArea.Height;
                    bArea.X = Clamp(aArea.Left + x, aArea.Left - bArea.Width + 10, aArea.Right - 10);
                }
                nearest.B.Area = bArea;
                placed.Add(nearest.B);
                pending.Remove(nearest.B);
            }
        }

        internal bool Map(Point current, Point proposed, bool blockAtClosedEdge, out Point result)
        {
            result = proposed;
            Display source = DisplayAt(current);
            if (source == null || source.Pixels.Contains(proposed)) return false;
            Rectangle px = source.Pixels;
            Point movement = WithoutWindowsEdgeSnap(px, current, proposed);
            double dx = (double)movement.X - current.X, dy = (double)movement.Y - current.Y;
            double tx = dx > 0 ? (px.Right - 0.5 - current.X) / dx : dx < 0 ? (px.Left - 0.5 - current.X) / dx : double.PositiveInfinity;
            double ty = dy > 0 ? (px.Bottom - 0.5 - current.Y) / dy : dy < 0 ? (px.Top - 0.5 - current.Y) / dy : double.PositiveInfinity;
            bool vertical = tx <= ty;
            Side side = vertical ? (dx > 0 ? Side.Right : Side.Left) : (dy > 0 ? Side.Bottom : Side.Top);
            RectangleF area = source.Area;
            double t = Math.Min(tx, ty);
            float crossX = (float)(current.X + t * dx);
            float crossY = (float)(current.Y + t * dy);
            float coordinate = vertical ? area.Top + (crossY + 0.5f - px.Top) * area.Height / px.Height
                                        : area.Left + (crossX + 0.5f - px.Left) * area.Width / px.Width;
            Seam seam = SeamAt(source, side, coordinate);
            if (seam == null)
            {
                if (!blockAtClosedEdge) return false;
                result = new Point((int)Clamp(movement.X, px.Left, px.Right - 1), (int)Clamp(movement.Y, px.Top, px.Bottom - 1));
                return result != proposed;
            }

            Rectangle target = seam.B.Pixels;
            RectangleF targetArea = seam.B.Area;
            if (vertical)
            {
                int y = target.Top + (int)((coordinate - targetArea.Top) * target.Height / targetArea.Height);
                result = new Point(side == Side.Right ? target.Left : target.Right - 1, (int)Clamp(y, target.Top, target.Bottom - 1));
            }
            else
            {
                int x = target.Left + (int)((coordinate - targetArea.Left) * target.Width / targetArea.Width);
                result = new Point((int)Clamp(x, target.Left, target.Right - 1), side == Side.Bottom ? target.Top : target.Bottom - 1);
            }
            return true;
        }

        private Seam SeamAt(Display source, Side side, float coordinate)
        {
            foreach (Seam seam in Seams)
                if (seam.A == source && seam.Edge == side && coordinate >= seam.From && coordinate < seam.To) return seam;
            return null;
        }

        private Point WithoutWindowsEdgeSnap(Rectangle source, Point current, Point proposed)
        {
            Display nativeTarget = DisplayAt(proposed);
            if (nativeTarget == null) return proposed;
            Rectangle target = nativeTarget.Pixels;
            // At an exposed edge, Windows can move the cursor to the nearest corner of the next display.
            if ((target.Left >= source.Right || target.Right <= source.Left) &&
                (proposed.X == target.Left || proposed.X == target.Right - 1) &&
                ((current.Y < target.Top && proposed.Y == target.Top) || (current.Y >= target.Bottom && proposed.Y == target.Bottom - 1)))
                return new Point(proposed.X, current.Y);
            if ((target.Top >= source.Bottom || target.Bottom <= source.Top) &&
                (proposed.Y == target.Top || proposed.Y == target.Bottom - 1) &&
                ((current.X < target.Left && proposed.X == target.Left) || (current.X >= target.Right && proposed.X == target.Right - 1)))
                return new Point(current.X, proposed.Y);
            return proposed;
        }
    }
}

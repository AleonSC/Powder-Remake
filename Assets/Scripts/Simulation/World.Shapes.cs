// Shape tools: lines, curves, boxes and circles drawn with any material. The
// shape is placed as a solid stroke and then left to the physics, so metal
// keeps its shape while water or sand falls apart.
using System;

namespace PowderGame.Simulation
{
    public enum Shape { Free, Line, Curve, Box, Circle }

    public sealed partial class World
    {
        /// <summary>
        /// Calls plot(x, y) for every dot along a shape's centre line, each once.
        /// Line and Box run from (x0, y0) to (x1, y1); Circle is centred on (x0, y0) and
        /// passes through (x1, y1); Curve runs from (x0, y0) to (x1, y1) through (mx, my).
        /// </summary>
        public static void TraceShape(Shape shape, int x0, int y0, int x1, int y1, int mx, int my, Action<int, int> plot)
        {
            var tracer = new Tracer(plot);
            switch (shape)
            {
                case Shape.Line:
                    tracer.Segment(x0, y0, x1, y1);
                    break;
                case Shape.Box:
                    tracer.Segment(x0, y0, x1, y0);
                    tracer.Segment(x1, y0, x1, y1);
                    tracer.Segment(x1, y1, x0, y1);
                    tracer.Segment(x0, y1, x0, y0);
                    break;
                case Shape.Circle:
                {
                    double r = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                    int n = Math.Max(8, (int)Math.Ceiling(r * 2 * Math.PI * 2));
                    for (int k = 0; k <= n; k++)
                    {
                        double a = 2 * Math.PI * k / n;
                        tracer.Point(x0 + r * Math.Cos(a), y0 + r * Math.Sin(a));
                    }
                    break;
                }
                case Shape.Curve:
                {
                    // Quadratic Bezier whose control point makes it pass through (mx, my) halfway.
                    double cx = 2 * mx - (x0 + x1) / 2.0, cy = 2 * my - (y0 + y1) / 2.0;
                    double length = Dist(x0, y0, cx, cy) + Dist(cx, cy, x1, y1);
                    int n = Math.Max(2, (int)Math.Ceiling(length * 2));
                    for (int k = 0; k <= n; k++)
                    {
                        double s = (double)k / n, u = 1 - s;
                        tracer.Point(u * u * x0 + 2 * u * s * cx + s * s * x1, u * u * y0 + 2 * u * s * cy + s * s * y1);
                    }
                    break;
                }
            }
        }

        static double Dist(double ax, double ay, double bx, double by)
        {
            return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        }

        /// <summary>Turns a stream of points into dots, skipping repeats of the last dot.</summary>
        sealed class Tracer
        {
            readonly Action<int, int> plot;
            int lastX = int.MinValue, lastY = int.MinValue;

            public Tracer(Action<int, int> plot) { this.plot = plot; }

            public void Point(double x, double y)
            {
                int px = (int)Math.Round(x), py = (int)Math.Round(y);
                if (px == lastX && py == lastY) return;
                lastX = px;
                lastY = py;
                plot(px, py);
            }

            public void Segment(int x0, int y0, int x1, int y1)
            {
                int n = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
                if (n == 0) { Point(x0, y0); return; }
                for (int k = 0; k <= n; k++) Point(x0 + (x1 - x0) * (double)k / n, y0 + (y1 - y0) * (double)k / n);
            }
        }

        /// <summary>
        /// Draws a shape as a solid stroke of material t, pen radius r, or erases along it.
        /// Direction-dependent materials (fans, lasers) point along the shape.
        /// </summary>
        public void DrawShape(Shape shape, int x0, int y0, int x1, int y1, int mx, int my, int r, byte t, bool erase)
        {
            float dx = x1 - x0, dy = y1 - y0, len = (float)Math.Sqrt(dx * dx + dy * dy);
            float dirx = len > 0 ? dx / len : 0, diry = len > 0 ? dy / len : 1;
            TraceShape(shape, x0, y0, x1, y1, mx, my, (x, y) =>
            {
                if (erase) Erase(x, y, r);
                else Stamp(x, y, r, t, dirx, diry);
            });
        }

        /// <summary>Fills every empty dot within radius r of (cx, cy) with t, unlike the speckled freehand brush.</summary>
        public void Stamp(int cx, int cy, int r, byte t, float dirx, float diry)
        {
            if (Elements.Sparse[t] == 1) r = 0; // lightning and lasers: one per dot along the shape
            float r2 = (r + 0.5f) * (r + 0.5f);
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int x = cx + dx, y = cy + dy;
                    if (At(x, y) != E.Empty) continue;
                    int j = y * W + x;
                    Set(j, t);
                    InitParticle(j, t, dirx, diry);
                }
            }
        }
    }
}

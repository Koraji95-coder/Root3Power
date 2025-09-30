using System;
using Autodesk.AutoCAD.Geometry;

namespace R3P.Hivemind.Features.Conduit.Services
{
    internal static class Geometry
    {
        public static Extents3d Expand(Extents3d e, double d)
        {
            if (d <= 0) return e;
            return new Extents3d(
                new Point3d(e.MinPoint.X - d, e.MinPoint.Y - d, e.MinPoint.Z),
                new Point3d(e.MaxPoint.X + d, e.MaxPoint.Y + d, e.MaxPoint.Z)
            );
        }

        public static bool SegmentIntersectsRect(Point2d a, Point2d b, Extents3d r)
        {
            // Quick reject by bounding boxes
            var segMinX = Math.Min(a.X, b.X); var segMaxX = Math.Max(a.X, b.X);
            var segMinY = Math.Min(a.Y, b.Y); var segMaxY = Math.Max(a.Y, b.Y);
            if (segMaxX < r.MinPoint.X || segMinX > r.MaxPoint.X || segMaxY < r.MinPoint.Y || segMinY > r.MaxPoint.Y)
                return false;
            // Check if either endpoint is inside
            if (PointInRect(a, r) || PointInRect(b, r)) return true;
            // Edges of rectangle
            var p1 = new Point2d(r.MinPoint.X, r.MinPoint.Y);
            var p2 = new Point2d(r.MaxPoint.X, r.MinPoint.Y);
            var p3 = new Point2d(r.MaxPoint.X, r.MaxPoint.Y);
            var p4 = new Point2d(r.MinPoint.X, r.MaxPoint.Y);
            if (SegmentsIntersect(a, b, p1, p2)) return true;
            if (SegmentsIntersect(a, b, p2, p3)) return true;
            if (SegmentsIntersect(a, b, p3, p4)) return true;
            if (SegmentsIntersect(a, b, p4, p1)) return true;
            return false;
        }

        public static bool PointInRect(Point2d p, Extents3d r)
            => p.X >= r.MinPoint.X && p.X <= r.MaxPoint.X && p.Y >= r.MinPoint.Y && p.Y <= r.MaxPoint.Y;

        public static double Cross(Point2d a, Point2d b, Point2d c)
            => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        public static bool SegmentsIntersect(Point2d a, Point2d b, Point2d c, Point2d d)
        {
            double d1 = Cross(a, b, c);
            double d2 = Cross(a, b, d);
            double d3 = Cross(c, d, a);
            double d4 = Cross(c, d, b);
            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;
            if (d1 == 0 && OnSegment(a, b, c)) return true;
            if (d2 == 0 && OnSegment(a, b, d)) return true;
            if (d3 == 0 && OnSegment(c, d, a)) return true;
            if (d4 == 0 && OnSegment(c, d, b)) return true;
            return false;
        }

        public static bool OnSegment(Point2d a, Point2d b, Point2d p)
        {
            return Math.Min(a.X, b.X) <= p.X && p.X <= Math.Max(a.X, b.X) &&
                   Math.Min(a.Y, b.Y) <= p.Y && p.Y <= Math.Max(a.Y, b.Y);
        }
    }
}




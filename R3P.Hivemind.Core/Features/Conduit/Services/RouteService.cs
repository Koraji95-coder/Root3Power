using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace R3P.Hivemind.Core.Features.Conduit.Services
{
    public static class RouteService
    {
        public static List<Point3d> BestOrthRoute(Point3d p1, Point3d p2)
        {
            var a = new Point3d(p1.X, p2.Y, p1.Z);
            var b = new Point3d(p2.X, p1.Y, p1.Z);
            double l1 = p1.DistanceTo(a) + a.DistanceTo(p2);
            double l2 = p1.DistanceTo(b) + b.DistanceTo(p2);
            return (l1 <= l2) ? new List<Point3d> { p1, a, p2 } : new List<Point3d> { p1, b, p2 };
        }

        // Obstacle-aware grid A*: coarse but robust; falls back to orth route if blocked
        public static List<Point3d> SmartRoute(Database db, Point3d p1, Point3d p2)
        {
            var cfg = ConfigService.Get(db);
            var layers = (cfg.ObstacleLayers ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
            var obstacles = ObstacleService.GetObstacles(db, layers);
            if (obstacles.Count == 0) return BestOrthRoute(p1, p2);

            double step = Math.Max(cfg.GridStep, 0.1);
            double clearance = Math.Max(cfg.Clearance, 0.0);
            bool allowDiag = cfg.Allow45;

            // Build routing extents
            var ext = new Extents3d(p1, p1); ext.AddPoint(p2);
            foreach (var eObs in obstacles)
            {
                try { ext.AddExtents(eObs); } catch { }
            }
            ext = Geometry.Expand(ext, clearance + step * 3);

            int nx = Math.Max(2, (int)Math.Ceiling((ext.MaxPoint.X - ext.MinPoint.X) / step));
            int ny = Math.Max(2, (int)Math.Ceiling((ext.MaxPoint.Y - ext.MinPoint.Y) / step));

            Func<int, int, bool> inside = (ix, iy) => ix >= 0 && iy >= 0 && ix < nx && iy < ny;
            Func<int, int, Point2d> toPt = (ix, iy) => new Point2d(ext.MinPoint.X + ix * step, ext.MinPoint.Y + iy * step);

            // Blocked test: segment vs obstacle extents expanded by clearance
            Func<Point2d, Point2d, bool> blocked = (a, b) =>
            {
                foreach (var eObs in obstacles)
                {
                    var e = Geometry.Expand(eObs, clearance);
                    if (Geometry.SegmentIntersectsRect(a, b, e)) return true;
                }
                return false;
            };

            // Map start/end to grid
            int sx = (int)Math.Round((p1.X - ext.MinPoint.X) / step);
            int sy = (int)Math.Round((p1.Y - ext.MinPoint.Y) / step);
            int tx = (int)Math.Round((p2.X - ext.MinPoint.X) / step);
            int ty = (int)Math.Round((p2.Y - ext.MinPoint.Y) / step);
            sx = Math.Max(0, Math.Min(nx - 1, sx)); sy = Math.Max(0, Math.Min(ny - 1, sy));
            tx = Math.Max(0, Math.Min(nx - 1, tx)); ty = Math.Max(0, Math.Min(ny - 1, ty));

            var open = new SortedSet<(double f, int n)>(Comparer<(double f, int n)>.Create((a, b) => a.f == b.f ? a.n.CompareTo(b.n) : a.f.CompareTo(b.f)));
            var came = new Dictionary<int, int>();
            var g = new Dictionary<int, double>();
            Func<int, int, int> key = (ix, iy) => iy * nx + ix;
            int sKey = key(sx, sy), tKey = key(tx, ty);
            Func<int, double> h = (n) =>
            {
                int ix = n % nx, iy = n / nx;
                var p = toPt(ix, iy);
                return Math.Abs(p.X - p2.X) + Math.Abs(p.Y - p2.Y);
            };

            g[sKey] = 0.0; open.Add((f: h(sKey), n: sKey));
            int[] dx4 = { 1, -1, 0, 0 };
            int[] dy4 = { 0, 0, 1, -1 };
            int[] dx8 = { 1, 1, 1, -1, -1, -1, 0, 0 };
            int[] dy8 = { 1, 0, -1, 1, 0, -1, 1, -1 };

            while (open.Count > 0)
            {
                var cur = open.Min; open.Remove(cur);
                int n = cur.n; if (n == tKey) break;
                int ix = n % nx, iy = n / nx;
                var p = toPt(ix, iy);
                var dirs = allowDiag ? 8 : 4;
                for (int k = 0; k < dirs; k++)
                {
                    int nx1 = allowDiag ? dx8[k] : dx4[k];
                    int ny1 = allowDiag ? dy8[k] : dy4[k];
                    int jx = ix + nx1, jy = iy + ny1;
                    if (!inside(jx, jy)) continue;
                    var q = toPt(jx, jy);
                    if (blocked(p, q)) continue;
                    int m = key(jx, jy);
                    double tentative = g[n] + p.GetDistanceTo(q);
                    if (!g.TryGetValue(m, out double gm) || tentative < gm)
                    {
                        came[m] = n; g[m] = tentative;
                        open.Add((f: tentative + h(m), n: m));
                    }
                }
            }

            if (!came.ContainsKey(tKey))
                return BestOrthRoute(p1, p2);

            // Reconstruct path
            var path = new List<Point2d>(); int curKey = tKey;
            path.Add(toPt(curKey % nx, curKey / nx));
            while (curKey != sKey)
            {
                curKey = came[curKey];
                path.Add(toPt(curKey % nx, curKey / nx));
            }
            path.Reverse();

            // Simplify to breakpoints (remove collinear nodes)
            var simp = new List<Point3d>();
            for (int i = 0; i < path.Count; i++)
            {
                var pt = path[i];
                if (i == 0 || i == path.Count - 1)
                {
                    simp.Add(new Point3d(pt.X, pt.Y, p1.Z));
                }
                else
                {
                    var a = path[i - 1]; var c = path[i + 1];
                    var v1 = new Point2d(pt.X - a.X, pt.Y - a.Y);
                    var v2 = new Point2d(c.X - pt.X, c.Y - pt.Y);
                    // Keep this point if direction changes
                    if (Math.Abs(v1.X * v2.Y - v1.Y * v2.X) > 1e-9)
                        simp.Add(new Point3d(pt.X, pt.Y, p1.Z));
                }
            }
            if (simp.Count < 2) return BestOrthRoute(p1, p2);
            return simp;
        }
    }
}




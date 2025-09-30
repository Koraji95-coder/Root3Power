using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace R3P.Hivemind.Core.Features.Conduit.Services
{
    public static class ObstacleService
    {
        public static List<Extents3d> GetObstacles(Database db, IEnumerable<string> layerNames)
        {
            var result = new List<Extents3d>();
            var layers = new HashSet<string>((layerNames ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()), StringComparer.InvariantCultureIgnoreCase);
            if (layers.Count == 0) return result;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in btr)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null || !(ent is Curve)) continue;
                    if (layers.Contains(ent.Layer))
                    {
                        try { result.Add(ent.GeometricExtents); } catch { }
                    }
                }
                tr.Commit();
            }
            return result;
        }
    }
}




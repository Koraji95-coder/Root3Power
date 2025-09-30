using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace R3P.Hivemind.Core.Features.Conduit.Services
{
    public static class ConfigService
    {
        public class Config {
            public string Prefix = "P-";
            public int Next = 100;
            public double AllowPercent = 20.0;
            public double RoundInc = 0.0;
            public double TextHeight = 0.18;
            // Routing
            public string ObstacleLayers = ""; // comma-separated
            public double Clearance = 0.0; // drawing units
            public double GridStep = 1.0; // drawing units
            public bool Allow45 = false;
            public double FiberThresholdFt = 328.0; // default 328 ft
        }
        private const string DictName = "R3P_CONFIG";

        public static Config Get(Database db)
        {
            var cfg = new Config();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (nod.Contains(DictName))
                {
                    var d = (DBDictionary)tr.GetObject((ObjectId)nod.GetAt(DictName), OpenMode.ForRead);
                    if (d.Contains("CFG"))
                    {
                        var xr = (Xrecord)tr.GetObject((ObjectId)d.GetAt("CFG"), OpenMode.ForRead);
                        var rb = xr.Data.AsArray();
                        if (rb.Length >= 5)
                        {
                            cfg.Prefix = rb[0].Value as string ?? cfg.Prefix;
                            cfg.Next = Convert.ToInt32(rb[1].Value);
                            cfg.AllowPercent = Convert.ToDouble(rb[2].Value);
                            cfg.RoundInc = Convert.ToDouble(rb[3].Value);
                            cfg.TextHeight = Convert.ToDouble(rb[4].Value);
                        }
                        // Optional extras
                        if (rb.Length >= 6) cfg.ObstacleLayers = rb[5].Value as string ?? cfg.ObstacleLayers;
                        if (rb.Length >= 7) cfg.Clearance = Convert.ToDouble(rb[6].Value);
                        if (rb.Length >= 8) cfg.GridStep = Convert.ToDouble(rb[7].Value);
                        if (rb.Length >= 9) cfg.Allow45 = Convert.ToInt32(rb[8].Value) != 0;
                        if (rb.Length >= 10) cfg.FiberThresholdFt = Convert.ToDouble(rb[9].Value);
                    }
                }
                tr.Commit();
            }
            return cfg;
        }

        public static void Set(Database db, Config cfg)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
                DBDictionary d;
                if (!nod.Contains(DictName)) { d = new DBDictionary(); nod.SetAt(DictName, d); tr.AddNewlyCreatedDBObject(d, true); }
                else d = (DBDictionary)tr.GetObject((ObjectId)nod.GetAt(DictName), OpenMode.ForWrite);

                Xrecord xr;
                if (!d.Contains("CFG")) { xr = new Xrecord(); d.SetAt("CFG", xr); tr.AddNewlyCreatedDBObject(xr, true); }
                else xr = (Xrecord)tr.GetObject((ObjectId)d.GetAt("CFG"), OpenMode.ForWrite);

                xr.Data = new ResultBuffer(
                    new TypedValue((int)DxfCode.Text, cfg.Prefix),
                    new TypedValue((int)DxfCode.Int32, cfg.Next),
                    new TypedValue((int)DxfCode.Real, cfg.AllowPercent),
                    new TypedValue((int)DxfCode.Real, cfg.RoundInc),
                    new TypedValue((int)DxfCode.Real, cfg.TextHeight),
                    new TypedValue((int)DxfCode.Text, cfg.ObstacleLayers ?? ""),
                    new TypedValue((int)DxfCode.Real, cfg.Clearance),
                    new TypedValue((int)DxfCode.Real, cfg.GridStep),
                    new TypedValue((int)DxfCode.Int16, cfg.Allow45 ? 1 : 0),
                    new TypedValue((int)DxfCode.Real, cfg.FiberThresholdFt)
                );
                tr.Commit();
            }
        }
    }
}





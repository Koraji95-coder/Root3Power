using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace R3P.Conduit.Services
{
    internal static class DataService
    {
        private const string ExtKey = "R3P_CONDUIT";

        public static void WriteTagXRecord(Entity ent, Transaction tr, string tag, double raw, double adj, double allowPct, double roundInc)
        {
            if (!ent.IsWriteEnabled) ent.UpgradeOpen();
            if (!ent.ExtensionDictionary.IsValid) ent.CreateExtensionDictionary();
            var xdict = (DBDictionary)tr.GetObject(ent.ExtensionDictionary, OpenMode.ForWrite);
            XRecord xr;
            if (!xdict.Contains(ExtKey)) { xr = new XRecord(); xdict.SetAt(ExtKey, xr); tr.AddNewlyCreatedDBObject(xr, true); }
            else xr = (XRecord)tr.GetObject((ObjectId)xdict.GetAt(ExtKey), OpenMode.ForWrite);

            xr.Data = new ResultBuffer(
                new TypedValue((int)DxfCode.Text, tag),
                new TypedValue((int)DxfCode.Real, raw),
                new TypedValue((int)DxfCode.Real, adj),
                new TypedValue((int)DxfCode.Real, allowPct),
                new TypedValue((int)DxfCode.Real, roundInc)
            );
        }

        public static bool ReadTagXRecord(Entity ent, Transaction tr, out string tag, out double raw, out double adj, out double allowPct, out double roundInc)
        {
            tag = string.Empty; raw = adj = allowPct = roundInc = 0.0;
            if (!ent.ExtensionDictionary.IsValid) return false;
            var xdict = (DBDictionary)tr.GetObject(ent.ExtensionDictionary, OpenMode.ForRead);
            if (!xdict.Contains(ExtKey)) return false;
            var xr = (XRecord)tr.GetObject((ObjectId)xdict.GetAt(ExtKey), OpenMode.ForRead);
            var rb = xr.Data.AsArray();
            if (rb.Length < 5) return false;
            tag = rb[0].Value as string ?? "";
            raw = Convert.ToDouble(rb[1].Value);
            adj = Convert.ToDouble(rb[2].Value);
            allowPct = Convert.ToDouble(rb[3].Value);
            roundInc = Convert.ToDouble(rb[4].Value);
            return true;
        }

        public static void RemoveTagXRecord(Entity ent, Transaction tr)
        {
            if (!ent.ExtensionDictionary.IsValid) return;
            var xdict = (DBDictionary)tr.GetObject(ent.ExtensionDictionary, OpenMode.ForWrite);
            if (xdict.Contains(ExtKey)) xdict.Remove(ExtKey);
        }
    }
}


using System;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace R3P.Hivemind.Core.Features.Conduit.Services
{
    public static class ReactorService
    {
        private static bool _attached;

        public static void AttachToActive()
        {
            if (_attached) return;
            var dm = AcadApp.DocumentManager;
            if (dm.MdiActiveDocument != null)
            {
                var db = dm.MdiActiveDocument.Database;
                db.ObjectModified += Db_ObjectModified;
                db.ObjectErased += Db_ObjectErased;
                dm.DocumentActivated += Dm_DocumentActivated;
                _attached = true;
            }
        }

        public static void Detach()
        {
            var dm = AcadApp.DocumentManager;
            if (dm.MdiActiveDocument != null)
            {
                var db = dm.MdiActiveDocument.Database;
                db.ObjectModified -= Db_ObjectModified;
                db.ObjectErased -= Db_ObjectErased;
                dm.DocumentActivated -= Dm_DocumentActivated;
            }
            _attached = false;
        }

        private static void Dm_DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            // Reattach to new active doc DB
            Detach();
            AttachToActive();
            R3P.UiRefreshList();
        }

        private static void Db_ObjectErased(object sender, ObjectErasedEventArgs e)
        {
            // If one of our tagged entities is erased, refresh the UI list
            if (e.Erased) R3P.UiRefreshList();
        }

        private static void Db_ObjectModified(object sender, ObjectEventArgs e)
        {
            try
            {
                if (!(e.DBObject is Entity ent)) return;
                // Only recompute if it's one we tagged
                var db = ent.Database;
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    if (!DataService.ReadTagXRecord(ent, tr, out string tag, out double raw, out double adj, out double allow, out double round))
                        return;

                    if (!(ent is Curve curve)) return;
                    double newRaw = Utils.CurveLength(curve);
                    double newAdj = Utils.ApplyAllowance(newRaw, allow, round);

                    if (Math.Abs(newAdj - adj) > 1e-6)
                    {
                        // Upgrade and write back new values
                        tr.Abort(); // end read-only tr
                        using (var trw = db.TransactionManager.StartTransaction())
                        {
                            var entW = (Entity)trw.GetObject(ent.ObjectId, OpenMode.ForRead);
                            DataService.WriteTagXRecord(entW, trw, tag, newRaw, newAdj, allow, round);
                            trw.Commit();
                        }
                        R3P.UiRefreshList();
                    }
                }
            }
            catch
            {
                // swallow to avoid breaking user commands
            }
        }
    }
}





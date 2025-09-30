using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using R3P.Conduit.Model;

namespace R3P.Conduit.Services
{
    internal static class TableService
    {
        public static void InsertScheduleTable(List<ConduitItem> items)
        {
            var doc = Application.DocumentManager.MdiActiveDocument; if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            var ppr = ed.GetPoint("\nPick table insertion point: "); if (ppr.Status != PromptStatus.OK) return;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var table = new Table();
                // Try to use Root3Power table style if present
                var tdict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead);
                if (tdict.Contains("Root3Power"))
                    table.TableStyle = (ObjectId)tdict.GetAt("Root3Power");
                else
                    table.TableStyle = db.Tablestyle;
                table.NumColumns = 6;
                table.NumRows = items.Count + 1;
                table.SetRowHeight(0.3);
                table.SetColumnWidth(1.5);
                table.Position = ppr.Value;

                // Headers
                table.SetTextHeight(0, 0, 0.18);
                table.SetTextString(0, 0, "Tag");
                table.SetTextString(0, 1, "Raw");
                table.SetTextString(0, 2, "Adjusted");
                table.SetTextString(0, 3, "Ft-In");
                table.SetTextString(0, 4, "Handle");
                table.SetTextString(0, 5, "Hint");

                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i]; int r = i + 1;
                    table.SetTextString(r, 0, it.Tag);
                    table.SetTextString(r, 1, it.Raw.ToString("0.###"));
                    table.SetTextString(r, 2, it.Adjusted.ToString("0.###"));
                    table.SetTextString(r, 3, it.FtIn);
                    table.SetTextString(r, 4, it.Handle);
                    table.SetTextString(r, 5, it.Hint ?? "");
                }

                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                btr.AppendEntity(table); tr.AddNewlyCreatedDBObject(table, true);
                tr.Commit();
            }
        }
    }
}

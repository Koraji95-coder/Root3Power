using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using R3P.Hivemind.Core.Features.Conduit.Model;

namespace R3P.Hivemind.Core.Features.Conduit.Services
{
    public static class TableService
    {
        public static void InsertScheduleTable(List<ConduitItem> items)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument; if (doc == null) return;
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
                table.Cells[0, 0].TextString = "Tag";
                table.Cells[0, 1].TextString = "Raw";
                table.Cells[0, 2].TextString = "Adjusted";
                table.Cells[0, 3].TextString = "Ft-In";
                table.Cells[0, 4].TextString = "Handle";
                table.Cells[0, 5].TextString = "Hint";

                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i]; int r = i + 1;
                    table.Cells[r, 0].TextString = it.Tag;
                    table.Cells[r, 1].TextString = it.Raw.ToString("0.###");
                    table.Cells[r, 2].TextString = it.Adjusted.ToString("0.###");
                    table.Cells[r, 3].TextString = it.FtIn;
                    table.Cells[r, 4].TextString = it.Handle;
                    table.Cells[r, 5].TextString = it.Hint ?? "";
                }

                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                btr.AppendEntity(table); tr.AddNewlyCreatedDBObject(table, true);
                tr.Commit();
            }
        }
    }
}





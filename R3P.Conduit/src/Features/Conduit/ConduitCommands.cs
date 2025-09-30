using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Microsoft.Win32;
using R3P.Hivemind.Core.Features.Conduit.Services;
using R3P.Hivemind.Features.Conduit.Services;
using R3P.Hivemind.UI.Wpf;
using R3P.Hivemind.Core.Diagnostics;
using Model = R3P.Hivemind.Core.Features.Conduit.Model;

namespace R3P.Hivemind.Features.Conduit
{
    // App entry: creates palettes and attaches reactors
    public class R3PApp : IExtensionApplication
    {
        internal static PaletteSet Palette;
        internal static ConduitManagerView ManagerView;
        internal static OneLineBuilderView OneLineView;
        internal static CrashConsoleView CrashConsoleView;

        public void Initialize()
        {
            Palette = new PaletteSet("Root3Power Tools")
            {
                Style = PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.Snappable,
                DockEnabled = (DockSides)((int)DockSides.Left + (int)DockSides.Right)
            };

            ManagerView = new ConduitManagerView();
            OneLineView = new OneLineBuilderView();
            Palette.AddVisual("Conduit Manager", ManagerView);
            Palette.AddVisual("Build One-Line (stub)", OneLineView);
            CrashConsoleView = new CrashConsoleView();
            Palette.AddVisual("Diagnostics Console", CrashConsoleView);

            ReactorService.AttachToActive();
            CrashLogWatcher.Start();
        }

        public void Terminate()
        {
            CrashLogWatcher.Stop();
            ReactorService.Detach();
        }
    }

    public static class R3P
    {
        // ---- Public Commands ----
        [CommandMethod("R3P")]
        public static void OpenPalette()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument; if (doc == null) return;
            R3PApp.Palette.Visible = true;
            UiRefreshList();
            R3PApp.ManagerView.LoadConfigToUi(ConfigService.Get(doc.Database));
        }

        [CommandMethod("R3P_WIRETAG")]
        public static void CmdWireTag() => MeasureAndTagSelection();

        [CommandMethod("R3P_ROUTE2")]
        public static void CmdRoute2() => Route2AndTag();

        [CommandMethod("R3P_TABLE")]
        public static void CmdInsertTable() => TableService.InsertScheduleTable(GetAllItems());

        [CommandMethod("R3P_EXCEL")]
        public static void CmdExportExcel() => ExcelService.ExportToExcel(GetAllItems());

        // ---- UI hooks ----
        internal static void UiRefreshList()
        {
            R3PApp.ManagerView.SetItems(GetAllItems());
        }

        internal static void UiExportCsv()
        {
            var items = GetAllItems();
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", FileName = "conduit_items.csv" };
            if (dlg.ShowDialog() == true)
            {
                ExcelService.ExportCsv(items, dlg.FileName);
            }
        }

        internal static void UiPlaceTagForSelected(Model.ConduitItem selected)
        {
            if (selected == null) return;
            var doc = AcadApp.DocumentManager.MdiActiveDocument; var ed = doc.Editor; var db = doc.Database;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(selected.Id, OpenMode.ForRead) as Entity; if (ent == null) return;
                var ppr = ed.GetPoint("\nPick tag location: "); if (ppr.Status != PromptStatus.OK) return;
                var cfg = ConfigService.Get(db);
                var txt = new DBText
                {
                    Position = ppr.Value,
                    Height = cfg.TextHeight,
                    TextString = $"{selected.Tag}  {selected.FtIn}  [{selected.Adjusted:0.###}]",
                    Layer = (string)AcadApp.GetSystemVariable("CLAYER"),
                };
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                btr.AppendEntity(txt); tr.AddNewlyCreatedDBObject(txt, true);
                tr.Commit();
            }
        }

        internal static void UiRemoveTag(Model.ConduitItem selected)
        {
            if (selected == null) return;
            var doc = AcadApp.DocumentManager.MdiActiveDocument; var db = doc.Database;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(selected.Id, OpenMode.ForRead) as Entity;
                if (ent != null) { DataService.RemoveTagXRecord(ent, tr); tr.Commit(); }
            }
            UiRefreshList();
        }

        internal static void UiSaveConfig(string prefix, int next, double allowPct, double roundInc, double textHeight)
        {
            var db = AcadApp.DocumentManager.MdiActiveDocument.Database;
            ConfigService.Set(db, new ConfigService.Config { Prefix = prefix, Next = next, AllowPercent = allowPct, RoundInc = roundInc, TextHeight = textHeight });
        }

        internal static ConfigService.Config UiGetConfig()
        {
            var db = AcadApp.DocumentManager.MdiActiveDocument.Database;
            return ConfigService.Get(db);
        }

        // ---- Core operations ----
        public static void MeasureAndTagSelection()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument; if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LINE,ARC,LWPOLYLINE,POLYLINE,SPLINE,ELLIPSE") });
            var psr = ed.GetSelection(filter);
            if (psr.Status != PromptStatus.OK) { ed.WriteMessage("\nNothing selected."); return; }
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var cfg = ConfigService.Get(db); int count = 0;
                foreach (SelectedObject so in psr.Value)
                {
                    if (so == null) continue;
                    var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity; if (!(ent is Curve curve)) continue;
                    double raw = MeasurementService.CurveLength(curve);
                    double adj = MeasurementService.ApplyAllowance(raw, cfg.AllowPercent, cfg.RoundInc);
                    string tag = TagService.NextTag(db, tr, cfg);
                    DataService.WriteTagXRecord(ent, tr, tag, raw, adj, cfg.AllowPercent, cfg.RoundInc);
                    count++;
                }
                tr.Commit(); ed.WriteMessage($"\nTagged {count} item(s).\n");
            }
            UiRefreshList();
        }

        public static void Route2AndTag()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument; if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            var p1 = ed.GetPoint("\nPick first point: "); if (p1.Status != PromptStatus.OK) return;
            var p2 = ed.GetPoint("\nPick second point: "); if (p2.Status != PromptStatus.OK) return;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var cfg = ConfigService.Get(db);
                var pts = RouteService.SmartRoute(db, p1.Value, p2.Value);
                var pl = new Polyline();
                for (int i = 0; i < pts.Count; i++) pl.AddVertexAt(i, new Point2d(pts[i].X, pts[i].Y), 0, 0, 0);
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                btr.AppendEntity(pl); tr.AddNewlyCreatedDBObject(pl, true);
                double raw = MeasurementService.CurveLength(pl);
                double adj = MeasurementService.ApplyAllowance(raw, cfg.AllowPercent, cfg.RoundInc);
                string tag = TagService.NextTag(db, tr, cfg);
                DataService.WriteTagXRecord(pl, tr, tag, raw, adj, cfg.AllowPercent, cfg.RoundInc);
                tr.Commit(); ed.WriteMessage($"\nRouted + tagged: {tag}\n");
            }
            UiRefreshList();
        }

        // ---- Query ----
        public static List<Model.ConduitItem> GetAllItems()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument; if (doc == null) return new List<Model.ConduitItem>();
            var ed = doc.Editor; var db = doc.Database; var items = new List<Model.ConduitItem>();
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LINE,ARC,LWPOLYLINE,POLYLINE,SPLINE,ELLIPSE") });
            var psr = ed.SelectAll(filter); if (psr.Status != PromptStatus.OK) return items;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject so in psr.Value)
                {
                    var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity; if (ent == null) continue;
                    if (DataService.ReadTagXRecord(ent, tr, out string tag, out double raw, out double adj, out double allow, out double round))
                    {
                        var h = ent.Handle.ToString();
                        var ftin = MeasurementService.FormatFtIn(adj);
                        string hint = null;
                        var feet = MeasurementService.ToFeet(adj);
                        var cfg = ConfigService.Get(db);
                        if (feet > cfg.FiberThresholdFt) hint = $"> {cfg.FiberThresholdFt:0} ft — fiber recommended";
                        items.Add(new Model.ConduitItem { Id = ent.ObjectId, Handle = h, Tag = tag, Raw = raw, Adjusted = adj, FtIn = ftin, Hint = hint });
                    }
                }
                tr.Commit();
            }
            return items.OrderBy(i => i.Tag, StringComparer.InvariantCultureIgnoreCase).ToList();
        }
    }
}









using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using R3P.Hivemind.Core.Features.Conduit.Model;

namespace R3P.Hivemind.Core.Features.Conduit.Services
{
    public static class ExcelService
    {
        public static void ExportCsv(List<ConduitItem> items, string fileName)
        {
            using (var sw = new StreamWriter(fileName))
            {
                sw.WriteLine("Tag,Raw,Adjusted,FtIn,Handle,Hint");
                foreach (var it in items)
                    sw.WriteLine($"{it.Tag},{it.Raw:0.###},{it.Adjusted:0.###},{it.FtIn},{it.Handle},{(it.Hint ?? "").Replace(',', ';')}");
            }
        }

[SupportedOSPlatform("windows")]
        public static void ExportToExcel(List<ConduitItem> items)
        {
            try
            {
                var type = Type.GetTypeFromProgID("Excel.Application");
                if (type == null) { MessageBox.Show("Excel not installed. Exporting CSV instead.");
                    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "conduit_schedule.csv");
                    ExportCsv(items, path); return; }

                dynamic excel = Activator.CreateInstance(type);
                excel.Visible = true;
                dynamic wb = excel.Workbooks.Add();
                dynamic ws = wb.ActiveSheet;
                ws.Name = "Conduit Schedule";
                ws.Cells[1, 1].Value = "Tag";
                ws.Cells[1, 2].Value = "Raw";
                ws.Cells[1, 3].Value = "Adjusted";
                ws.Cells[1, 4].Value = "Ft-In";
                ws.Cells[1, 5].Value = "Handle";
                ws.Cells[1, 6].Value = "Hint";
                int r = 2;
                foreach (var it in items)
                {
                    ws.Cells[r, 1].Value = it.Tag;
                    ws.Cells[r, 2].Value = it.Raw;
                    ws.Cells[r, 3].Value = it.Adjusted;
                    ws.Cells[r, 4].Value = it.FtIn;
                    ws.Cells[r, 5].Value = it.Handle;
                    ws.Cells[r, 6].Value = it.Hint ?? "";
                    r++;
                }
                ws.Columns.AutoFit();
            }
            catch (COMException)
            {
                MessageBox.Show("Excel COM error. Exporting CSV on Desktop.");
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "conduit_schedule.csv");
                ExportCsv(items, path);
            }
        }
    }
}




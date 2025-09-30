using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using R3P.Conduit.Model;
using R3P.Conduit.Services;

namespace R3P.Conduit.UI.Wpf
{
    public partial class ConduitManagerView : UserControl
    {
        public ConduitManagerView()
        {
            InitializeComponent();
            BtnSave.Click += (_, __) => SaveConfig();
            BtnMeasureAdd.Click += (_, __) => R3P.MeasureAndTagSelection();
            BtnRoute2.Click += (_, __) => R3P.Route2AndTag();
            BtnPlace.Click += (_, __) => R3P.UiPlaceTagForSelected(LbItems.SelectedItem as ConduitItem);
            BtnRemove.Click += (_, __) => R3P.UiRemoveTag(LbItems.SelectedItem as ConduitItem);
            BtnRefresh.Click += (_, __) => R3P.UiRefreshList();
            BtnCsv.Click += (_, __) => R3P.UiExportCsv();
            BtnExcel.Click += (_, __) => ExcelService.ExportToExcel(GetItems());
            BtnTable.Click += (_, __) => TableService.InsertScheduleTable(GetItems());
        }

        public void LoadConfigToUi(ConfigService.Config cfg)
        {
            TbPrefix.Text = cfg.Prefix;
            TbNext.Text = cfg.Next.ToString();
            TbAllow.Text = cfg.AllowPercent.ToString("0.##");
            TbRound.Text = cfg.RoundInc.ToString("0.###");
            TbText.Text = cfg.TextHeight.ToString("0.##");
            TbLayers.Text = cfg.ObstacleLayers ?? string.Empty;
            TbClearance.Text = cfg.Clearance.ToString("0.###");
            TbGrid.Text = cfg.GridStep.ToString("0.###");
            CbAllow45.IsChecked = cfg.Allow45;
            TbFiber.Text = cfg.FiberThresholdFt.ToString("0.##");
        }

        void SaveConfig()
        {
            try
            {
                string prefix = TbPrefix.Text;
                int next = int.Parse(TbNext.Text);
                double allow = double.Parse(TbAllow.Text);
                double round = double.Parse(TbRound.Text);
                double text = double.Parse(TbText.Text);
                // Save core first
                R3P.UiSaveConfig(prefix, next, allow, round, text);
                // Then pull and extend with routing fields
                var db = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Database;
                var cfg = ConfigService.Get(db);
                cfg.ObstacleLayers = TbLayers.Text;
                cfg.Clearance = double.Parse(TbClearance.Text);
                cfg.GridStep = double.Parse(TbGrid.Text);
                cfg.Allow45 = CbAllow45.IsChecked == true;
                cfg.FiberThresholdFt = double.Parse(TbFiber.Text);
                ConfigService.Set(db, cfg);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Invalid settings: {ex.Message}");
            }
        }

        public void SetItems(List<ConduitItem> items)
        {
            LbItems.ItemsSource = items;
        }

        List<ConduitItem> GetItems() => new List<ConduitItem>(LbItems.ItemsSource as IEnumerable<ConduitItem> ?? Array.Empty<ConduitItem>());
    }
}

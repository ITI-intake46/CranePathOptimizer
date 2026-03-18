using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using WPF_IFC_test1.ViewModels;
using WPF_IFC_test1.Utilities;

namespace WPF_IFC_test1
{
    public partial class MainWindow
    {
        #region Building Analysis

        private void AnalyzeBuilding(string filePath)
        {
            try
            {
                _analyzer.Analyze(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Analysis: {ex}");
                throw;
            }
        }

        private void UpdateAnalysisUI()
        {
            if (!_analyzer.IsAnalyzed) return;

            Dispatcher.Invoke(() =>
            {
                _viewModel.BuildingName = _analyzer.BuildingName;

                if (_viewModel.UnitSystem == "Imperial")
                {
                    _viewModel.BuildingArea = UnitConverter.ConvertArea(_analyzer.BuildingArea, "ft²");
                    _viewModel.AreaUnit = "ft²";
                }
                else
                {
                    _viewModel.BuildingArea = _analyzer.BuildingArea;
                    _viewModel.AreaUnit = "m²";
                }

                _viewModel.BuildingHeight = UnitConverter.ConvertLength(
                    _analyzer.BuildingHeight, _viewModel.SelectedUnit);
                _viewModel.NumberOfStoreys = _analyzer.NumberOfStoreys;
            });
        }

        private void ShowAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (!_analyzer.IsAnalyzed)
            {
                MessageBox.Show("No analysis yet.");
                return;
            }

            var summary = _analyzer.GetSummary();
            summary += $"\nTotal Elements: {_viewModel.Elements.Count}";
            summary += $"\n🔴 Heavy (>5000 kg): {_viewModel.Elements.Count(el => el.Weight > 5000)}";
            summary += $"\n🟠 Medium (3000–5000 kg): {_viewModel.Elements.Count(el => el.Weight > 3000 && el.Weight <= 5000)}";

            if (_craneManager?.TotalCranes > 0)
            {
                summary += $"\n🏗️ Cranes: {_craneManager.TotalCranes}";
                foreach (var crane in _craneManager.Cranes)
                    summary += $"\n  - {crane.Name}: Reach {crane.Parameters.HookRadius:F1} m";
            }

            if (_zoneController?.Manager?.HasSiteBoundary == true)
            {
                summary += $"\n\n🗺️ Zone Analysis:";
                summary += $"\n  Site area: {_zoneController.Manager.TotalSiteArea:F0} m²";
                summary += $"\n  Zones: {_zoneController.Manager.Zones.Count}";
                summary += $"\n  Forbidden ground coverage: {_zoneController.Manager.ForbiddenGroundCoverage():F1}%";
            }

            MessageBox.Show(summary, "Building Analysis");
        }

        private void ExportAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (!_analyzer.IsAnalyzed)
            {
                MessageBox.Show("No analysis yet.");
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Text (*.txt)|*.txt",
                FileName = $"{_analyzer.BuildingName}_Analysis.txt"
            };
            
            if (dlg.ShowDialog() != true) return;

            try
            {
                var content = _analyzer.GetSummary() +
                              $"\nElements: {_viewModel.Elements.Count}" +
                              $"\nExport: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                File.WriteAllText(dlg.FileName, content);
                StatusText.Text = "Analysis exported";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}");
            }
        }

        #endregion
    }
}

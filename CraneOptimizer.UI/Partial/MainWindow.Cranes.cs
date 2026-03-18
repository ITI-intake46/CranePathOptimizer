using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.Models;
using WPF_IFC_test1.Utilities;
using WPF_IFC_test1.ZoneModule.Drawing;
using CraneDragHandler = WPF_IFC_test1.Utilities.CraneDragHandler;
using CraneInstance = WPF_IFC_test1.Utilities.CraneInstance;
using CraneParametersBase = WPF_IFC_test1.Models.CraneParametersBase;
using CraneType = WPF_IFC_test1.Models.CraneType;

namespace WPF_IFC_test1
{
    public partial class MainWindow
    {
        #region Crane Manager Initialization

        private void InitializeCraneManager()
        {
            _craneManager = new CraneManager();
            _craneManager.StatusChanged += OnCraneManagerStatusChanged;
            _craneManager.CraneSelected += OnCraneSelected;
            _craneManager.PropertyChanged += OnCraneManagerPropertyChanged;
        }

        #endregion

        #region Crane Manager Events

        private void OnCraneManagerStatusChanged(string status)
            => Dispatcher.Invoke(() => StatusText.Text = status);

        private void OnCraneManagerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CraneManager.TotalCranes)) return;

            Dispatcher.Invoke(() =>
            {
                if (_craneManager.TotalCranes > 0)
                {
                    CraneStatusIndicator.Visibility = Visibility.Visible;
                    CraneStatusText.Text = $"{_craneManager.TotalCranes} Crane(s) Active";
                }
                else
                {
                    CraneStatusIndicator.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void OnCraneSelected(CraneInstance crane)
        {
            Dispatcher.Invoke(() =>
            {
                if (crane != null)
                {
                    SitePlanner.RefreshCraneParameters(crane);
                    SitePlanner.SwitchToCranes();
                    StatusText.Text = $"✓ Selected: {crane.Name}";
                }
            });
        }

        #endregion

        #region Add Crane

        private void AddTowerCrane_Click(object sender, RoutedEventArgs e)
            => AddCrane(CraneType.TowerCrane);

        private void AddMobileCrane_Click(object sender, RoutedEventArgs e)
            => AddCrane(CraneType.MobileCrane);

        private void AddCrane(CraneType craneType)
        {
            if (string.IsNullOrEmpty(_currentIfcFilePath))
            {
                MessageBox.Show("Please load a building IFC file first.");
                return;
            }

            try
            {
                var crane = _craneManager.AddCrane(craneType);
                if (crane == null) return;

                PlaceCraneNearBuilding(crane.Parameters);
                IfcViewer.Children.Add(crane.Visual);

                // Initialize drag handler if needed
                if (_craneDragHandler == null)
                {
                    _craneDragHandler = new CraneDragHandler(IfcViewer, _craneManager);
                    _craneDragHandler.StatusChanged += s =>
                        Dispatcher.Invoke(() => StatusText.Text = s);
                }
                _craneDragHandler.NotifyCraneAdded();

                SitePlanner.SetCraneDataContext(_craneManager);
                SitePlanner.RefreshCraneParameters(crane);

                // Reset view to show the crane
                Dispatcher.BeginInvoke(new Action(() => ResetToFitAll()),
                    System.Windows.Threading.DispatcherPriority.Loaded);

                // Register with undo/redo system
                RegisterCraneUndoAction(crane);

                StatusText.Text = $"✓ {crane.Name} placed at ({crane.Parameters.PositionX:F0}, {crane.Parameters.PositionY:F0})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add crane:\n{ex.Message}");
            }
        }

        private void RegisterCraneUndoAction(CraneInstance crane)
        {
            var capturedCrane = crane;
            var capturedVisual = crane.Visual;

            _zoneController.UndoMgr.Push(
                $"Add {crane.Name}",
                new UndoAction
                {
                    Type = UndoType.Custom,
                    CustomUndo = () =>
                    {
                        IfcViewer.Children.Remove(capturedVisual);
                        _craneManager.Cranes.Remove(capturedCrane);
                    },
                    CustomRedo = () =>
                    {
                        _craneManager.Cranes.Add(capturedCrane);
                        if (!IfcViewer.Children.Contains(capturedVisual))
                            IfcViewer.Children.Add(capturedVisual);
                    }
                });
        }

        #endregion

        #region Crane Placement

        private void PlaceCraneNearBuilding(CraneParametersBase parameters)
        {
            try
            {
                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;
                double minZ = double.MaxValue;
                bool hasPoints = false;

                foreach (var visual in _loadedIfcModels)
                {
                    IEnumerable<Point3D> pts = null;
                    if (visual.Content is Model3DGroup grp)
                    {
                        pts = grp.Children.OfType<GeometryModel3D>()
                            .Where(g => g.Geometry is MeshGeometry3D)
                            .SelectMany(g => ((MeshGeometry3D)g.Geometry).Positions);
                    }
                    else if (visual.Content is GeometryModel3D gm && gm.Geometry is MeshGeometry3D m)
                    {
                        pts = m.Positions;
                    }

                    if (pts == null) continue;

                    foreach (var pt in pts)
                    {
                        minX = Math.Min(minX, pt.X);
                        maxX = Math.Max(maxX, pt.X);
                        minY = Math.Min(minY, pt.Y);
                        maxY = Math.Max(maxY, pt.Y);
                        minZ = Math.Min(minZ, pt.Z);
                        hasPoints = true;
                    }
                }

                if (!hasPoints)
                {
                    parameters.PositionX = 20;
                    parameters.PositionY = 20;
                    parameters.PositionZ = 0;
                    return;
                }

                // Place crane at an angle based on crane count
                double angle = (_craneManager.TotalCranes - 1) * 90.0 * Math.PI / 180.0;
                double reach = Math.Max(maxX - minX, maxY - minY) / 2 + 15;

                parameters.PositionX = (minX + maxX) / 2 + reach * Math.Cos(angle);
                parameters.PositionY = (minY + maxY) / 2 + reach * Math.Sin(angle);
                parameters.PositionZ = minZ;
            }
            catch
            {
                parameters.PositionX = 20;
                parameters.PositionY = 20;
                parameters.PositionZ = 0;
            }
        }

        #endregion

        #region Remove Crane

        private void RemoveSelectedCrane_Click(object sender, RoutedEventArgs e)
        {
            if (_craneManager.SelectedCrane != null)
            {
                IfcViewer.Children.Remove(_craneManager.SelectedCrane.Visual);
                _craneManager.RemoveSelectedCrane();
            }
        }

        private void RemoveAllCranes_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Remove all cranes?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                foreach (var c in _craneManager.Cranes)
                    IfcViewer.Children.Remove(c.Visual);
                _craneManager.RemoveAllCranes();
            }
        }

        private void RemoveExistingCranes()
        {
            try
            {
                if (_craneManager != null)
                {
                    foreach (var c in _craneManager.Cranes.ToList())
                        IfcViewer.Children.Remove(c.Visual);
                }
                _craneManager?.RemoveAllCranes();
                _craneDragHandler?.Detach();
                _craneDragHandler = null;
                CraneStatusIndicator.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private void CloseCranePanel_Click(object sender, RoutedEventArgs e)
            => SitePlanner.SwitchToZones();

        #endregion

        #region Crane IFC Loading and Merging

        private void LoadCrane_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentIfcFilePath))
            {
                MessageBox.Show("Load a building IFC first.");
                return;
            }

            var dlg = new OpenFileDialog { Filter = "IFC files (*.ifc)|*.ifc" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                Cursor = Cursors.Wait;
                var posDialog = new CranePositionDialog();
                Vector3D offset = default;

                if (posDialog.ShowDialog() == true)
                {
                    offset = new Vector3D(
                        posDialog.PositionX,
                        posDialog.PositionY,
                        posDialog.PositionZ);
                }

                LoadIfcFileToViewer(dlg.FileName, offset);
                StatusText.Text = $"✓ Crane loaded: {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed:\n{ex.Message}");
            }
            finally
            {
                Cursor = Cursors.Arrow;
            }
        }

        private void MergeFiles_Click(object sender, RoutedEventArgs e)
        {
            var buildingDlg = new OpenFileDialog
            {
                Filter = "IFC files (*.ifc)|*.ifc",
                Title = "Building IFC"
            };
            if (buildingDlg.ShowDialog() != true) return;

            var craneDlg = new OpenFileDialog
            {
                Filter = "IFC files (*.ifc)|*.ifc",
                Title = "Crane IFC"
            };
            if (craneDlg.ShowDialog() != true) return;

            var saveDlg = new SaveFileDialog
            {
                Filter = "IFC files (*.ifc)|*.ifc",
                FileName = "Merged.ifc"
            };
            if (saveDlg.ShowDialog() != true) return;

            try
            {
                Cursor = Cursors.Wait;

                if (IfcMerger.MergeBuildingAndCrane(
                    buildingDlg.FileName,
                    craneDlg.FileName,
                    saveDlg.FileName))
                {
                    if (MessageBox.Show("Merged! Load now?", "Done", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        LoadIFCFile(saveDlg.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Merge failed:\n{ex.Message}");
            }
            finally
            {
                Cursor = Cursors.Arrow;
            }
        }

        #endregion
    }
}

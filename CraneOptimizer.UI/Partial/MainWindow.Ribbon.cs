using System;
using System.Linq;
using System.Windows;
using WPF_IFC_test1.Dialogs;
using WPF_IFC_test1.Main_Application_UserControl;
using WPF_IFC_test1.Main_Application_UserControl.Ribbon_Tabs;
using WPF_IFC_test1.ZoneModule.Drawing;
using WPF_IFC_test1.ZoneModule.UI;
using CraneType = WPF_IFC_test1.Models.CraneType;

namespace WPF_IFC_test1
{
    public partial class MainWindow
    {
        #region Ribbon Event Wiring

        private void WireRibbonEvents()
        {
            if (MainRibbon == null) return;

            WireProjectEvents();
            WireIFCEvents();
            WireDXFEvents();
            WireCraneEvents();
            WireViewEvents();
            WireZoneEvents();
            WireDrawingEvents();
        }

        private void WireProjectEvents()
        {
            MainRibbon.OnSaveProject += () => SaveProject_Click(null, null);
        }

        private void WireIFCEvents()
        {
            MainRibbon.OnLoadIFC += () => LoadIFC_Click(null, null);
            MainRibbon.OnMergeIFC += () => MergeFiles_Click(null, null);
            MainRibbon.OnToggleCOG += ToggleCOGMarkers;
            MainRibbon.OnFilterElements += OpenFilterElementsDialog;
        }

        private void WireDXFEvents()
        {
            MainRibbon.OnLoadDXF += () => _zoneController.LoadDxf();
            MainRibbon.OnAlignDXF += () => _zoneController.AlignDxf();
        }

        private void WireCraneEvents()
        {
            MainRibbon.OnAddTowerCrane += () => AddCrane(CraneType.TowerCrane);
            MainRibbon.OnAddMobileCrane += () => AddCrane(CraneType.MobileCrane);
            MainRibbon.OnRemoveCrane += () => RemoveSelectedCrane_Click(null, null);
            MainRibbon.OnRemoveAllCranes += () => RemoveAllCranes_Click(null, null);

            // ── NEW: Load chart import ────────────────────────────────────────
            MainRibbon.OnImportLoadChart += OpenLoadChartImportDialog;
            MainRibbon.OnViewLoadCharts += OpenLoadChartViewer;

            // ── NEW: Catalog, Compare, Lift Plan, Optimizer ───────────────────
            MainRibbon.OnOpenCatalog   += OpenCraneCatalogBrowser;
            MainRibbon.OnCompareCharts += OpenLoadChartCompareWindow;
            MainRibbon.OnOpenLiftPlan  += OpenLiftPlanWindow;
            MainRibbon.OnRunOptimizer  += OpenCraneOptimizerDialog;
        }

        private void WireViewEvents()
        {
            MainRibbon.OnFitToSite += () => ResetToFitAll();
            MainRibbon.OnToggleGrid += () => _zoneController.ToggleGrid();
            MainRibbon.OnToggleFlatZones += () => _zoneController.ToggleFlatMode();

            MainRibbon.OnEnterPlanView += () =>
            {
                _zoneController.EnterPlanView();
                StatusText.Text = "📐 Plan View";
            };
            MainRibbon.OnReturnTo3D += () => _zoneController.ReturnTo3DView();
        }

        private void WireZoneEvents()
        {
            // Auto-hide draw tools panel when drawing finishes
            // Guard: if ShapeInputController just took over (tool switch), keep panel open
            _zoneController.Handler.DrawingEnded += () =>
                Dispatcher.Invoke(() =>
                {
                    if (_shapeCtrl?.IsActive != true)
                        MainRibbon?.ZoneTab?.HideDrawTools();
                });

            // Re-show draw tools when drawing starts
            _zoneController.Handler.DrawingStarted += (zt, tool) =>
                Dispatcher.Invoke(() => MainRibbon?.ZoneTab?.ShowDrawTools());

            // Bug 3 fix: un-toggle buttons when StartDraw rejects (no site boundary drawn yet)
            _zoneController.Handler.DrawingRejected += () =>
                Dispatcher.Invoke(() => MainRibbon?.ZoneTab?.HideDrawTools());

            // Bug 3 fix: disable Allowed/Forbidden buttons until site boundary exists
            _zoneController.Manager.ZonesChanged += () =>
                Dispatcher.Invoke(() =>
                    MainRibbon?.ZoneTab?.SetSiteBoundaryExists(
                        _zoneController.Manager.HasSiteBoundary));

            MainRibbon.OnZoneAction += HandleZoneAction;
        }

        private void WireDrawingEvents()
        {
            // Bug 4 fix: wire Measure ribbon button to MeasureTool
            // Old code called CancelAllTools() which fired DrawingEnded → HideDrawTools()
            MainRibbon.OnStartMeasure += () =>
            {
                _shapeCtrl?.Cancel();
                _zoneController?.Handler?.SilentCancelDraw();  // clear ZDH state without firing DrawingEnded
                Overlay?.ClearMeasure();
                _measureTool?.Start();
            };
            MainRibbon.OnCancelMeasure += () =>
                _measureTool?.Cancel();

            // Show result on the ribbon button label
            _measureTool.MeasureCompleted += (metres, _) =>
                Dispatcher.Invoke(() => MainRibbon?.NotifyMeasureResult(metres));

            // Hide draw tools panel when ShapeInputController finishes or cancels
            _shapeCtrl.RectangleConfirmed += _ =>
                Dispatcher.Invoke(() => MainRibbon?.ZoneTab?.HideDrawTools());
            _shapeCtrl.CircleConfirmed += _ =>
                Dispatcher.Invoke(() => MainRibbon?.ZoneTab?.HideDrawTools());
            _shapeCtrl.PickObjectsConfirmed += _ =>
                Dispatcher.Invoke(() => MainRibbon?.ZoneTab?.HideDrawTools());
            _shapeCtrl.DrawingCancelled += () =>
                Dispatcher.Invoke(() => MainRibbon?.ZoneTab?.HideDrawTools());
        }

        #endregion

        #region Filter Elements

        private void OpenFilterElementsDialog()
        {
            if (_viewModel == null) return;

            var dialog = new ElementFilterDialog(_viewModel, _viewModel.ActiveFilter)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && dialog.ResultFilter != null)
            {
                _viewModel.ApplyFilter(dialog.ResultFilter);

                // Update the ribbon filter button indicator
                MainRibbon?.IFCTab?.SetFilterActive(!dialog.ResultFilter.IsEmpty);

                // Update status bar
                bool filtered = !dialog.ResultFilter.IsEmpty;
                StatusText.Text = filtered
                    ? $"🔍 Filter active — showing {_viewModel.FilteredCount} of {_viewModel.TotalCount} elements"
                    : "Filter cleared — showing all elements";
            }
        }

        #endregion

        #region
        /// <summary>
        /// Opens the import dialog. After the user verifies and saves, shows
        /// a status message with the result.
        /// </summary>
        private void OpenLoadChartImportDialog()
        {
            var dialog = new LoadChartImportDialog { Owner = this };

            if (dialog.ShowDialog() == true && dialog.ImportedChart != null)
            {
                int points = dialog.ImportedPoints?.Count ?? 0;
                StatusText.Text =
                    $"✅  Load chart imported: {dialog.ImportedChart.CraneModel}" +
                    $"  —  {points} data points saved";
            }
        }

        /// <summary>
        /// Opens the Load Chart Viewer window so the user can browse and inspect all
        /// previously saved crane load charts (dimensions, capacity, derating, etc.).
        /// </summary>
        private void OpenLoadChartViewer()
        {
            var viewer = new WPF_IFC_test1.Dialogs.LoadChartViewerWindow
            {
                Owner = this
            };
            viewer.Show();
        }

        /// <summary>
        /// Opens the Crane Catalog Browser so the user can search manufacturer
        /// reference cranes and assign / manage their load charts.
        /// </summary>
        private void OpenCraneCatalogBrowser()
        {
            var dlg = new WPF_IFC_test1.Dialogs.CraneCatalogBrowser
            {
                Owner = this
            };
            dlg.ShowDialog();
        }

        /// <summary>
        /// Opens the side-by-side Load Chart Comparison window.
        /// The user picks two saved charts and sees a delta grid.
        /// </summary>
        private void OpenLoadChartCompareWindow()
        {
            var dlg = new WPF_IFC_test1.Dialogs.LoadChartCompareWindow
            {
                Owner = this
            };
            dlg.ShowDialog();
        }

        /// <summary>
        /// Opens the Crane Position Optimizer dialog.
        /// Requires: at least one IFC model loaded, at least one crane in the scene,
        /// and at least one Allowed (green) zone drawn.
        /// </summary>
        private void OpenCraneOptimizerDialog()
        {
            if (_viewModel?.Elements == null || _viewModel.Elements.Count == 0)
            {
                MessageBox.Show("Load an IFC model first so the optimizer has elements to analyse.",
                    "Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_craneManager == null || _craneManager.TotalCranes == 0)
            {
                MessageBox.Show("Add at least one crane to the scene before running the optimizer.",
                    "Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var zones = _zoneController?.Manager?.Zones?.ToList()
                        ?? new System.Collections.Generic.List<WPF_IFC_test1.ZoneModule.Models.CraneZone>();

            bool hasAllowed = zones.Any(z =>
                z.ZoneType == WPF_IFC_test1.ZoneModule.Models.ZoneType.Allowed);

            if (!hasAllowed)
            {
                MessageBox.Show(
                    "Draw at least one Allowed (green) zone first.\n\n" +
                    "The optimizer searches for the best crane position within allowed zones.",
                    "Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new WPF_IFC_test1.Dialogs.CraneOptimizerDialog(
                _viewModel.Elements.ToList(),
                _craneManager.Cranes.ToList(),
                zones)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && dlg.ApplyResult != null)
                ApplyOptimizerResult(dlg.ApplyResult);
        }

        /// <summary>
        /// Moves the crane to the optimised position and recolours all analysed elements
        /// by Lift Status (Green = Liftable, Orange = NonLiftable, Red = Unreachable).
        /// Elements not included in the analysis keep their original colour.
        /// </summary>
        private void ApplyOptimizerResult(WPF_IFC_test1.Dialogs.ApplyRequest req)
        {
            try
            {
                // ── 1. Move crane ────────────────────────────────────────────────
                req.Crane.Parameters.PositionX = req.Result.BestX;
                req.Crane.Parameters.PositionY = req.Result.BestY;

                // Rebuild the 3D visual so it renders at the new position
                req.Crane.CraneVisual?.Rebuild();

                // ── 2. Recolour elements by Lift Status ──────────────────────────
                const string colLiftable    = "#27AE60";   // Green
                const string colNonLiftable = "#E67E22";   // Orange
                const string colUnreachable = "#E74C3C";   // Red

                foreach (var r in req.Result.ElementResults)
                {
                    string hex = r.Status switch
                    {
                        WPF_IFC_test1.Analysis.LiftStatus.Liftable    => colLiftable,
                        WPF_IFC_test1.Analysis.LiftStatus.NonLiftable => colNonLiftable,
                        WPF_IFC_test1.Analysis.LiftStatus.Unreachable => colUnreachable,
                        _                                              => colUnreachable
                    };
                    r.Element.ElementColor = hex;
                }

                // ── 3. Update 3D material colours in the scene ───────────────────
                // Each IFC product can produce MULTIPLE shape instances (mesh pieces).
                // Build GlobalId → List<ModelVisual3D> so ALL pieces get recoloured.
                var gidToVisuals = new Dictionary<string, List<System.Windows.Media.Media3D.ModelVisual3D>>();
                foreach (var kv in _visualToGlobalIdMap)
                {
                    if (!gidToVisuals.TryGetValue(kv.Value, out var lst))
                        gidToVisuals[kv.Value] = lst = new List<System.Windows.Media.Media3D.ModelVisual3D>();
                    lst.Add(kv.Key);
                }

                foreach (var r in req.Result.ElementResults)
                {
                    if (string.IsNullOrEmpty(r.Element.GlobalId)) continue;
                    if (!gidToVisuals.TryGetValue(r.Element.GlobalId, out var visuals)) continue;
                    var newColor = ParseColor(r.Element.ElementColor);
                    foreach (var visual in visuals)
                        ApplyColorToVisual(visual, newColor);
                }

                StatusText.Text =
                    $"✅ Optimizer applied — Score: {req.Result.BestScore:F0}  " +
                    $"({req.Result.CountLiftable} Liftable / " +
                    $"{req.Result.CountNonLiftable} Non-Liftable / " +
                    $"{req.Result.CountUnreachable} Unreachable)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply optimizer result:\n{ex.Message}",
                    "Optimizer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Opens the Lift Plan panel / window.
        /// Currently opens the Load Chart Viewer as a placeholder until
        /// the full LiftPlanPanel is wired up.
        /// </summary>
        private void OpenLiftPlanWindow()
        {
            // TODO: Replace with dedicated LiftPlanPanel window once built.
            MessageBox.Show(
                "Lift Plan module is not yet fully wired.\n\n" +
                "You can import and manage load charts via the Crane tab → Import / View Charts buttons.\n" +
                "Lift feasibility checking is available in the Crane Manager panel once a chart is assigned.",
                "Lift Plan – Coming Soon",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

#endregion

        #region Zone Action Handler

        private void HandleZoneAction(ZoneTabEventArgs args)
        {
            if (args == null) return;

            // Handle OSNAP toggles
            if (args.Action == "OsnapToggle" && args.Parameter is OsnapToggleArg oa)
            {
                if (System.Enum.TryParse<OsnapType>(oa.SnapName, out var snapType))
                    _zoneController.Handler.Osnap.SetSnapEnabled(snapType, oa.Enabled);
                return;
            }

            switch (args.Action)
            {
                // Zone type selection
                case "DrawSiteBoundary":
                    _lastSelectedZoneType = ZonePreviewType.SiteBoundary;
                    if (_shapeCtrl?.IsActive == true)
                        _shapeCtrl.UpdateZoneType(_lastSelectedZoneType);
                    else
                    {
                        _measureTool?.Cancel();  // cancel measure when entering draw mode
                        Overlay?.ClearMeasure();
                        _zoneController.HandleZoneAction("DrawSiteBoundary");
                    }
                    break;
                case "DrawAllowed":
                    _lastSelectedZoneType = ZonePreviewType.Allowed;
                    if (_shapeCtrl?.IsActive == true)
                        _shapeCtrl.UpdateZoneType(_lastSelectedZoneType);
                    else
                    {
                        _measureTool?.Cancel();  // cancel measure when entering draw mode
                        Overlay?.ClearMeasure();
                        _zoneController.HandleZoneAction("DrawAllowed");
                    }
                    break;
                case "DrawForbidden":
                    _lastSelectedZoneType = ZonePreviewType.Forbidden;
                    if (_shapeCtrl?.IsActive == true)
                        _shapeCtrl.UpdateZoneType(_lastSelectedZoneType);
                    else
                    {
                        _measureTool?.Cancel();  // cancel measure when entering draw mode
                        Overlay?.ClearMeasure();
                        _zoneController.HandleZoneAction("DrawForbidden");
                    }
                    break;

                // Tool selection
                case "SetToolPolyline":
                    _zoneController.HandleZoneAction("SetToolPolyline");
                    break;
                case "SetToolRectangle":
                    _measureTool?.Cancel();
                    if (_shapeCtrl?.IsActive == true) _shapeCtrl.Cancel();
                    _zoneController.Handler.SilentCancelDraw();  // clear ZDH state, keep panel open
                    Overlay?.ClearMeasure();
                    _shapeCtrl.StartRectangle(_lastSelectedZoneType);
                    break;
                case "SetToolCircle":
                    _measureTool?.Cancel();
                    if (_shapeCtrl?.IsActive == true) _shapeCtrl.Cancel();
                    _zoneController.Handler.SilentCancelDraw();  // clear ZDH state, keep panel open
                    Overlay?.ClearMeasure();
                    _shapeCtrl.StartCircle(_lastSelectedZoneType);
                    break;
                case "SetToolPickObjects":
                    _measureTool?.Cancel();
                    if (_shapeCtrl?.IsActive == true) _shapeCtrl.Cancel();
                    _zoneController.Handler.SilentCancelDraw();  // clear ZDH state, keep panel open
                    Overlay?.ClearMeasure();
                    if (_loadedIfcModels == null || _loadedIfcModels.Count == 0)
                    {
                        StatusText.Text = "⚠ Load an IFC model first before using Pick Lines.";
                        break;
                    }
                    _shapeCtrl.StartPickObjects(_lastSelectedZoneType, IfcViewer,
                        _loadedIfcModels, _visualToGlobalIdMap,
                        _zoneController?.Handler?.Osnap);
                    break;

                // Mode selection
                case "SetModeDimension":
                    _zoneController.HandleZoneAction("SetModeDimension");
                    break;
                case "SetModeSelect":
                    _zoneController.HandleZoneAction("SetModeSelect");
                    break;

                // Zone operations
                case "FinishZone":
                    if (_shapeCtrl?.IsPickObjectsActive == true)
                        _shapeCtrl.FinishPickObjects();
                    else
                        _zoneController.HandleZoneAction("FinishZone");
                    break;
                case "UndoVertex":
                    _zoneController.HandleZoneAction("UndoVertex");
                    break;
                case "Undo":
                    _zoneController.HandleZoneAction("Undo");
                    break;
                case "Redo":
                    _zoneController.HandleZoneAction("Redo");
                    break;
                case "CancelDraw":
                    _zoneController.HandleZoneAction("CancelDraw");
                    break;
                case "DeleteZone":
                    _zoneController.HandleZoneAction("DeleteZone");
                    break;
                case "DeleteAllZones":
                    _zoneController.HandleZoneAction("DeleteAllZones");
                    break;
                case "ExportZones":
                    _zoneController.HandleZoneAction("ExportZones");
                    break;
                case "ToggleDxf":
                    _zoneController.ToggleDxfVisibility();
                    break;

                // Grid settings
                case "GridSpacing":
                    if (args.Parameter is double spacing)
                        _zoneController.SetGridSpacing(spacing);
                    break;

                // Project persistence
                case "SaveZoneProject":
                    if (args.Parameter is string savePath)
                        _zoneController.SaveProject(savePath);
                    break;
                case "LoadZoneProject":
                    if (args.Parameter is string loadPath)
                        _zoneController.LoadProject(loadPath);
                    break;
            }
        }

        #endregion

        #region Undo / Redo

        private void Undo_Click(object sender, RoutedEventArgs e)
            => _zoneController?.HandleZoneAction("Undo");

        private void Redo_Click(object sender, RoutedEventArgs e)
            => _zoneController?.HandleZoneAction("Redo");

        #endregion

        #region 3D Color Helpers (used by optimizer apply)

        /// <summary>
        /// Recursively walks a ModelVisual3D and updates every DiffuseMaterial brush to newColor.
        /// </summary>
        private static void ApplyColorToVisual(
            System.Windows.Media.Media3D.ModelVisual3D visual,
            System.Windows.Media.Color newColor)
        {
            if (visual?.Content == null) return;
            ApplyColorToModel3D(visual.Content, newColor);
        }

        private static void ApplyColorToModel3D(
            System.Windows.Media.Media3D.Model3D model,
            System.Windows.Media.Color newColor)
        {
            if (model is System.Windows.Media.Media3D.GeometryModel3D gm && !gm.IsFrozen)
            {
                // Always replace with a fresh unfrozen material so frozen brushes are never an issue.
                var newMat = new System.Windows.Media.Media3D.DiffuseMaterial(
                                 new System.Windows.Media.SolidColorBrush(newColor));
                gm.Material     = newMat;
                gm.BackMaterial = newMat;
            }
            else if (model is System.Windows.Media.Media3D.Model3DGroup grp)
            {
                foreach (var child in grp.Children)
                    ApplyColorToModel3D(child, newColor);
            }
        }

        #endregion
    }
}
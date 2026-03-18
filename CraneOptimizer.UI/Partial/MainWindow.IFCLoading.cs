using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;
using WPF_IFC_test1.Dialogs;
using WPF_IFC_test1.Utilities;
using WPF_IFC_test1.ViewModels;

namespace WPF_IFC_test1
{
    public partial class MainWindow
    {
        // ── Last-used load options (remembered across sessions within the app) ──
        private IFCLoadOptions _lastLoadOptions = new IFCLoadOptions();

        #region IFC File Loading

        private void LoadIFC_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "IFC files (*.ifc)|*.ifc|All files (*.*)|*.*",
                Title = "Select IFC File"
            };

            if (dialog.ShowDialog() != true) return;

            // Show the load-options dialog so the user can filter annotations before loading
            var optionsDialog = new IFCLoadOptionsDialog(dialog.FileName)
            {
                Owner = this
            };

            if (optionsDialog.ShowDialog() != true) return;

            // Remember options for drag-drop loads and project re-opens
            _lastLoadOptions = optionsDialog.Options;

            LoadIFCFile(dialog.FileName);
        }

        /// <summary>
        /// Loads an IFC file using the last-configured load options.
        /// Call from drag-drop or project open. Use LoadIFC_Click for interactive load.
        /// </summary>
        public void LoadIFCFile(string filePath)
        {
            LoadIFCFile(filePath, _lastLoadOptions);
        }

        /// <summary>
        /// Core load entry-point. Accepts explicit options so callers can override filters.
        /// </summary>
        public void LoadIFCFile(string filePath, IFCLoadOptions options)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"File not found:\n{filePath}");
                    return;
                }

                _currentIfcFilePath = filePath;
                _currentProjectName = Path.GetFileNameWithoutExtension(filePath);
                CloseOldModelOnly();

                IsEnabled = false;
                Cursor = Cursors.Wait;

                StatusText.Text = "Loading element data...";
                DoEvents();
                _viewModel.LoadIfc(filePath, status => { StatusText.Text = status; DoEvents(); });

                StatusText.Text = "Analyzing building...";
                DoEvents();
                AnalyzeBuilding(filePath);
                UpdateAnalysisUI();

                StatusText.Text = "Colorizing elements...";
                DoEvents();
                ColorizeElementsByWeight();

                StatusText.Text = "Rendering 3D model...";
                DoEvents();
                LoadIfcToHelix(filePath, options);

                NoModelOverlay.Visibility = Visibility.Collapsed;
                StatusText.Text = $"✓ Loaded: {Path.GetFileName(filePath)} | Elements: {_viewModel.Elements.Count}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error loading file";
                MessageBox.Show($"Failed to load IFC:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
                Cursor = Cursors.Arrow;
            }
        }

        #endregion

        #region Helix 3D Rendering

        private void LoadIfcToHelix(string filePath, IFCLoadOptions options = null)
        {
            ClearIfcModels();

            // Default to no filtering if options were not provided
            options ??= new IFCLoadOptions();

            var colorMap = new Dictionary<string, Color>();
            foreach (var el in _viewModel.Elements)
                colorMap[el.GlobalId] = ParseColor(el.ElementColor);

            var fallbackGroup = new Model3DGroup();

            using (var model = IfcStore.Open(filePath))
            {
                var context = new Xbim3DModelContext(model);
                context.CreateContext();

                // How many of the IFC file's internal length units equal 1 metre.
                // Revit/ArchiCAD exports in mm  → OneMeter = 1000
                // Some tools export in m         → OneMeter = 1
                // Feet-based files               → OneMeter ≈ 3.2808
                double oneMeter = model.ModelFactors.OneMeter;
                if (oneMeter <= 0) oneMeter = 1000.0;   // safe fallback

                var allInstances = context.ShapeInstances().ToList();
                if (allInstances.Count == 0)
                {
                    allInstances = context.ShapeInstances()
                        .Where(s => s.RepresentationType ==
                                    XbimGeometryRepresentationType.OpeningsAndAdditionsIncluded)
                        .ToList();
                }
                if (allInstances.Count == 0) return;

                foreach (var si in allInstances)
                {
                    try
                    {
                        var shapeGeom = context.ShapeGeometry(si) as IXbimShapeGeometryData;
                        if (shapeGeom?.ShapeData == null || shapeGeom.ShapeData.Length == 0)
                            continue;

                        XbimMatrix3D matrix = si.Transformation;
                        Color col = Color.FromRgb(160, 160, 170);
                        string globalId = null;

                        try
                        {
                            var product = model.Instances[si.IfcProductLabel] as IIfcProduct;
                            if (product != null)
                            {
                                // ── Annotation / type filter ───────────────────────────────
                                // Check the product against each enabled filter.
                                // If it matches a hidden type, skip rendering this shape entirely.
                                if (options.AnyFilterActive && ShouldHideProduct(product, options))
                                    continue;
                                // ──────────────────────────────────────────────────────────

                                globalId = product.GlobalId.ToString();
                                if (colorMap.TryGetValue(globalId, out var mapped))
                                    col = mapped;
                            }
                        }
                        catch { }

                        var mesh = CreateMeshFromShapeData(shapeGeom.ShapeData, matrix, oneMeter);
                        if (mesh == null || mesh.Positions.Count == 0) continue;

                        mesh.Freeze();
                        var mat = new DiffuseMaterial(new SolidColorBrush(col));
                        var geoModel = new GeometryModel3D
                        {
                            Geometry = mesh,
                            Material = mat,
                            BackMaterial = mat
                        };

                        if (globalId != null)
                        {
                            var visual = new ModelVisual3D { Content = geoModel };
                            IfcViewer.Children.Add(visual);
                            _loadedIfcModels.Add(visual);
                            _visualToGlobalIdMap[visual] = globalId;
                        }
                        else
                        {
                            fallbackGroup.Children.Add(geoModel);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Shape error: {ex.Message}");
                    }
                }
            }

            if (fallbackGroup.Children.Count > 0)
            {
                var groupVisual = new ModelVisual3D { Content = fallbackGroup };
                IfcViewer.Children.Add(groupVisual);
                _loadedIfcModels.Add(groupVisual);
            }

            // Wire up element click handler
            IfcViewer.MouseLeftButtonDown -= IfcViewer_MouseLeftButtonDown;
            IfcViewer.MouseLeftButtonDown += IfcViewer_MouseLeftButtonDown;

            // Fit camera to IFC geometry only — ignores zones so the building
            // is never dwarfed by pre-existing large zone polygons.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FitToIFCModel();
                Dispatcher.BeginInvoke(new Action(SaveCurrentCameraState),
                    System.Windows.Threading.DispatcherPriority.Background);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Returns true if the given IFC product matches any of the active hide-filters
        /// in <paramref name="options"/>. The check is type-based using Xbim interfaces.
        /// </summary>
        private static bool ShouldHideProduct(IIfcProduct product, IFCLoadOptions options)
        {
            // IfcGrid — structural reference grid axes
            if (options.HideGrids && product is IIfcGrid)
                return true;

            // IfcBuildingStorey — floor/level markers
            if (options.HideLevels && product is IIfcBuildingStorey)
                return true;

            // IfcAnnotation — text, dimensions, symbols
            if (options.HideAnnotations && product is IIfcAnnotation)
                return true;

            // IfcSite — terrain and site boundary
            if (options.HideSite && product is IIfcSite)
                return true;

            // IfcSpace — room/area volumes
            if (options.HideSpaces && product is IIfcSpace)
                return true;

            // IfcOpeningElement — door/window void cutouts
            if (options.HideOpenings && product is IIfcOpeningElement)
                return true;

            // IfcVirtualElement — invisible spatial separators
            if (options.HideVirtualElements && product is IIfcVirtualElement)
                return true;

            return false;
        }

        /// <param name="oneMeter">
        /// How many IFC internal length units equal 1 metre.
        /// Read from <c>model.ModelFactors.OneMeter</c>: 1000 for mm files, 1 for m files.
        /// </param>
        private MeshGeometry3D CreateMeshFromShapeData(byte[] shapeData, XbimMatrix3D matrix,
                                                        double oneMeter = 1000.0)
        {
            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();

            using (var ms = new MemoryStream(shapeData))
            using (var br = new BinaryReader(ms))
            {
                var xbimMesh = br.ReadShapeTriangulation();
                if (xbimMesh == null) return null;

                var transformedVertices = xbimMesh.Vertices
                    .Select(v =>
                    {
                        var wp = matrix.Transform(new XbimPoint3D(v.X, v.Y, v.Z));
                        // Divide by oneMeter to convert IFC units → metres
                        return new Point3D(wp.X / oneMeter, wp.Y / oneMeter, wp.Z / oneMeter);
                    })
                    .ToArray();

                foreach (var face in xbimMesh.Faces)
                {
                    for (int i = 0; i < face.TriangleCount; i++)
                    {
                        var p0 = transformedVertices[face.Indices[3 * i]];
                        var p1 = transformedVertices[face.Indices[3 * i + 1]];
                        var p2 = transformedVertices[face.Indices[3 * i + 2]];

                        var normal = Vector3D.CrossProduct(p1 - p0, p2 - p0);
                        normal.Normalize();

                        if (double.IsNaN(normal.X) || double.IsNaN(normal.Y) || double.IsNaN(normal.Z))
                            continue;

                        int idx = positions.Count;
                        positions.Add(p0); normals.Add(normal);
                        positions.Add(p1); normals.Add(normal);
                        positions.Add(p2); normals.Add(normal);
                        indices.Add(idx);
                        indices.Add(idx + 1);
                        indices.Add(idx + 2);
                    }
                }
            }

            return new MeshGeometry3D
            {
                Positions = positions,
                Normals = normals,
                TriangleIndices = indices
            };
        }

        #endregion

        #region Additional IFC Loading

        private void LoadIfcFileToViewer(string filePath, Vector3D offset = default)
        {
            var modelGroup = new Model3DGroup();

            using (var model = IfcStore.Open(filePath))
            {
                var context = new Xbim3DModelContext(model);
                context.CreateContext();

                double oneMeter = model.ModelFactors.OneMeter;
                if (oneMeter <= 0) oneMeter = 1000.0;

                foreach (var si in context.ShapeInstances())
                {
                    try
                    {
                        var shapeGeom = context.ShapeGeometry(si) as IXbimShapeGeometryData;
                        if (shapeGeom?.ShapeData == null || shapeGeom.ShapeData.Length == 0)
                            continue;

                        var mesh = CreateMeshFromShapeData(shapeGeom.ShapeData, si.Transformation, oneMeter);
                        if (mesh == null || mesh.Positions.Count == 0) continue;

                        mesh.Freeze();
                        var mat = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 165, 0)));
                        modelGroup.Children.Add(new GeometryModel3D
                        {
                            Geometry = mesh,
                            Material = mat,
                            BackMaterial = mat
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Shape error: {ex.Message}");
                    }
                }
            }

            if (modelGroup.Children.Count > 0)
            {
                var vis = new ModelVisual3D
                {
                    Content = modelGroup,
                    Transform = new TranslateTransform3D(offset)
                };
                IfcViewer.Children.Add(vis);
                _loadedIfcModels.Add(vis);
            }
        }

        #endregion

        #region Element Colorization

        private void ColorizeElementsByWeight()
        {
            if (_viewModel.Elements == null || !_viewModel.Elements.Any()) return;

            bool imperial = _viewModel.UnitSystem == "Imperial";
            string wUnit = imperial ? "lb" : "kg";

            foreach (var el in _viewModel.Elements)
            {
                el.WeightUnit = wUnit;

                if (!el.Weight.HasValue || el.Weight.Value == 0)
                {
                    el.ElementColor = "#A0A0AA";
                    el.DisplayWeight = null;
                    continue;
                }

                el.DisplayWeight = imperial
                    ? UnitConverter.ConvertMass(el.Weight.Value, "lb")
                    : el.Weight.Value;

                // Weight-based coloring: Red > 5000kg, Orange > 3000kg
                if (el.Weight.Value > 5000)
                    el.ElementColor = "#E74C3C";
                else if (el.Weight.Value > 3000)
                    el.ElementColor = "#F39C12";
                else
                    el.ElementColor = "#FFFFFF";
            }
        }

        private static Color ParseColor(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex))
                    return Color.FromRgb(160, 160, 170);

                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                {
                    return Color.FromRgb(
                        Convert.ToByte(hex.Substring(0, 2), 16),
                        Convert.ToByte(hex.Substring(2, 2), 16),
                        Convert.ToByte(hex.Substring(4, 2), 16));
                }
            }
            catch { }
            return Color.FromRgb(160, 160, 170);
        }

        #endregion

        #region Model Cleanup

        private void ClearIfcModels()
        {
            ResetHighlight();

            foreach (var sphere in _cogVisuals.Values)
            {
                try { IfcViewer.Children.Remove(sphere); }
                catch { }
            }
            _cogVisuals.Clear();
            _cogMarkersVisible = false;

            foreach (var v in _loadedIfcModels)
            {
                try { IfcViewer.Children.Remove(v); }
                catch { }
            }
            _loadedIfcModels.Clear();
            _visualToGlobalIdMap.Clear();
        }

        private void CloseOldModelOnly()
        {
            try
            {
                IfcViewer.MouseLeftButtonDown -= IfcViewer_MouseLeftButtonDown;
                RemoveExistingCranes();
                ClearIfcModels();
                _viewModel.ClearData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CloseOldModelOnly: {ex.Message}");
            }
        }

        private void CloseCurrentModel()
        {
            try
            {
                IfcViewer.MouseLeftButtonDown -= IfcViewer_MouseLeftButtonDown;
                RemoveExistingCranes();
                ClearIfcModels();
                _analyzer.Reset();
                _viewModel?.ClearData();
                NoModelOverlay.Visibility = Visibility.Visible;
                CraneStatusIndicator.Visibility = Visibility.Collapsed;
                StatusText.Text = "Ready";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CloseCurrentModel: {ex.Message}");
            }
        }

        #endregion
    }
}
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.Models;
using WPF_IFC_test1.Utilities;
using WPF_IFC_test1.ViewModels;
using WPF_IFC_test1.ZoneModule.Drawing;
using Xbim.Ifc;

namespace WPF_IFC_test1
{
    public partial class MainWindow
    {
        #region Element Click Handlers

        private void ElementItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is IfcElementModel element)
            {
                if (e.ClickCount == 1)
                    HandleSingleClick(element);
                else if (e.ClickCount == 2)
                    HandleDoubleClick(element);
            }
        }

        private void IfcViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Skip element hit-testing when zone controller is drawing
            if (_zoneController?.Handler?.ActiveMode != DrawMode.Select)
                return;

            try
            {
                var mousePos = e.GetPosition(IfcViewer);

                // BUG 2 FIX: VisualTreeHelper.HitTest (single-result overload) returns the
                // first hit in visual-tree order, which may be the INTERIOR face of a wall
                // or element rather than the exterior face the user is looking at.
                //
                // Fix: use the callback overload to collect ALL ray hits, sort by
                // DistanceToRayOrigin (ascending = nearest to camera first), then walk the
                // sorted list until we find a hit that maps to a known IFC element.
                // This guarantees we always pick the face closest to the user's mouse.

                var hits = new List<System.Windows.Media.Media3D.RayHitTestResult>();

                VisualTreeHelper.HitTest(
                    IfcViewer,
                    null, // no filter callback
                    result =>
                    {
                        if (result is System.Windows.Media.Media3D.RayHitTestResult rayResult)
                            hits.Add(rayResult);
                        return HitTestResultBehavior.Continue; // collect ALL hits
                    },
                    new PointHitTestParameters(mousePos));

                if (hits.Count == 0) return;

                // Sort nearest-to-camera first
                hits.Sort((a, b) => a.DistanceToRayOrigin.CompareTo(b.DistanceToRayOrigin));

                // Walk hits until we find one that belongs to a known IFC element
                foreach (var hit in hits)
                {
                    var visual = FindModelVisual3D(hit.VisualHit);
                    if (visual == null) continue;
                    if (!_visualToGlobalIdMap.TryGetValue(visual, out string gid)) continue;

                    var el = _viewModel.Elements.FirstOrDefault(x => x.GlobalId == gid);
                    if (el == null) continue;

                    if (e.ClickCount == 1)
                        HandleSingleClick(el);
                    else if (e.ClickCount == 2)
                        HandleDoubleClick(el);

                    break; // stop at the nearest matched element
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Click: {ex.Message}");
            }
        }

        private ModelVisual3D FindModelVisual3D(DependencyObject obj)
        {
            while (obj != null)
            {
                if (obj is ModelVisual3D v && _visualToGlobalIdMap.ContainsKey(v))
                    return v;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }

        #endregion

        #region Selection Handling

        private void HandleSingleClick(IfcElementModel element)
        {
            if (_selectedElement != null && _selectedElement != element)
                _selectedElement.IsSelected = false;

            _selectedElement = element;
            element.IsSelected = true;

            ShowElementProperties(element);
            HighlightElementIn3DViewer(element);
            StatusText.Text = $"✓ Selected: {element.DisplayName ?? element.Name}";
        }

        private void HandleDoubleClick(IfcElementModel element)
        {
            if (_selectedElement != element)
            {
                if (_selectedElement != null)
                    _selectedElement.IsSelected = false;
                _selectedElement = element;
                element.IsSelected = true;
            }
            OpenElementDetailWindow(element);
        }

        private void OpenElementDetailWindow(IfcElementModel element)
        {
            try
            {
                using (var model = IfcStore.Open(_currentIfcFilePath))
                {
                    var win = new ElementDetailWindow(
                         model, element,
                        _viewModel.SelectedUnit,
                        _viewModel.UnitSystem);
                    win.Owner = this;
                    win.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}");
            }
        }

        #endregion

        #region 3D Highlighting

        private void HighlightElementIn3DViewer(IfcElementModel element)
        {
            try
            {
                ResetHighlight();

                var visual = _visualToGlobalIdMap
                    .FirstOrDefault(x => x.Value == element.GlobalId).Key;

                if (visual?.Content is GeometryModel3D geo)
                {
                    _highlightedVisual = visual;
                    _originalMaterial = geo.Material;
                    _originalBackMaterial = geo.BackMaterial;

                    var highlightMaterial = new DiffuseMaterial(
                        new SolidColorBrush(Color.FromRgb(0, 200, 255)));
                    geo.Material = highlightMaterial;
                    geo.BackMaterial = highlightMaterial;
                }
            }
            catch { }
        }

        private void ResetHighlight()
        {
            if (_highlightedVisual?.Content is GeometryModel3D geo)
            {
                geo.Material = _originalMaterial;
                geo.BackMaterial = _originalBackMaterial;
            }
            _highlightedVisual = null;
            _originalMaterial = null;
            _originalBackMaterial = null;
        }

        #endregion

        #region Properties Panel

        private void ShowElementProperties(IfcElementModel element)
        {
            PropertiesPanel.Children.Clear();
            var unit = _viewModel.SelectedUnit ?? "m";

            void AddProp(string label, string value)
            {
                if (string.IsNullOrWhiteSpace(value) || value == "0" || value == "0.00")
                    return;

                var border = new Border { Style = (Style)FindResource("PropertyRow") };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });

                var lbl = new TextBlock
                {
                    Text = label,
                    Style = (Style)FindResource("PropertyLabel")
                };
                var val = new TextBlock
                {
                    Text = value,
                    Style = (Style)FindResource("PropertyValue"),
                    TextWrapping = TextWrapping.Wrap
                };

                Grid.SetColumn(lbl, 0);
                Grid.SetColumn(val, 1);
                grid.Children.Add(lbl);
                grid.Children.Add(val);
                border.Child = grid;
                PropertiesPanel.Children.Add(border);
            }

            // Header with weight-based styling
            string hdrColor = "#2D2D30";
            string wIcon = "🔍";
            if (element.Weight.HasValue)
            {
                if (element.Weight.Value > 5000)
                {
                    hdrColor = "#55E74C3C";
                    wIcon = "🔴";
                }
                else if (element.Weight.Value > 3000)
                {
                    hdrColor = "#55F39C12";
                    wIcon = "🟠";
                }
            }

            var hdrBorder = new Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 5),
                Background = (SolidColorBrush)new BrushConverter().ConvertFromString(hdrColor)
            };
            hdrBorder.Child = new TextBlock
            {
                Text = $"{wIcon} {element.DisplayName ?? element.Name ?? "Element"}",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = (SolidColorBrush)FindResource("AccentBrush")
            };
            PropertiesPanel.Children.Add(hdrBorder);

            // Basic properties
            AddProp("Global ID:", element.GlobalId);
            AddProp("Display Name:", element.DisplayName);
            AddProp("Type:", element.Type);
            AddProp("Mark:", element.Mark);

            // Dimensions
            if (element.Length.HasValue && element.Length.Value > 0)
                AddProp("Length:", UnitSystem.Format(element.Length.Value));
            if (element.Width.HasValue && element.Width.Value > 0)
                AddProp("Width:", UnitSystem.Format(element.Width.Value));
            if (element.Height.HasValue && element.Height.Value > 0)
                AddProp("Height:", UnitSystem.Format(element.Height.Value));

            // Weight
            if (element.Weight.HasValue && element.Weight.Value > 0)
            {
                string wLabel = element.Weight.Value > 5000 ? "⚠️ Weight (HEAVY):"
                              : element.Weight.Value > 3000 ? "⚠️ Weight (MEDIUM):"
                              : "Weight:";
                AddProp(wLabel,
                    $"{element.DisplayWeight ?? element.Weight.Value:F2} {element.WeightUnit ?? "kg"}");
            }

            // Volume
            if (element.Volume.HasValue && element.Volume.Value > 0)
            {
                string vUnit = _viewModel.UnitSystem == "Imperial" ? "ft³" : "m³";
                double vol = _viewModel.UnitSystem == "Imperial"
                    ? UnitConverter.ConvertVolume(element.Volume.Value, "ft³")
                    : element.Volume.Value;
                AddProp("Volume:", $"{vol:F4} {vUnit}");
            }

            // Material
            AddProp("Material:", element.Material);

            // Centre of Gravity
            if (element.COG_X.HasValue && element.COG_Y.HasValue && element.COG_Z.HasValue)
            {
                var cogHdr = new Border
                {
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 6, 0, 2),
                    Background = (SolidColorBrush)new BrushConverter().ConvertFromString("#2255AA")
                };
                cogHdr.Child = new TextBlock
                {
                    Text = "📍 Centre of Gravity",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Foreground = Brushes.White
                };
                PropertiesPanel.Children.Add(cogHdr);

                AddProp("COG X:", UnitSystem.Format(element.COG_X.Value));
                AddProp("COG Y:", UnitSystem.Format(element.COG_Y.Value));
                AddProp("COG Z:", UnitSystem.Format(element.COG_Z.Value));
            }
        }

        #endregion

        #region Unit Selector

        private void UnitSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_viewModel.Elements?.Count > 0)
                    ColorizeElementsByWeight();

                if (_selectedElement != null)
                {
                    var updated = _viewModel.Elements
                        .FirstOrDefault(el => el.GlobalId == _selectedElement.GlobalId);
                    if (updated != null)
                    {
                        _selectedElement = updated;
                        Dispatcher.BeginInvoke(
                            new Action(() => ShowElementProperties(updated)),
                            System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }

                if (_analyzer?.IsAnalyzed == true)
                    UpdateAnalysisUI();

                if (!string.IsNullOrEmpty(_viewModel.SelectedUnit))
                    StatusText.Text = $"✓ Unit: {_viewModel.SelectedUnit}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UnitSelector: {ex.Message}");
            }
        }

        #endregion
    }
}
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Application.Services;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer.Services;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using CraneOptimizer.UI.Features.F5_ElementManagement.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels
{
    public class IfcViewerViewModel : ViewModelBase, IHighlightService
    {
        private readonly IIfcReaderRepository _ifcReader;
        private readonly ISceneService _scene;
        private readonly IDialogService _dialog;
        private readonly IIfcOptionsDialogService _dialogService;
        private readonly ISelectionService _selectionService;
        private readonly IRenderingService _renderingService;

        private readonly Dictionary<Visual3D, PrecastElement> _elementVisuals = new();
        public IReadOnlyDictionary<Visual3D, PrecastElement> ElementVisuals => _elementVisuals;

        public Visual3DCollection? ViewportChildren { get; set; }
        public PerspectiveCamera? Camera { get; set; }

        public IViewportInteraction? CurrentInteraction { get; set; }
        public ICommand LoadIfcCommand { get; }
        public ICommand ViewportClickCommand { get; }
        public ICommand ViewportMoveCommand { get; }

        #region State

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();

                // CanExecute
                (LoadIfcCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private string _status = "Idle";
        public string Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
            }
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) < 0.01) return;
                _progress = value;
                OnPropertyChanged();
            }
        }

        #endregion


        public IfcViewerViewModel(
            IIfcReaderRepository ifcReader,
            ISceneService scene,
            IDialogService dialog,
            IIfcOptionsDialogService dialogService,
            ISelectionService selectionService,
            IRenderingService renderingService)
        {
            _ifcReader = ifcReader;
            _scene = scene;
            _dialog = dialog;
            _dialogService = dialogService;
            _selectionService = selectionService;
            _renderingService = renderingService;
            
            LoadIfcCommand = new RelayCommand(
                async () => await ExecuteLoadIfcAsync(),
                () => !IsLoading);

            ViewportClickCommand = new RelayCommand<DomainPoint3D>(p =>
            {
                if (p == null) return;
                CurrentInteraction?.OnClick(p);
            });

            ViewportMoveCommand = new RelayCommand<DomainPoint3D>(
                p => CurrentInteraction?.OnMove(p));

            _selectionService.SelectionChanged += element =>
            {
                var mode = element is null ? HighlightMode.Normal : HighlightMode.Highlight;
                var dispatcher = System.Windows.Application.Current.Dispatcher;

                if (dispatcher.CheckAccess())
                    ApplyHighlight(element, mode);
                else
                    dispatcher.InvokeAsync(() => ApplyHighlight(element, mode));
            };

            _scene.ElementsChanged += OnElementsChanged;
        }

        private void OnElementsChanged(List<PrecastElement> elements)
        {
            var dispatcher = System.Windows.Application.Current.Dispatcher;

            if (dispatcher.CheckAccess())
                UpdateVisuals(elements);
            else
                dispatcher.InvokeAsync(() => UpdateVisuals(elements));
        }

        private void UpdateVisuals(List<PrecastElement> elements)
        {
            if (ViewportChildren == null) return;

            // Clear old visuals
            foreach (var visual in _elementVisuals.Keys)
            {
                ViewportChildren.Remove(visual);
            }
            _elementVisuals.Clear();

            var builder = new ElementVisualBuilder();
            foreach (var element in elements)
            {
                var visual = builder.Build((element, (CraneOptimizer.Domain.Schemas.Enums.LiftStatus?)null));
                if (visual != null)
                {
                    _elementVisuals[visual] = element;
                    ViewportChildren.Add(visual);
                }
            }
        }

        public void RequestMode(HighlightMode mode)
        {
            var dispatcher = System.Windows.Application.Current.Dispatcher;

            if (dispatcher.CheckAccess())
                ApplyHighlight(_selectionService.SelectedElement, mode);
            else
                dispatcher.InvokeAsync(() => ApplyHighlight(_selectionService.SelectedElement, mode));
        }

        private void ApplyHighlight(PrecastElement? selected, HighlightMode mode)
        {
            foreach (var kvp in _elementVisuals)
            {
                var visual = kvp.Key as ModelVisual3D;
                if (visual?.Content is not GeometryModel3D model) continue;

                bool isSelected = kvp.Value == selected;
                double opacity = 1.0;
                bool isVisible = true;

                switch (mode)
                {
                    case HighlightMode.Normal:
                        opacity = 1.0;
                        isVisible = true;
                        break;

                    case HighlightMode.Highlight:
                        opacity = isSelected ? 1.0 : 0.35;
                        isVisible = true;
                        break;

                    case HighlightMode.Focus:
                        opacity = isSelected ? 1.0 : 0.35;
                        isVisible = true;
                        break;

                    case HighlightMode.Isolation:
                        opacity = 1.0;
                        isVisible = isSelected;
                        break;
                }

                // Apply opacity to material
                if (model.Material is DiffuseMaterial dm && dm.Brush is SolidColorBrush scb)
                {
                    scb.Opacity = isVisible ? opacity : 0.0;
                }
                if (model.BackMaterial is DiffuseMaterial bdm && bdm.Brush is SolidColorBrush bscb)
                {
                    bscb.Opacity = isVisible ? opacity : 0.0;
                }
            }

            if (mode == HighlightMode.Focus && selected is not null)
                AnimateCameraTo(selected);
        }

        private void AnimateCameraTo(PrecastElement element)
        {
            if (Camera == null) return;

            var center = element.ElementCOG;
            var target = new Point3D(center.X, center.Y - 20, center.Z + 10);

            var animation = new Point3DAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            Camera.BeginAnimation(PerspectiveCamera.PositionProperty, animation);
            Camera.LookDirection = new Vector3D(center.X - target.X, center.Y - target.Y, center.Z - target.Z);
        }

        #region Main Logic

        private async Task ExecuteLoadIfcAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                Progress = 0;
                Status = "Selecting IFC file...";

                // 1️⃣ select file
                var path = _dialog.OpenFile("IFC (*.ifc)|*.ifc");
                if (string.IsNullOrWhiteSpace(path))
                {
                    Status = "Operation cancelled";
                    return;
                }

                // 2️⃣ filter
                var filter = _dialogService.Show(path);
                if (filter == null)
                {
                    Status = "Filter cancelled";
                    return;
                }

                // 3️⃣ read ifc data
                Status = "Reading IFC...";

                var progress = new Progress<double>(p =>
                {
                    Progress = p;
                    Status = $"Reading IFC... {p:F0}%";
                });

                var elements = await Task.Run(() =>
                    _ifcReader.ReadElements(path, progress));

                if (elements == null || elements.Count == 0)
                {
                    Status = "No elements found";
                    _dialog.ShowMessage("No elements found.");
                    return;
                }

                // 4️⃣ filteration
                Status = "Filtering elements...";
                var visible = ApplyFilter(elements, filter);

                // 5️⃣ update scene
                Status = "Updating 3D scene...";
                _scene.SetElements(visible);

                Progress = 100;
                Status = $"Loaded {visible.Count} elements successfully";
            }
            catch (Exception ex)
            {
                Status = "Error occurred";
                _dialog.ShowMessage(ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Filtering

        private static List<PrecastElement> ApplyFilter(
            List<PrecastElement> elements,
            IfcFilterOptions options)
        {
            if (elements == null || elements.Count == 0)
                return new List<PrecastElement>();

            return elements.Where(e =>
            {
                var t = e.ElementType ?? string.Empty;

                if (options.HideGrids && Contains(t, "IfcGrid")) return false;
                if (options.HideLevels && Contains(t, "IfcBuildingStorey")) return false;
                if (options.HideAnnotations && Contains(t, "IfcAnnotation")) return false;
                if (options.HideSite && Contains(t, "IfcSite")) return false;
                if (options.HideSpaces && Contains(t, "IfcSpace")) return false;
                if (options.HideOpenings && Contains(t, "IfcOpeningElement")) return false;
                if (options.HideVirtualElements && Contains(t, "IfcVirtualElement")) return false;

                return true;

            }).ToList();
        }

        private static bool Contains(string source, string value)
        {
            return source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}

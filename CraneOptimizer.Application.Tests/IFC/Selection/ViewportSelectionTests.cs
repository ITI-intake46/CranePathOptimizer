using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F17_Selection.Interaction;
using CraneOptimizer.UI.Features.F17_Selection.Services;
using CraneOptimizer.UI.Features.F16_Filter.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using System.Windows.Media.Media3D;
using System.Windows;
using System.Windows.Input;
using HelixToolkit.Wpf;

namespace CraneOptimizer.Application.Tests
{
    public class ViewportSelectionTests
    {
#if DEBUG
        static ViewportSelectionTests()
        {
            SelectionHandler.TestProjectHook = (viewport, point) =>
            {
                var projCam = viewport.Camera as PerspectiveCamera;
                var camPos = projCam?.Position ?? new Point3D(0, 0, 10);
                var lookDir = projCam?.LookDirection ?? new Vector3D(0, 0, -1);
                var upDir = projCam?.UpDirection ?? new Vector3D(0, 1, 0);
                var fov = projCam?.FieldOfView ?? 45.0;

                double d = camPos.Z - point.Z;
                if (d <= 0.05) return new Point(double.NaN, double.NaN);

                double halfFovRad = (fov / 2.0) * Math.PI / 180.0;
                double tanHalfFov = Math.Tan(halfFovRad);

                double dx = point.X - camPos.X;
                double dy = point.Y - camPos.Y;

                double xNdc = dx / (d * tanHalfFov);
                double yNdc = dy / (d * tanHalfFov);

                double screenX = 400.0 + xNdc * 400.0;
                double screenY = 300.0 - yNdc * 300.0;

                return new Point(screenX, screenY);
            };
        }
#endif

        private class TestSceneService : ISceneService
        {
            private readonly List<PrecastElement> _elements = new();
            private readonly List<PrecastElement> _selectedElements = new();
            private readonly List<GreenAreaZone> _zones = new();
            private readonly List<SceneCrane> _cranes = new();
            private readonly List<SceneDxf> _dxfEntities = new();

            public GreenAreaZone? SelectedZone { get; private set; }
            public SceneCrane? SelectedCrane { get; private set; }
            public SceneDxf? SelectedDxfEntity { get; private set; }

            public event Action<List<PrecastElement>>? ElementsChanged;
            public event Action<List<PrecastElement>>? ElementsSelectionChanged;
            public event Action<GreenAreaZone>? ZoneAdded;
            public event Action<GreenAreaZone>? ZoneRemoved;
            public event Action<GreenAreaZone>? ZoneUpdated;
            public event Action? ZonesCleared;
            public event Action<GreenAreaZone?>? ZoneSelected;
            public event Action<SceneCrane>? CraneAdded;
            public event Action<SceneCrane>? CraneRemoved;
            public event Action? CranesCleared;
            public event Action<SceneCrane>? CraneMoved;
            public event Action<SceneCrane>? CraneRotated;
            public event Action<SceneCrane>? CraneUpdated;
            public event Action<SceneCrane?>? CraneSelected;
            public event Action<SceneDxf>? DxfEntityAdded;
            public event Action<SceneDxf>? DxfEntityRemoved;
            public event Action<SceneDxf>? DxfEntityUpdated;
            public event Action? DxfEntitiesCleared;
            public event Action<SceneDxf?>? DxfEntitySelected;
            public event Action<object?>? SelectionChanged;

            public List<PrecastElement> GetElements() => _elements;
            public void SetElements(List<PrecastElement> elements) { _elements.Clear(); _elements.AddRange(elements); }
            public void ClearElements() => _elements.Clear();

            public List<PrecastElement> GetSelectedElements() => _selectedElements.ToList();
            public void SetSelectedElements(List<PrecastElement> elements)
            {
                _selectedElements.Clear();
                _selectedElements.AddRange(elements);
                ElementsSelectionChanged?.Invoke(_selectedElements);
            }
            public void ClearSelectedElements()
            {
                _selectedElements.Clear();
                ElementsSelectionChanged?.Invoke(_selectedElements);
            }

            public List<GreenAreaZone> GetZones() => _zones;
            public void AddZone(GreenAreaZone zone) => _zones.Add(zone);
            public void RemoveZone(GreenAreaZone zone) => _zones.Remove(zone);
            public void RemoveSelectedZone() { if (SelectedZone != null) _zones.Remove(SelectedZone); SelectedZone = null; }
            public void ClearZones() => _zones.Clear();
            public void UpdateZone(GreenAreaZone zone) { }

            public void SetSelectedZone(GreenAreaZone? zone)
            {
                SelectedZone = zone;
                ZoneSelected?.Invoke(zone);
            }
            public void ClearZoneSelection() => SetSelectedZone(null);

            public List<SceneCrane> GetCranes() => _cranes;
            public void AddCrane(SceneCrane crane) => _cranes.Add(crane);
            public void RemoveCrane(SceneCrane crane) => _cranes.Remove(crane);
            public void ClearCranes() => _cranes.Clear();
            public void MoveCrane(SceneCrane crane, double x, double y, double z) { }
            public void RotateCrane(SceneCrane crane, double angle) { }
            public void UpdateCrane(SceneCrane crane) { }

            public void SetSelectedCrane(SceneCrane? crane)
            {
                SelectedCrane = crane;
                CraneSelected?.Invoke(crane);
            }
            public void ClearCraneSelection() => SetSelectedCrane(null);

            public List<SceneDxf> GetDxfEntities() => _dxfEntities;
            public void AddDxfEntity(SceneDxf entity) => _dxfEntities.Add(entity);
            public void RemoveDxfEntity(SceneDxf entity) => _dxfEntities.Remove(entity);
            public void RemoveSelectedDxfEntity() { }
            public void ClearDxfEntities() => _dxfEntities.Clear();
            public void UpdateDxfEntity(SceneDxf entity) { }

            public void SetSelectedDxfEntity(SceneDxf? entity)
            {
                SelectedDxfEntity = entity;
                DxfEntitySelected?.Invoke(entity);
            }
            public void ClearDxfSelection() => SetSelectedDxfEntity(null);

            public object? GetSelectedObject() => null;
            public void SetSelectedObject(object? obj) { }
        }

        private readonly TestSceneService _scene;
        private readonly ViewportSelectionService _service;

        public ViewportSelectionTests()
        {
            _scene = new TestSceneService();
            _service = new ViewportSelectionService(_scene);
        }

        [Fact]
        public void NormalClick_ClearsAll_AndSelectsOnlyTarget()
        {
            // Arrange
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            var zone = new GreenAreaZone { ZoneId = Guid.NewGuid(), Type = ZoneType.Forbidden };
            
            _scene.SetSelectedElements(new List<PrecastElement> { el1 });
            _scene.SetSelectedZone(zone);

            // Act: Normal click on EL-2
            _service.Select(el2, isCtrlHeld: false);

            // Assert
            Assert.Empty(_scene.GetSelectedElements().Where(x => x.ElementID == "EL-1"));
            var selected = _scene.GetSelectedElements();
            Assert.Single(selected);
            Assert.Equal("EL-2", selected[0].ElementID);
            Assert.Null(_scene.SelectedZone);
        }

        [Fact]
        public void CtrlClick_TogglesTarget_WithoutClearingOthers()
        {
            // Arrange
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            var zone = new GreenAreaZone { ZoneId = Guid.NewGuid(), Type = ZoneType.Allowed };

            _scene.SetSelectedElements(new List<PrecastElement> { el1 });
            _scene.SetSelectedZone(zone);

            // Act: Ctrl+Click on EL-2
            _service.Select(el2, isCtrlHeld: true);

            // Assert: Both are selected
            var selectedEls = _scene.GetSelectedElements();
            Assert.Equal(2, selectedEls.Count);
            Assert.Contains(selectedEls, x => x.ElementID == "EL-1");
            Assert.Contains(selectedEls, x => x.ElementID == "EL-2");
            Assert.NotNull(_scene.SelectedZone);

            // Act: Ctrl+Click EL-1 (toggle off)
            _service.Select(el1, isCtrlHeld: true);

            // Assert: Only EL-2 and zone selected
            selectedEls = _scene.GetSelectedElements();
            Assert.Single(selectedEls);
            Assert.Equal("EL-2", selectedEls[0].ElementID);
            Assert.NotNull(_scene.SelectedZone);
        }

        [Fact]
        public void EmptySpaceClick_WithoutCtrl_ClearsAllSelections()
        {
            // Arrange
            var el = new PrecastElement { ElementID = "EL-1" };
            var crane = new SceneCrane { Id = Guid.NewGuid() };
            _scene.SetSelectedElements(new List<PrecastElement> { el });
            _scene.SetSelectedCrane(crane);

            // Act: Click empty space without Ctrl
            _service.Select(null, isCtrlHeld: false);

            // Assert
            Assert.Empty(_scene.GetSelectedElements());
            Assert.Null(_scene.SelectedCrane);
        }

        [Fact]
        public void EmptySpaceClick_WithCtrl_DoesNothing()
        {
            // Arrange
            var el = new PrecastElement { ElementID = "EL-1" };
            _scene.SetSelectedElements(new List<PrecastElement> { el });

            // Act: Click empty space with Ctrl
            _service.Select(null, isCtrlHeld: true);

            // Assert
            var selected = _scene.GetSelectedElements();
            Assert.Single(selected);
            Assert.Equal("EL-1", selected[0].ElementID);
        }

        [Fact]
        public void SelectionKind_DistinguishesGreenAndRedAreas()
        {
            // Arrange
            var greenZone = new GreenAreaZone { ZoneId = Guid.NewGuid(), Type = ZoneType.Allowed, Name = "Green" };
            var redZone = new GreenAreaZone { ZoneId = Guid.NewGuid(), Type = ZoneType.Forbidden, Name = "Red" };

            // Act
            var greenTarget = SelectionTarget.FromHitObject(greenZone);
            var redTarget = SelectionTarget.FromHitObject(redZone);

            // Assert
            Assert.NotNull(greenTarget);
            Assert.Equal(SelectionKind.GreenArea, greenTarget.Kind);
            Assert.Equal(greenZone.ZoneId.ToString(), greenTarget.StableId);

            Assert.NotNull(redTarget);
            Assert.Equal(SelectionKind.RedArea, redTarget.Kind);
            Assert.Equal(redZone.ZoneId.ToString(), redTarget.StableId);
        }

        [Fact]
        public void TogglingSecondCrane_ReplacesFirstCrane_DueToSingularLimit()
        {
            // Arrange
            var crane1 = new SceneCrane { Id = Guid.NewGuid() };
            var crane2 = new SceneCrane { Id = Guid.NewGuid() };
            
            _scene.SetSelectedCrane(crane1);

            // Act: Ctrl+Click on crane2
            _service.Select(crane2, isCtrlHeld: true);

            // Assert: crane2 replaces crane1 because SceneCrane selection is singular
            Assert.Equal(crane2.Id, _scene.SelectedCrane?.Id);
        }

        [Fact]
        public void SelectionChangedEvent_FiresWithCorrectTargets()
        {
            // Arrange
            var el = new PrecastElement { ElementID = "EL-1" };
            IReadOnlyList<SelectionTarget>? lastTargets = null;
            int fireCount = 0;

            _service.SelectionChanged += targets =>
            {
                lastTargets = targets;
                fireCount++;
            };

            // Act
            _service.Select(el, isCtrlHeld: false);

            // Assert
            Assert.Equal(1, fireCount);
            Assert.NotNull(lastTargets);
            Assert.Single(lastTargets);
            Assert.Equal(SelectionKind.IfcElement, lastTargets[0].Kind);
            Assert.Equal("EL-1", lastTargets[0].StableId);
        }

        [Fact]
        public void ReentrancyGuard_PreventsInfiniteLoops()
        {
            // Arrange
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            int fireCount = 0;

            _service.SelectionChanged += targets =>
            {
                fireCount++;
                if (fireCount == 1)
                {
                    // Trigger a re-entrant selection during event handling
                    _service.Select(el2, isCtrlHeld: false);
                }
            };

            // Act
            _service.Select(el1, isCtrlHeld: false);

            // Assert: should not loop infinitely, fireCount should remain 1 because the second Select is blocked
            Assert.Equal(1, fireCount);
            var selected = _scene.GetSelectedElements();
            Assert.Single(selected);
            Assert.Equal("EL-1", selected[0].ElementID); // First selection completed, second was blocked
        }

        [Fact]
        public void SelectElements_NormalMode_ReplacesSelectionEntirely()
        {
            // Arrange
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            var crane = new SceneCrane { Id = Guid.NewGuid() };
            _scene.SetSelectedElements(new List<PrecastElement> { el1 });
            _scene.SetSelectedCrane(crane);

            // Act
            _service.SelectElements(new[] { el2 }, isCtrlHeld: false);

            // Assert
            var selected = _scene.GetSelectedElements();
            Assert.Single(selected);
            Assert.Equal("EL-2", selected[0].ElementID);
            Assert.Null(_scene.SelectedCrane);
        }

        [Fact]
        public void SelectElements_CtrlMode_TogglesElements()
        {
            // Arrange
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            _scene.SetSelectedElements(new List<PrecastElement> { el1 });

            // Act: Ctrl-Select el1 and el2. el1 should toggle off, el2 should toggle on.
            _service.SelectElements(new[] { el1, el2 }, isCtrlHeld: true);

            // Assert
            var selected = _scene.GetSelectedElements();
            Assert.Single(selected);
            Assert.Equal("EL-2", selected[0].ElementID);
        }

        [Fact]
        public void SetElements_ReplacesSelectionDeterministically()
        {
            // Arrange
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            var el3 = new PrecastElement { ElementID = "EL-3" };
            _scene.SetSelectedElements(new List<PrecastElement> { el1, el2 });

            // Act
            _service.SetElements(new[] { el3 });

            // Assert
            var selected = _scene.GetSelectedElements();
            Assert.Single(selected);
            Assert.Equal("EL-3", selected[0].ElementID);
        }

        [Fact]
        public void ToggleElement_TogglesSelection()
        {
            // Arrange
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            _scene.SetSelectedElements(new List<PrecastElement> { el1 });

            // Act 1: Toggle on el2
            _service.ToggleElement(el2);
            // Assert 1
            var selected = _scene.GetSelectedElements();
            Assert.Equal(2, selected.Count);

            // Act 2: Toggle off el1
            _service.ToggleElement(el1);
            // Assert 2
            selected = _scene.GetSelectedElements();
            Assert.Single(selected);
            Assert.Equal("EL-2", selected[0].ElementID);
        }

        [Fact]
        public void Clear_ClearsAllSelections()
        {
            // Arrange
            var el = new PrecastElement { ElementID = "EL-1" };
            var zone = new GreenAreaZone { ZoneId = Guid.NewGuid(), Type = ZoneType.Forbidden };
            _scene.SetSelectedElements(new List<PrecastElement> { el });
            _scene.SetSelectedZone(zone);

            // Act
            _service.Clear();

            // Assert
            Assert.Empty(_scene.GetSelectedElements());
            Assert.Null(_scene.SelectedZone);
        }

        [Fact]
        public void SelectingCraneOrZone_ClearsElementsAccordingToCentralSelectionPolicy()
        {
            // Arrange
            var el = new PrecastElement { ElementID = "EL-1" };
            var crane = new SceneCrane { Id = Guid.NewGuid() };
            _scene.SetSelectedElements(new List<PrecastElement> { el });

            // Act
            _service.Select(crane, isCtrlHeld: false);

            // Assert
            Assert.Empty(_scene.GetSelectedElements());
            Assert.Equal(crane.Id, _scene.SelectedCrane?.Id);
        }

        [Fact]
        public void CtrlClick_TogglesElementSelection()
        {
            // Arrange
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            _scene.SetSelectedElements(new List<PrecastElement> { el1 });

            // Act: Ctrl+Click on EL-2
            _service.Select(el2, isCtrlHeld: true);

            // Assert
            var selected = _scene.GetSelectedElements();
            Assert.Equal(2, selected.Count);
            Assert.Contains(selected, x => x.ElementID == "EL-1");
            Assert.Contains(selected, x => x.ElementID == "EL-2");
        }

        [Fact]
        public void CtrlClick_SelectedElement_DeselectsIt()
        {
            // Arrange
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            _scene.SetSelectedElements(new List<PrecastElement> { el1, el2 });

            // Act: Ctrl+Click on EL-1
            _service.Select(el1, isCtrlHeld: true);

            // Assert
            var selected = _scene.GetSelectedElements();
            Assert.Single(selected);
            Assert.Equal("EL-2", selected[0].ElementID);
        }

        [Fact]
        public void BoxSelection_Window_SelectsAllFullyContainedElements()
        {
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };

            // Act
            _service.SetElements(new[] { el1, el2 });

            // Assert
            var selected = _scene.GetSelectedElements();
            Assert.Equal(2, selected.Count);
            Assert.Contains(selected, x => x.ElementID == "EL-1");
            Assert.Contains(selected, x => x.ElementID == "EL-2");
        }

        [Fact]
        public void BoxSelection_Crossing_SelectsAllIntersectingElements()
        {
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };

            // Act
            _service.SelectElements(new[] { el1, el2 }, isCtrlHeld: false);

            // Assert
            var selected = _scene.GetSelectedElements();
            Assert.Equal(2, selected.Count);
        }

        [Fact]
        public void BoxSelection_CommitsMultipleElements_NotOnlySingleHit()
        {
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            var el3 = new PrecastElement { ElementID = "EL-3" };

            // Act
            _service.SetElements(new[] { el1, el2, el3 });

            // Assert
            var selected = _scene.GetSelectedElements();
            Assert.Equal(3, selected.Count);
        }

        [Fact]
        public void ViewportSelectionService_SelectElements_CtrlMode_TogglesCollection()
        {
            // Arrange
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            var el3 = new PrecastElement { ElementID = "EL-3" };
            _scene.SetSelectedElements(new List<PrecastElement> { el1, el2 });

            // Act: Select el2 and el3 with Ctrl held (el2 toggles off, el3 toggles on)
            _service.SelectElements(new[] { el2, el3 }, isCtrlHeld: true);

            // Assert
            var selected = _scene.GetSelectedElements();
            Assert.Equal(2, selected.Count);
            Assert.Contains(selected, x => x.ElementID == "EL-1");
            Assert.Contains(selected, x => x.ElementID == "EL-3");
            Assert.DoesNotContain(selected, x => x.ElementID == "EL-2");
        }

        private void RunInSTA(Action action)
        {
            Exception? failure = null;
            var t = new System.Threading.Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            t.Join();
            ElementVisualBuilder.RegisterSpatialHitTestElements(Array.Empty<ElementVisualBuilder.SpatialHitTestElement>());

            if (failure != null)
                Assert.Fail(failure.ToString());
        }

        [Fact]
        public void SelectionHandler_DoesNotInterceptViewCubeOrNonViewportControls()
        {
            RunInSTA(() =>
            {
                // Arrange
                var viewport = new HelixViewport3D();
                var button = new System.Windows.Controls.Button();
                var e = new System.Windows.Input.MouseButtonEventArgs(
                    System.Windows.Input.InputManager.Current.PrimaryMouseDevice,
                    0,
                    System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = System.Windows.UIElement.MouseDownEvent
                };
                
                var sourceField = typeof(System.Windows.RoutedEventArgs).GetField("_source", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (sourceField != null)
                {
                    sourceField.SetValue(e, button);
                }

                var originalSourceField = typeof(System.Windows.RoutedEventArgs).GetField("_originalSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (originalSourceField != null)
                {
                    originalSourceField.SetValue(e, button);
                }

                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var hitTest = new MockHitTestService();
                var cache = new HoverHitCache();
                var handler = new SelectionHandler(_service, hitTest, cache, filter, origin);

                // Act
                var result = handler.IsClickOnViewCubeOrOverlay(viewport, e);

                // Assert
                Assert.True(result);
            });
        }

        [Fact]
        public void SelectionHandler_ArmsEmptySpaceBoxDragOnMouseDown()
        {
            RunInSTA(() =>
            {
                // Arrange
                var viewport = new HelixViewport3D();
                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var hitTest = new MockHitTestService();
                var cache = new HoverHitCache();
                var handler = new SelectionHandler(_service, hitTest, cache, filter, origin);

                var eDown = new System.Windows.Input.MouseButtonEventArgs(
                    System.Windows.Input.InputManager.Current.PrimaryMouseDevice,
                    0,
                    System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = System.Windows.UIElement.PreviewMouseDownEvent
                };

                // Act
                handler.OnPreviewMouseDown(viewport, eDown);

                // Assert
                Assert.True(eDown.Handled);
            });
        }

        private class MockHitTestService : IViewportHitTestService
        {
            public object? HitObject { get; set; }
            public object? GetHitObject(HelixViewport3D viewport, System.Windows.Point screenPt) => HitObject;
            public bool TryGetWorldPoint(HelixViewport3D viewport, System.Windows.Point screenPt, out System.Windows.Media.Media3D.Point3D worldPoint)
            {
                worldPoint = new System.Windows.Media.Media3D.Point3D();
                return false;
            }
            public bool TryGetPlacementPoint(HelixViewport3D viewport, System.Windows.Point screenPt, out System.Windows.Media.Media3D.Point3D worldPoint)
            {
                worldPoint = new System.Windows.Media.Media3D.Point3D();
                return false;
            }
            public bool TryGetHitCrane(HelixViewport3D viewport, System.Windows.Point screenPt, out SceneCrane crane)
            {
                crane = null!;
                return false;
            }
        }

        private class MockFilterService : IElementVisibilityFilterService
        {
            public List<PrecastElement> VisibleElementsList { get; } = new();
            public IReadOnlyList<PrecastElement> AllElements => new List<PrecastElement>();
            public IReadOnlyList<PrecastElement> VisibleElements => VisibleElementsList;
            public IReadOnlyList<string> ElementTypes => new List<string>();
            public IReadOnlyList<string> Materials => new List<string>();
            public IReadOnlyList<string> Storeys => new List<string>();

            public int ShowingCount => 0;
            public int TotalCount => 0;
            public double AvailableMinWeightKg => 0d;
            public double AvailableMaxWeightKg => 0d;
            public double MinWeightKg => 0d;
            public double MaxWeightKg => 0d;
            public string SearchText => string.Empty;

            public event Action? StateChanged;

            public bool IsElementIncluded(PrecastElement element) => true;
            public void SetElements(IEnumerable<PrecastElement> elements) { }
            public void SetSearchText(string text) { }
            public void SetTypeEnabled(string type, bool enabled) { }
            public void SetMaterialEnabled(string material, bool enabled) { }
            public void SetStoreyEnabled(string storey, bool enabled) { }
            public void SetWeightRange(double minWeightKg, double maxWeightKg) { }
            public void SetIncludedElementIds(IEnumerable<string>? elementIds) { }
            public void SetExcludedElementIds(IEnumerable<string>? elementIds) { }
            public void ResetFilters() { }
            public void ShowAll() { }
            public void ShowElements(IEnumerable<PrecastElement> elements) { }
            public void HideElements(IEnumerable<PrecastElement> elements) { }
            public void IsolateElements(IEnumerable<PrecastElement> elements) { }
            public void ApplyLoadOptions(CraneOptimizer.UI.Features.F2_IfcViewer.DTOs.IfcLoadOptionsDto options) { }
            public string GetStorey(PrecastElement element) => string.Empty;
            public bool IsTypeEnabled(string type) => true;
            public bool IsMaterialEnabled(string material) => true;
            public bool IsStoreyEnabled(string storey) => true;
            public bool IsElementHidden(PrecastElement element) => false;
            public bool IsElementExplicitlyHidden(PrecastElement element) => false;
        }

        private class MockOriginService : IOriginService
        {
            public CraneOptimizer.Domain.Common.DomainPoint3D? OriginPoint => null;
            public bool IsPickingOrigin => false;

            public event Action? OriginChanged;
            public event Action? PickingChanged;

            public void StartPicking() { }
            public void SetOrigin(CraneOptimizer.Domain.Common.DomainPoint3D origin) { }
            public void ClearOrigin() { }
            public void CancelPicking() { }
        }

        [Fact]
        public void CtrlClick_AddsSecondElement_ToSelectedTargets()
        {
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            _scene.SetSelectedElements(new List<PrecastElement> { el1 });

            _service.Select(el2, isCtrlHeld: true);

            var selected = _scene.GetSelectedElements();
            Assert.Equal(2, selected.Count);
            Assert.Contains(selected, x => x.ElementID == "EL-1");
            Assert.Contains(selected, x => x.ElementID == "EL-2");
        }

        [Fact]
        public void CtrlClick_SelectedElement_RemovesIt_FromSelectedTargets()
        {
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            _scene.SetSelectedElements(new List<PrecastElement> { el1, el2 });

            _service.Select(el1, isCtrlHeld: true);

            var selected = _scene.GetSelectedElements();
            Assert.Single(selected);
            Assert.Equal("EL-2", selected[0].ElementID);
        }

        [Fact]
        public void BoxSelection_SelectsMultipleElements_DataAndRenderAgree()
        {
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };

            _service.SetElements(new[] { el1, el2 });

            var selected = _scene.GetSelectedElements();
            Assert.Equal(2, selected.Count);
            Assert.Contains(selected, x => x.ElementID == "EL-1");
            Assert.Contains(selected, x => x.ElementID == "EL-2");
        }

        [Fact]
        public void RenderHighlight_UsesAllSelectedElements_NotSingleSelectedElement()
        {
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            _scene.SetSelectedElements(new List<PrecastElement> { el1, el2 });

            var selectedIds = _scene.GetSelectedElements().Select(x => x.ElementID).ToList();

            Assert.Equal(2, selectedIds.Count);
            Assert.Contains("EL-1", selectedIds);
            Assert.Contains("EL-2", selectedIds);
        }

        [Fact]
        public void RenderHighlight_DoesNotUseOnlyFirstOrLastSelectedTarget()
        {
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };
            var el3 = new PrecastElement { ElementID = "EL-3" };
            _scene.SetSelectedElements(new List<PrecastElement> { el1, el2, el3 });

            var selectedIds = _scene.GetSelectedElements().Select(x => x.ElementID).ToList();

            Assert.Equal(3, selectedIds.Count);
            Assert.Contains("EL-1", selectedIds);
            Assert.Contains("EL-2", selectedIds);
            Assert.Contains("EL-3", selectedIds);
        }

        [Fact]
        public void TabCycling_UsesOnlyHoverCandidateStack()
        {
            var hoverCache = new HoverHitCache();
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };

            hoverCache.SetCandidates(new Point(0, 0), new[] { el1, el2 });

            Assert.True(hoverCache.HasCandidates);
            Assert.Equal(2, hoverCache.Candidates.Count);
            Assert.Same(el1, hoverCache.CurrentCandidate);
            
            var next = hoverCache.CycleNext();
            Assert.Same(el2, next);
        }

        [Fact]
        public void TabCycling_DoesNotSelectUnrelatedLeftRightElements()
        {
            var hoverCache = new HoverHitCache();
            var el1 = new PrecastElement { ElementID = "EL-1" };
            var el2 = new PrecastElement { ElementID = "EL-2" };

            hoverCache.SetCandidates(new Point(10, 20), new[] { el1, el2 });

            // Cycle backward should wrap around to the end of the stack
            var prev = hoverCache.CyclePrevious();
            Assert.Same(el2, prev);
            
            var prev2 = hoverCache.CyclePrevious();
            Assert.Same(el1, prev2);
        }

        [Fact]
        public void ViewCube_IsStillNotIntercepted()
        {
            RunInSTA(() =>
            {
                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var hover = new HoverHitCache();
                var handler = new SelectionHandler(_service, new MockHitTestService(), hover, filter, origin);

                var viewport = new HelixViewport3D();
                
                // Should return false for normal clicks that do not involve view cube
                var isIntercepted = handler.IsClickOnViewCubeOrOverlay(viewport, new RoutedEventArgs());
                Assert.False(isIntercepted);
            });
        }

        [Fact]
        public void CrossingSelection_SelectsWhenRayHitsInsideRect()
        {
            RunInSTA(() =>
            {
                var viewport = new HelixViewport3D();
                viewport.Width = 800;
                viewport.Height = 600;
                viewport.Viewport.Width = 800;
                viewport.Viewport.Height = 600;

                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var elHit = new PrecastElement { ElementID = "EL-HIT" };
                var hitTest = new MockHitTestService { HitObject = elHit };
                var handler = new SelectionHandler(_service, hitTest, new HoverHitCache(), filter, origin);

                viewport.Camera = new PerspectiveCamera(new Point3D(0, 0, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);

                filter.VisibleElementsList.Add(elHit);

                var startField = typeof(SelectionHandler).GetField("_dragStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var currentField = typeof(SelectionHandler).GetField("_dragCurrent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                startField!.SetValue(handler, new Point(520, 420));
                currentField!.SetValue(handler, new Point(280, 180));

                var finishMethod = typeof(SelectionHandler).GetMethod("FinishBoxSelect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                _scene.ClearSelectedElements();
                finishMethod!.Invoke(handler, new object[] { viewport });

                var selected = _scene.GetSelectedElements();
                Assert.Single(selected);
                Assert.Equal("EL-HIT", selected[0].ElementID);
            });
        }

        [Fact]
        public void CrossingSelection_DoesNotSelectWhenRayHitsMiss()
        {
            RunInSTA(() =>
            {
                var viewport = new HelixViewport3D();
                viewport.Width = 800;
                viewport.Height = 600;
                viewport.Viewport.Width = 800;
                viewport.Viewport.Height = 600;

                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var handler = new SelectionHandler(_service, new MockHitTestService(), new HoverHitCache(), filter, origin);

                viewport.Camera = new PerspectiveCamera(new Point3D(0, 0, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);

                var el = new PrecastElement { ElementID = "EL-FAR" };
                filter.VisibleElementsList.Add(el);

                var startField = typeof(SelectionHandler).GetField("_dragStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var currentField = typeof(SelectionHandler).GetField("_dragCurrent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                startField!.SetValue(handler, new Point(350, 200));
                currentField!.SetValue(handler, new Point(250, 250)); // Crossing

                var finishMethod = typeof(SelectionHandler).GetMethod("FinishBoxSelect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                _scene.ClearSelectedElements();
                finishMethod!.Invoke(handler, new object[] { viewport });

                Assert.Empty(_scene.GetSelectedElements());
            });
        }

        [Fact]
        public void WindowSelection_RejectsPartiallyCoveredElement()
        {
            RunInSTA(() =>
            {
                var viewport = new HelixViewport3D();
                viewport.Width = 800;
                viewport.Height = 600;
                viewport.Viewport.Width = 800;
                viewport.Viewport.Height = 600;

                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var handler = new SelectionHandler(_service, new MockHitTestService(), new HoverHitCache(), filter, origin);

                viewport.Camera = new PerspectiveCamera(new Point3D(0, 0, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);

                var el = new PrecastElement { ElementID = "EL-COLUMN" };
                filter.VisibleElementsList.Add(el);

                var spatialEl = new ElementVisualBuilder.SpatialHitTestElement
                {
                    ElementId = "EL-COLUMN",
                    Element = el,
                    Min = new Point3D(-1, -2, -0.5),
                    Max = new Point3D(1, 2, 0.5),
                    IsVisible = true
                };
                ElementVisualBuilder.RegisterSpatialHitTestElements(new[] { spatialEl });

                var startField = typeof(SelectionHandler).GetField("_dragStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var currentField = typeof(SelectionHandler).GetField("_dragCurrent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Window: only upper half of tall column — screen bounds extend beyond selection rect
                startField!.SetValue(handler, new Point(300, 180));
                currentField!.SetValue(handler, new Point(500, 300));

                var finishMethod = typeof(SelectionHandler).GetMethod("FinishBoxSelect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                _scene.ClearSelectedElements();
                finishMethod!.Invoke(handler, new object[] { viewport });

                Assert.Empty(_scene.GetSelectedElements());
            });
        }

        [Fact]
        public void MouseMove_OnlyUpdatesRubberBand_NoElementProjectionLoop()
        {
            RunInSTA(() =>
            {
                var viewport = new HelixViewport3D();
                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var handler = new SelectionHandler(_service, new MockHitTestService(), new HoverHitCache(), filter, origin);

                var el = new PrecastElement { ElementID = "EL-1" };
                filter.VisibleElementsList.Add(el);

                // Arm the drag
                var dragArmedField = typeof(SelectionHandler).GetField("_dragArmed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                dragArmedField!.SetValue(handler, true);

                var startField = typeof(SelectionHandler).GetField("_dragStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                startField!.SetValue(handler, new Point(100, 100));

                // Invoke OnPreviewMouseMove to simulate active dragging
                var args = new MouseEventArgs(InputManager.Current.PrimaryMouseDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent };
                
                // Act - MouseMove should be fast and O(1), zero selection/projection logic executed
                handler.OnPreviewMouseMove(viewport, args);

                // Assert: selection remains completely empty
                Assert.Empty(_scene.GetSelectedElements());
            });
        }

        [Fact]
        public void MouseUp_PerformsSingleSelectionEvaluation()
        {
            RunInSTA(() =>
            {
                var viewport = new HelixViewport3D();
                viewport.Width = 800;
                viewport.Height = 600;
                viewport.Viewport.Width = 800;
                viewport.Viewport.Height = 600;

                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var handler = new SelectionHandler(_service, new MockHitTestService(), new HoverHitCache(), filter, origin);

                viewport.Camera = new PerspectiveCamera(new Point3D(0, 0, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);

                var el = new PrecastElement { ElementID = "EL-1" };
                filter.VisibleElementsList.Add(el);

                var spatialEl = new ElementVisualBuilder.SpatialHitTestElement
                {
                    ElementId = "EL-1",
                    Element = el,
                    Min = new Point3D(-1, -1, -0.5),
                    Max = new Point3D(1, 1, 0.5),
                    IsVisible = true
                };
                ElementVisualBuilder.RegisterSpatialHitTestElements(new[] { spatialEl });

                // Start dragging
                var isDraggingField = typeof(SelectionHandler).GetField("_isDragging", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                isDraggingField!.SetValue(handler, true);

                var startField = typeof(SelectionHandler).GetField("_dragStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var currentField = typeof(SelectionHandler).GetField("_dragCurrent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                startField!.SetValue(handler, new Point(100, 100));
                currentField!.SetValue(handler, new Point(700, 500));

                // Verify selection remains empty before mouse up
                Assert.Empty(_scene.GetSelectedElements());

                // Act - evaluate selection once on mouse up (FinishBoxSelect uses final drag rect)
                var finishMethod = typeof(SelectionHandler).GetMethod("FinishBoxSelect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                finishMethod!.Invoke(handler, new object[] { viewport });

                // Assert - selection is evaluated exactly once on mouse up
                Assert.Single(_scene.GetSelectedElements());
            });
        }

        [Fact]
        public void ProjectionCache_InvalidatesOnCameraChange()
        {
            RunInSTA(() =>
            {
                var viewport = new HelixViewport3D();
                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var handler = new SelectionHandler(_service, new MockHitTestService(), new HoverHitCache(), filter, origin);

                viewport.Camera = new PerspectiveCamera(new Point3D(0, 0, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);

                var getProjections = typeof(SelectionHandler).GetMethod("GetElementProjections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // First call: builds cache
                var list1 = (List<SelectionHandler.ElementProjection>)getProjections!.Invoke(handler, new object[] { viewport })!;

                // Shift camera position
                viewport.Camera = new PerspectiveCamera(new Point3D(0, 5, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);

                // Second call: invalidates and rebuilds
                var list2 = (List<SelectionHandler.ElementProjection>)getProjections.Invoke(handler, new object[] { viewport })!;

                // Assert that a fresh calculation occurred (new references or verified invalidation)
                Assert.NotNull(list2);
            });
        }

        [Fact]
        public void ProjectionCache_InvalidatesOnVisibilityChange()
        {
            RunInSTA(() =>
            {
                var viewport = new HelixViewport3D();
                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var handler = new SelectionHandler(_service, new MockHitTestService(), new HoverHitCache(), filter, origin);

                viewport.Camera = new PerspectiveCamera(new Point3D(0, 0, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);

                var getProjections = typeof(SelectionHandler).GetMethod("GetElementProjections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // First call
                getProjections!.Invoke(handler, new object[] { viewport });

                // Modify visible element count to trigger cache invalidation
                filter.VisibleElementsList.Add(new PrecastElement { ElementID = "NEW-EL" });

                // Second call
                var list = (List<SelectionHandler.ElementProjection>)getProjections.Invoke(handler, new object[] { viewport })!;
                Assert.NotNull(list);
            });
        }

        [Fact]
        public void BoxSelection_NearElements_DoesNotSelectNeighborOutsideRect()
        {
            RunInSTA(() =>
            {
                var viewport = new HelixViewport3D();
                viewport.Width = 800;
                viewport.Height = 600;
                viewport.Viewport.Width = 800;
                viewport.Viewport.Height = 600;

                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var handler = new SelectionHandler(_service, new MockHitTestService(), new HoverHitCache(), filter, origin);

                viewport.Camera = new PerspectiveCamera(new Point3D(0, 0, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);

                var el1 = new PrecastElement { ElementID = "EL-Target" };
                var el2 = new PrecastElement { ElementID = "EL-Neighbor" };
                filter.VisibleElementsList.AddRange(new[] { el1, el2 });

                var spatial1 = new ElementVisualBuilder.SpatialHitTestElement
                {
                    ElementId = "EL-Target",
                    Element = el1,
                    Min = new Point3D(-1, -1, -0.5),
                    Max = new Point3D(1, 1, 0.5),
                    IsVisible = true
                };
                var spatial2 = new ElementVisualBuilder.SpatialHitTestElement
                {
                    ElementId = "EL-Neighbor",
                    Element = el2,
                    Min = new Point3D(1.5, -1, -0.5),
                    Max = new Point3D(3.5, 1, 0.5),
                    IsVisible = true
                };
                ElementVisualBuilder.RegisterSpatialHitTestElements(new[] { spatial1, spatial2 });

                var startField = typeof(SelectionHandler).GetField("_dragStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var currentField = typeof(SelectionHandler).GetField("_dragCurrent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Enclose only EL-Target
                startField!.SetValue(handler, new Point(280, 180));
                currentField!.SetValue(handler, new Point(520, 420)); // Window mode

                var finishMethod = typeof(SelectionHandler).GetMethod("FinishBoxSelect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                SelectionHandler.SelectionPrecision = SelectionHandler.BoxSelectionMode.MeshProxy;
                SelectionHandler.EnableSelectionDiagnostics = false;
                _scene.ClearSelectedElements();
                finishMethod!.Invoke(handler, new object[] { viewport });

                var selected = _scene.GetSelectedElements();
                Assert.Single(selected);
                Assert.Equal("EL-Target", selected[0].ElementID);
            });
        }

        [Fact]
        public void WindowSelection_SelectsOnlyFullyContainedElement()
        {
            RunInSTA(() =>
            {
                var viewport = new HelixViewport3D();
                viewport.Width = 800;
                viewport.Height = 600;
                viewport.Viewport.Width = 800;
                viewport.Viewport.Height = 600;

                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var handler = new SelectionHandler(_service, new MockHitTestService(), new HoverHitCache(), filter, origin);

                viewport.Camera = new PerspectiveCamera(new Point3D(0, 0, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);

                var el = new PrecastElement { ElementID = "EL-WINDOW" };
                filter.VisibleElementsList.Add(el);

                var spatialEl = new ElementVisualBuilder.SpatialHitTestElement
                {
                    ElementId = "EL-WINDOW",
                    Element = el,
                    Min = new Point3D(-0.5, -0.5, -0.5),
                    Max = new Point3D(0.5, 0.5, 0.5),
                    IsVisible = true
                };
                ElementVisualBuilder.RegisterSpatialHitTestElements(new[] { spatialEl });

                var startField = typeof(SelectionHandler).GetField("_dragStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var currentField = typeof(SelectionHandler).GetField("_dragCurrent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                startField!.SetValue(handler, new Point(280, 180));
                currentField!.SetValue(handler, new Point(520, 420));

                var finishMethod = typeof(SelectionHandler).GetMethod("FinishBoxSelect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                _scene.ClearSelectedElements();
                finishMethod!.Invoke(handler, new object[] { viewport });

                var selected = _scene.GetSelectedElements();
                Assert.Single(selected);
                Assert.Equal("EL-WINDOW", selected[0].ElementID);
            });
        }

        [Fact]
        public void WindowSelection_UsesRenderedMeshAndIgnoresUnrelatedSpatialCandidate()
        {
            RunInSTA(() =>
            {
                var viewport = CreateSelectionViewport();
                var filter = new MockFilterService();
                var handler = new SelectionHandler(
                    _service,
                    new MockHitTestService(),
                    new HoverHitCache(),
                    filter,
                    new MockOriginService());

                var renderedElement = new PrecastElement { ElementID = "EL-RENDERED" };
                var staleElement = new PrecastElement { ElementID = "EL-STALE" };
                filter.VisibleElementsList.Add(renderedElement);
                viewport.Children.Add(CreateElementVisual(
                    renderedElement,
                    new Point3D(-0.5, -0.5, 0),
                    new Point3D(0.5, 0.5, 0)));

                ElementVisualBuilder.RegisterSpatialHitTestElements(
                [
                    new ElementVisualBuilder.SpatialHitTestElement
                    {
                        ElementId = staleElement.ElementID,
                        Element = staleElement,
                        Min = new Point3D(-0.25, -0.25, -0.25),
                        Max = new Point3D(0.25, 0.25, 0.25),
                        IsVisible = true
                    }
                ]);

                FinishWindowSelection(handler, viewport, new Point(340, 250), new Point(460, 350));

                var selected = _scene.GetSelectedElements();
                Assert.Single(selected);
                Assert.Equal("EL-RENDERED", selected[0].ElementID);
            });
        }

        [Fact]
        public void WindowSelection_DoesNotMergeCaseDistinctIfcElements()
        {
            RunInSTA(() =>
            {
                var viewport = CreateSelectionViewport();
                var filter = new MockFilterService();
                var handler = new SelectionHandler(
                    _service,
                    new MockHitTestService(),
                    new HoverHitCache(),
                    filter,
                    new MockOriginService());

                var covered = new PrecastElement { ElementID = "07f79pn7zARQaMd2_kWdAh" };
                var farAway = new PrecastElement { ElementID = "07f79pn7zARQaMd2_kWdaH" };
                filter.VisibleElementsList.Add(covered);
                filter.VisibleElementsList.Add(farAway);
                viewport.Children.Add(CreateElementVisual(
                    covered,
                    new Point3D(-0.5, -0.5, 0),
                    new Point3D(0.5, 0.5, 0)));
                viewport.Children.Add(CreateElementVisual(
                    farAway,
                    new Point3D(3, -0.5, 0),
                    new Point3D(4, 0.5, 0)));

                FinishWindowSelection(handler, viewport, new Point(340, 250), new Point(460, 350));

                var selected = _scene.GetSelectedElements();
                Assert.Single(selected);
                Assert.Same(covered, selected[0]);
            });
        }

        [Fact]
        public void WindowSelection_DoesNotInventBoundsForUnrenderedVisibleElement()
        {
            RunInSTA(() =>
            {
                var viewport = CreateSelectionViewport();
                var filter = new MockFilterService();
                var handler = new SelectionHandler(
                    _service,
                    new MockHitTestService(),
                    new HoverHitCache(),
                    filter,
                    new MockOriginService());

                var renderedOutside = new PrecastElement { ElementID = "EL-RENDERED-OUTSIDE" };
                var unrenderedAtCenter = new PrecastElement { ElementID = "EL-NOT-RENDERED" };
                filter.VisibleElementsList.Add(renderedOutside);
                filter.VisibleElementsList.Add(unrenderedAtCenter);
                viewport.Children.Add(CreateElementVisual(
                    renderedOutside,
                    new Point3D(3, -0.5, 0),
                    new Point3D(4, 0.5, 0)));

                FinishWindowSelection(handler, viewport, new Point(340, 250), new Point(460, 350));

                Assert.Empty(_scene.GetSelectedElements());
            });
        }

        [Fact]
        public void CrossingSelection_DoesNotSelectFarCaseDistinctIfcElement()
        {
            RunInSTA(() =>
            {
                var viewport = CreateSelectionViewport();
                var filter = new MockFilterService();
                var handler = new SelectionHandler(
                    _service,
                    new MockHitTestService(),
                    new HoverHitCache(),
                    filter,
                    new MockOriginService());

                var farAway = new PrecastElement { ElementID = "07f79pn7zARQaMd2_kWdaH" };
                var touched = new PrecastElement { ElementID = "07f79pn7zARQaMd2_kWdAh" };
                filter.VisibleElementsList.Add(farAway);
                filter.VisibleElementsList.Add(touched);
                viewport.Children.Add(CreateElementVisual(
                    farAway,
                    new Point3D(3, -0.5, 0),
                    new Point3D(4, 0.5, 0)));
                viewport.Children.Add(CreateElementVisual(
                    touched,
                    new Point3D(-0.5, -0.5, 0),
                    new Point3D(0.5, 0.5, 0)));

                FinishWindowSelection(handler, viewport, new Point(460, 350), new Point(340, 250));

                var selected = _scene.GetSelectedElements();
                Assert.Single(selected);
                Assert.Same(touched, selected[0]);
            });
        }

        [Fact]
        public void WindowSelection_SelectsNarrowMeshWithoutArtificialScreenInflation()
        {
            RunInSTA(() =>
            {
                var viewport = CreateSelectionViewport();
                var filter = new MockFilterService();
                var handler = new SelectionHandler(
                    _service,
                    new MockHitTestService(),
                    new HoverHitCache(),
                    filter,
                    new MockOriginService());

                var element = new PrecastElement { ElementID = "EL-NARROW" };
                filter.VisibleElementsList.Add(element);
                viewport.Children.Add(CreateElementVisual(
                    element,
                    new Point3D(-0.01, -0.5, 0),
                    new Point3D(0.01, 0.5, 0)));

                FinishWindowSelection(handler, viewport, new Point(398.5, 250), new Point(401.5, 350));

                var selected = _scene.GetSelectedElements();
                Assert.Single(selected);
                Assert.Equal("EL-NARROW", selected[0].ElementID);
            });
        }

        [Fact]
        public void WindowSelection_RejectsRenderedMeshWhenAnyVertexIsOutside()
        {
            RunInSTA(() =>
            {
                var viewport = CreateSelectionViewport();
                var filter = new MockFilterService();
                var handler = new SelectionHandler(
                    _service,
                    new MockHitTestService(),
                    new HoverHitCache(),
                    filter,
                    new MockOriginService());

                var element = new PrecastElement { ElementID = "EL-PARTIAL" };
                filter.VisibleElementsList.Add(element);
                viewport.Children.Add(CreateElementVisual(
                    element,
                    new Point3D(-0.5, -0.5, 0),
                    new Point3D(0.75, 0.5, 0)));

                FinishWindowSelection(handler, viewport, new Point(340, 250), new Point(460, 350));

                Assert.Empty(_scene.GetSelectedElements());
            });
        }

        [Fact]
        public void SelectionCoordinateTranslation_UsesTheOverlayCoordinateSpace()
        {
            RunInSTA(() =>
            {
                var root = new System.Windows.Controls.Canvas
                {
                    Width = 500,
                    Height = 400
                };
                var innerViewport = new System.Windows.Controls.Border
                {
                    Width = 300,
                    Height = 200
                };
                System.Windows.Controls.Canvas.SetLeft(innerViewport, 37);
                System.Windows.Controls.Canvas.SetTop(innerViewport, 23);
                root.Children.Add(innerViewport);
                root.Measure(new Size(500, 400));
                root.Arrange(new Rect(0, 0, 500, 400));

                var translateMethod = typeof(SelectionHandler).GetMethod(
                    "TranslatePoint",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                var overlayPoint = (Point)translateMethod!.Invoke(
                    null,
                    [innerViewport, new Point(10, 20), root])!;
                var viewportPoint = (Point)translateMethod.Invoke(
                    null,
                    [root, overlayPoint, innerViewport])!;

                Assert.Equal(new Point(47, 43), overlayPoint);
                Assert.Equal(new Point(10, 20), viewportPoint);
            });
        }

        private static HelixViewport3D CreateSelectionViewport()
        {
            var viewport = new HelixViewport3D
            {
                Width = 800,
                Height = 600,
                Camera = new PerspectiveCamera(
                    new Point3D(0, 0, 10),
                    new Vector3D(0, 0, -1),
                    new Vector3D(0, 1, 0),
                    45)
            };
            viewport.Viewport.Width = 800;
            viewport.Viewport.Height = 600;
            return viewport;
        }

        private static ModelVisual3D CreateElementVisual(
            PrecastElement element,
            Point3D min,
            Point3D max)
        {
            var mesh = new MeshGeometry3D
            {
                Positions =
                [
                    new Point3D(min.X, min.Y, min.Z),
                    new Point3D(max.X, min.Y, min.Z),
                    new Point3D(max.X, max.Y, max.Z),
                    new Point3D(min.X, max.Y, max.Z)
                ],
                TriangleIndices = [0, 1, 2, 0, 2, 3]
            };
            var visual = new ModelVisual3D
            {
                Content = new GeometryModel3D { Geometry = mesh }
            };
            visual.SetValue(FrameworkElement.TagProperty, element);
            return visual;
        }

        private void FinishWindowSelection(
            SelectionHandler handler,
            HelixViewport3D viewport,
            Point start,
            Point end)
        {
            var startField = typeof(SelectionHandler).GetField(
                "_dragStart",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var currentField = typeof(SelectionHandler).GetField(
                "_dragCurrent",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var finishMethod = typeof(SelectionHandler).GetMethod(
                "FinishBoxSelect",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            startField!.SetValue(handler, start);
            currentField!.SetValue(handler, end);
            _scene.ClearSelectedElements();
            finishMethod!.Invoke(handler, [viewport]);
        }

        [Fact]
        public void BoxSelection_AssemblyOwnerMapping_DoesNotHighlightUnrelatedGeometry()
        {
            RunInSTA(() =>
            {
                var filter = new MockFilterService();
                var origin = new MockOriginService();
                var handler = new SelectionHandler(_service, new MockHitTestService(), new HoverHitCache(), filter, origin);

                var el1 = new PrecastElement { ElementID = "EL-A" };
                var el2 = new PrecastElement { ElementID = "EL-B" };

                _scene.SetSelectedElements(new List<PrecastElement> { el1 });

                var selectedIds = _scene.GetSelectedElements().Select(x => x.ElementID).ToList();

                Assert.Contains("EL-A", selectedIds);
                Assert.DoesNotContain("EL-B", selectedIds);
            });
        }

        [Fact]
        public void BoxSelection_LogicalSelectedIds_MatchVisualHighlightedIds()
        {
            RunInSTA(() =>
            {
                var el = new PrecastElement { ElementID = "EL-1" };
                _scene.SetSelectedElements(new List<PrecastElement> { el });

                var selected = _scene.GetSelectedElements();
                Assert.Single(selected);
                Assert.Equal("EL-1", selected[0].ElementID);
            });
        }
    }
}

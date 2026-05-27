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
using System.Windows.Media.Media3D;
using System.Windows;
using HelixToolkit.Wpf;

namespace CraneOptimizer.Application.Tests
{
    public class ViewportSelectionTests
    {
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
            var t = new System.Threading.Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Assert.Fail(ex.ToString());
                }
            });
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            t.Join();
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
        public void SelectionHandler_DoesNotHandleMouseDownBeforeDragThreshold()
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
                Assert.False(eDown.Handled);
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
            public void ResetFilters() { }
            public void ShowAll() { }
            public void HideElements(IEnumerable<PrecastElement> elements) { }
            public void IsolateElements(IEnumerable<PrecastElement> elements) { }
            public void ApplyLoadOptions(CraneOptimizer.UI.Features.F2_IfcViewer.DTOs.IfcLoadOptionsDto options) { }
            public string GetStorey(PrecastElement element) => string.Empty;
            public bool IsTypeEnabled(string type) => true;
            public bool IsMaterialEnabled(string material) => true;
            public bool IsStoreyEnabled(string storey) => true;
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
    }
}

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
    }
}

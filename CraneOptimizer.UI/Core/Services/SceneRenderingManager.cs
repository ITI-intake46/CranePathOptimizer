using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F3_CraneSetup;
using System.Diagnostics;

namespace CraneOptimizer.UI.Core.Services
{
    public sealed class SceneRenderingManager : ISceneRenderingManager
    {
        private readonly ISceneService _scene;
        private readonly IRenderingService _rendering;
        private readonly IIfcReaderRepository _ifcReader;
        private readonly ICraneValidationService _craneValidation;
        private readonly List<PrecastElement> _sceneElements = new();
        private SceneRenderingContext? _context;
        private bool _disposed;
        private bool _showCraneRadius = true;

        public SceneRenderingManager(
            ISceneService scene,
            IRenderingService rendering,
            IIfcReaderRepository ifcReader,
            ICraneValidationService craneValidation)
        {
            _scene = scene;
            _rendering = rendering;
            _ifcReader = ifcReader;
            _craneValidation = craneValidation;

            _scene.ElementsChanged += OnElementsChanged;
            _scene.CraneAdded += OnCraneChanged;
            _scene.CraneMoved += OnCraneChanged;
            _scene.CraneUpdated += OnCraneChanged;
            _scene.CraneRemoved += OnCraneChanged;
            _scene.CranesCleared += OnCranesCleared;
            _scene.ZoneAdded += OnZoneChanged;
            _scene.ZoneUpdated += OnZoneChanged;
            _scene.ZoneRemoved += OnZoneChanged;
            _scene.ZonesCleared += ReRenderCranes;
        }

        public event Action<IReadOnlyList<PrecastElement>>? ElementsChanged;
        public event Action? ElementsCleared;

        public bool ShowCraneRadius
        {
            get => _showCraneRadius;
            set
            {
                if (_showCraneRadius == value)
                    return;

                _showCraneRadius = value;
                ReRenderCranes();
            }
        }

        public void Configure(SceneRenderingContext context)
        {
            _context = context;
        }

        public void ReRenderElements()
        {
            if (_context == null)
                return;

            _rendering.ClearLayer("Elements");

            if (_sceneElements.Count == 0)
                return;

            Debug.WriteLine("Rendering Elements: " + _sceneElements.Count);

            var statusMap = _context.GetLiftResults()
                ?.Where(result => !string.IsNullOrWhiteSpace(result.ElementId))
                .GroupBy(result => result.ElementId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Status, StringComparer.OrdinalIgnoreCase);

            var selectedElementId = _context.GetSelectedElementId();
            var origin = _context.GetOriginPoint();
            var placementOffset = _context.GetPlacementOffset();

            _rendering.Render(
                _sceneElements.Select(element =>
                {
                    LiftStatus? status = null;
                    if (statusMap != null && statusMap.TryGetValue(element.ElementID, out var liftStatus))
                        status = liftStatus;

                    return (element, status, element.ElementID == selectedElementId);
                }),
                new ElementVisualBuilder(_ifcReader, origin, placementOffset),
                "Elements");
        }

        public void ReRenderCranes()
        {
            if (_context == null)
                return;

            var cranes = _scene.GetCranes();
            var zones = _context.GetZones().ToList();

            foreach (var crane in cranes)
                crane.IsValidPlacement = _craneValidation.ValidatePlacement(crane, zones);

            _rendering.Render(cranes, new CraneVisualBuilder(), "Cranes");

            if (ShowCraneRadius)
                _rendering.Render(cranes, new CraneRadiusBuilder(), "CraneRadius");
            else
                _rendering.ClearLayer("CraneRadius");
        }

        private void OnElementsChanged(List<PrecastElement> elements)
        {
            Debug.WriteLine("Scene ElementsChanged: " + elements.Count);

            _sceneElements.Clear();
            _sceneElements.AddRange(elements);

            if (_sceneElements.Count == 0)
            {
                _rendering.ClearLayer("Elements");
                ElementsCleared?.Invoke();
                return;
            }

            ElementsChanged?.Invoke(_sceneElements);
            ReRenderElements();
        }

        private void OnCraneChanged(SceneCrane _) => ReRenderCranes();
        private void OnZoneChanged(GreenAreaZone _) => ReRenderCranes();

        private void OnCranesCleared()
        {
            _rendering.ClearLayer("Cranes");
            _rendering.ClearLayer("CraneRadius");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _scene.ElementsChanged -= OnElementsChanged;
            _scene.CraneAdded -= OnCraneChanged;
            _scene.CraneMoved -= OnCraneChanged;
            _scene.CraneUpdated -= OnCraneChanged;
            _scene.CraneRemoved -= OnCraneChanged;
            _scene.CranesCleared -= OnCranesCleared;
            _scene.ZoneAdded -= OnZoneChanged;
            _scene.ZoneUpdated -= OnZoneChanged;
            _scene.ZoneRemoved -= OnZoneChanged;
            _scene.ZonesCleared -= ReRenderCranes;
        }
    }
}

using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public class CranePlacementService : ICranePlacementService
    {
        private readonly IOptimizationInputBuilder _inputBuilder;
        private readonly ICranePlacementEngine _placementEngine;
        private readonly IOptimizationScopeEngine _scopeEngine;
        private readonly ISceneService _sceneService;
        private readonly CoordinateService _coordinateService;

        public CranePlacementService(
            IOptimizationInputBuilder inputBuilder,
            ICranePlacementEngine placementEngine,
            IOptimizationScopeEngine scopeEngine,
            ISceneService sceneService,
            CoordinateService coordinateService)
        {
            _inputBuilder = inputBuilder;
            _placementEngine = placementEngine;
            _scopeEngine = scopeEngine;
            _sceneService = sceneService;
            _coordinateService = coordinateService;
        }

        public async Task<CranePlacementResult> RunOptimizationAsync(
            GASettings settings,
            Crane crane,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sceneElements = _sceneService.GetElements();
            if (sceneElements == null || sceneElements.Count == 0)
                throw new InvalidOperationException("No elements found in the scene.");

            var sceneZones = _sceneService.GetZones();

            // Filter elements using OptimizationScopeEngine before normalizing
            var selectedElements = _sceneService.GetSelectedElements();
            var selectedIds = (selectedElements != null && selectedElements.Count > 0)
                ? selectedElements.Select(e => e.ElementID).Where(id => !string.IsNullOrEmpty(id)).ToHashSet()
                : null;

            var filter = new OptimizationScopeFilter
            {
                SelectedElementIds = selectedIds
            };

            var filteredElements = _scopeEngine.FilterElements(sceneElements, filter);

            // Normalize elements to meters
            var elements = new List<PrecastElement>(filteredElements.Count);
            for (int i = 0; i < filteredElements.Count; i++)
            {
                var element = filteredElements[i];

                if (!_coordinateService.IsValidPoint(element.ElementCOG))
                    throw new InvalidOperationException($"Invalid coordinates found for element '{element.ElementID}'.");

                elements.Add(_coordinateService.NormalizeElementToMeters(element));
            }

            // Normalize zones to meters
            var zones = new List<GreenAreaZone>(sceneZones.Count);
            for (int i = 0; i < sceneZones.Count; i++)
            {
                zones.Add(_coordinateService.NormalizeZoneToMeters(sceneZones[i]));
            }

            // Build input in normalized meters
            var input = _inputBuilder.Build(settings, crane, elements, zones);

            // Run optimization in a Task
            var appResult = await Task.Run(
                () => _placementEngine.RunOptimization(input, cancellationToken),
                cancellationToken);

            // Map results back to UI DTOs
            var finalResult = MapPlacementResult(appResult, sceneElements, input.CraneSpec, input.Settings);

            // Write debug report
            WriteDebugReport(finalResult);

            return finalResult;
        }

        private CranePlacementResult MapPlacementResult(
            PlacementResult appResult,
            List<PrecastElement> allElements,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            var result = new CranePlacementResult
            {
                FitnessScore = appResult.FitnessScore,
                CoverageRatio = appResult.CoverageRatio,
                LiftedElementCount = appResult.LiftedElementCount,
                TotalElementCount = appResult.TotalElementCount,
                Statistics = new DTOs.LiftStatistics
                {
                    Liftable = appResult.Statistics.Liftable,
                    OutOfRadius = appResult.Statistics.OutOfRadius,
                    Overweight = appResult.Statistics.Overweight,
                    HeightFail = appResult.Statistics.HeightFail,
                    SpecialCase = appResult.Statistics.SpecialCase
                },
                Positions = new List<DTOs.CranePositionResult>(appResult.Positions.Count)
            };

            var elementLookup = allElements
                .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
                .GroupBy(element => element.ElementID, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var appPos in appResult.Positions)
            {
                var scenePoint = _coordinateService.DenormalizePoint(new Domain.Common.DomainPoint3D(appPos.X, appPos.Y, appPos.Z));
                var posResult = new DTOs.CranePositionResult
                {
                    X = scenePoint.X,
                    Y = scenePoint.Y,
                    Z = scenePoint.Z,
                    NewLiftableCount = appPos.NewLiftableCount,
                    ElementResults = appPos.ElementResults.Select(appElem =>
                    {
                        elementLookup.TryGetValue(appElem.ElementId, out var element);
                        var sceneCog = element == null
                            ? new Domain.Common.DomainPoint3D()
                            : _coordinateService.DenormalizePoint(element.ElementCOG);

                        return new DTOs.ElementLiftResult
                        {
                            ElementId = appElem.ElementId,
                            ElementMark = appElem.ElementMark,
                            ElementType = appElem.ElementType,
                            Weight = appElem.Weight,
                            CogX = sceneCog.X,
                            CogY = sceneCog.Y,
                            CogZ = sceneCog.Z,
                            Status = appElem.Status,
                            Radius = appElem.Radius,
                            BoomLength = appElem.BoomLength,
                            AvailableCapacity = appElem.AvailableCapacity,
                            RequiredCapacity = appElem.RequiredCapacity
                        };
                    }).ToList()
                };
                result.Positions.Add(posResult);
            }

            return result;
        }

        private static void WriteDebugReport(CranePlacementResult result)
        {
            try
            {
                result.DebugReportPath = GADebugReportWriter.Write(result);
                result.DebugReportError = null;
            }
            catch (Exception ex)
            {
                result.DebugReportPath = null;
                result.DebugReportError = ex.Message;
            }
        }
    }
}

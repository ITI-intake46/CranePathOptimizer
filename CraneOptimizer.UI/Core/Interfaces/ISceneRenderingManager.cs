using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ISceneRenderingManager : IDisposable
    {
        event Action<IReadOnlyList<PrecastElement>>? ElementsChanged;
        event Action? ElementsCleared;

        bool ShowCraneRadius { get; set; }

        void Configure(SceneRenderingContext context);
        void ReRenderElements();
        void ReRenderCranes();
    }

    public sealed class SceneRenderingContext
    {
        public required Func<DomainPoint3D?> GetOriginPoint { get; init; }
        public required Func<DomainPoint3D?> GetPlacementOffset { get; init; }
        public required Func<IReadOnlyList<ElementLiftResult>?> GetLiftResults { get; init; }
        public required Func<string?> GetSelectedElementId { get; init; }
        public required Func<IReadOnlyList<GreenAreaZone>> GetZones { get; init; }
        public Func<GAViewMode>? GetGAViewMode { get; init; }
    }
}

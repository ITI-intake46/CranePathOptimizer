using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Models;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public record LiftFeasibilityResult(bool IsFeasible, string Reason, double RequiredRadius, double RequiredHeight);

    public interface ICraneValidationService
    {
        bool ValidatePlacement(SceneCrane crane, IReadOnlyList<GreenAreaZone> zones);
        LiftFeasibilityResult ValidateLiftFeasibility(SceneCrane crane, PrecastElement element);
        double EvaluateCraneFitness(SceneCrane crane, IReadOnlyList<PrecastElement> elements, IReadOnlyList<GreenAreaZone> zones);
    }
}

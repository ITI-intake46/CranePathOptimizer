using CraneOptimizer.Domain.Entities;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public interface ICraneOptimizerDialogService
    {
        CraneOptimizerDialogOutcome? ShowDialog();
    }

    public sealed class CraneOptimizerDialogOutcome
    {
        public CraneOptimizerDialogOutcome(
            CranePlacementResult result,
            CranePositionResult? appliedPosition,
            Crane? appliedCrane)
        {
            Result = result;
            AppliedPosition = appliedPosition;
            AppliedCrane = appliedCrane;
        }

        public CranePlacementResult Result { get; }
        public CranePositionResult? AppliedPosition { get; }
        public Crane? AppliedCrane { get; }
    }
}

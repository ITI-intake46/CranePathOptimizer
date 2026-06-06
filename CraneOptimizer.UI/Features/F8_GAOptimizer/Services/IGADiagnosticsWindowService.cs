using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public interface IGADiagnosticsWindowService
    {
        void Show(GAViewMode currentMode = GAViewMode.FinalAssignment);
    }
}

using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    public interface ILoadChartCapacityResolver
    {
        LoadChartResolverResult Resolve(
            CraneSpecification craneSpec,
            double radius,
            double boomLength,
            double elementWeight,
            GASettings settings,
            bool buildDiagnostics = true,
            LoadChartIndex? index = null);
    }
}

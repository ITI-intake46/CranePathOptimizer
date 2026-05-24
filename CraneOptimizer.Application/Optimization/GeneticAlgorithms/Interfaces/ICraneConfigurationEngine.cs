using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    public interface ICraneConfigurationEngine
    {
        double CalculateRequiredBoomLength(double radius, double height);
        double CraneCapacity(CraneSpecification craneSpec, double radius, double boomLength, GASettings settings);
        CraneCapacityDiagnosticResult EvaluateCraneCapacityDiagnostic(CraneSpecification craneSpec, double radius, double boomLength, GASettings settings);
        bool ExceedsMaxRadius(double radius, CraneSpecification craneSpec);
        bool ExceedsMaxBoomLength(double boomLength, CraneSpecification craneSpec);
        void ClearCache();
    }
}

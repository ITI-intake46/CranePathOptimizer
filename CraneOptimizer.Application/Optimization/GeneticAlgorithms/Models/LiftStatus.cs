namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    /// <summary>
    /// Represents the possible lift evaluation outcomes.
    /// </summary>
    public enum LiftStatus
    {
        Liftable,
        OutOfRadius,
        Overweight,
        HeightFail,
        SpecialCase,
        NotCharted
    }
}
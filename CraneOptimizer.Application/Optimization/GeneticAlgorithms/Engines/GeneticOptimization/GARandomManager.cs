using System;
using System.Threading;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization
{
    /// <summary>
    /// Provides a thread-safe, centralized Random instance for the entire GA execution flow.
    /// This ensures complete determinism when a seed is provided, even across multiple concurrent optimization runs.
    /// </summary>
    public static class GARandomManager
    {
        private static readonly AsyncLocal<Random> _random = new AsyncLocal<Random>();

        /// <summary>
        /// Gets the centralized Random instance for the current execution context.
        /// If not initialized, defaults to an unseeded Random.
        /// </summary>
        public static Random Instance
        {
            get
            {
                if (_random.Value == null)
                    _random.Value = new Random();
                
                return _random.Value;
            }
        }

        /// <summary>
        /// Initializes the Random instance with the provided seed for deterministic behavior.
        /// Call this exactly once at the start of GAEngine.Run.
        /// </summary>
        /// <param name="seed">The deterministic random seed, or null for random behavior.</param>
        public static void Initialize(int? seed)
        {
            _random.Value = seed.HasValue ? new Random(seed.Value) : new Random();
        }
    }
}

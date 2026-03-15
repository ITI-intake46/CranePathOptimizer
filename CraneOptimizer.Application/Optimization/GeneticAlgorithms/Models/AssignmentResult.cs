namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    /// <summary>
    /// Contains the results of assigning elements to crane positions.
    /// </summary>
    public class AssignmentResult
    {
        /// <summary>
        /// Mapping between crane index and the elements assigned to it.
        /// Key = crane position index in chromosome.
        /// Value = list of element IDs.
        /// </summary>
        public Dictionary<int, List<string>> AssignedElements { get; set; } = new();

        /// <summary>
        /// Detailed evaluation result for every element.
        /// </summary>
        public List<ElementEvaluationResult> EvaluationResults { get; set; } = new();
    }
}
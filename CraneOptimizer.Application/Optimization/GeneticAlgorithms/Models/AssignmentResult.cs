namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    /// <summary>
    /// Contains the results of assigning elements to crane positions.
    /// </summary>
    public class AssignmentResult
    {
        /// <summary>
        /// Mapping between crane index and all elements assigned to it
        /// (including non-liftable failures such as Overweight, HeightFail, etc.).
        /// Key = crane position index in chromosome.
        /// Value = list of element IDs.
        /// </summary>
        public Dictionary<int, List<string>> AssignedElements { get; set; } = new();

        /// <summary>
        /// Mapping between crane index and elements that are confirmed liftable.
        /// Used for the balance penalty calculation so that cranes attracting
        /// many unreachable/failed elements are not mistakenly counted as "busy".
        /// Key = crane position index in chromosome.
        /// Value = list of successfully liftable element IDs.
        /// </summary>
        public Dictionary<int, List<string>> AssignedLiftableElements { get; set; } = new();

        /// <summary>
        /// Detailed evaluation result for every element.
        /// </summary>
        public List<ElementEvaluationResult> EvaluationResults { get; set; } = new();
    }
}
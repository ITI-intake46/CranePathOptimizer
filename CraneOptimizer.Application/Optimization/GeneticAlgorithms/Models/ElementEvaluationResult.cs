namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    /// <summary>
    /// Represents the evaluation result of a single element
    /// against the crane positions defined in a chromosome.
    /// </summary>
    public class ElementEvaluationResult
    {
        /// <summary>
        /// Unique identifier of the evaluated element.
        /// Corresponds to PrecastElement.ElementID.
        /// </summary>
        public string ElementId { get; set; }

        /// <summary>
        /// Index of the crane position in Chromosome.CranePositions
        /// that is responsible for lifting this element.
        /// </summary>
        public int AssignedCraneIndex { get; set; }

        /// <summary>
        /// Result of the lift feasibility evaluation.
        /// </summary>
        public LiftStatus Status { get; set; }

        /// <summary>
        /// Working radius between crane and element center of gravity.
        /// </summary>
        public double Radius { get; set; }

        /// <summary>
        /// Required boom length to perform the lift.
        /// </summary>
        public double BoomLength { get; set; }

        /// <summary>
        /// Required lifting capacity to lift the element.
        /// </summary>
        public double RequiredCapacity { get; set; }
    }
}
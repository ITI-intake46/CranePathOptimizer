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
        /// Available crane capacity from the load chart at the evaluated radius and boom length.
        /// Uses the same derating and safety factors as lift-status determination.
        /// </summary>
        public double AvailableCapacity { get; set; }

        /// <summary>
        /// Required lifting capacity to lift the element.
        /// </summary>
        public double RequiredCapacity { get; set; }

        public ReachStatus ReachStatus { get; set; }
        public ChartStatus ChartStatus { get; set; }
        public bool IsExactMatch { get; set; }
        public bool IsSteppedRadius { get; set; }
        public bool IsBlankCell { get; set; }
        public bool IsZeroCell { get; set; }
        public double SafetyMargin { get; set; }
        public string LookupMode { get; set; } = "ConservativeStep";
        public double RequiredRadius { get; set; }
        public double SelectedChartRadius { get; set; }
        public double SelectedChartBoomLength { get; set; }

        public double CraneBaseX { get; set; }
        public double CraneBaseY { get; set; }
        public double CraneBaseZ { get; set; }

        public double SlewCenterX { get; set; }
        public double SlewCenterY { get; set; }
        public double SlewCenterZ { get; set; }

        public double BoomHingeX { get; set; }
        public double BoomHingeY { get; set; }
        public double BoomHingeZ { get; set; }

        public double LoadPointX { get; set; }
        public double LoadPointY { get; set; }
        public double LoadPointZ { get; set; }

        public string RadiusOriginType { get; set; } = "SlewCenter";
        public string BoomOriginType { get; set; } = "BoomHinge";
        public string CandidateBoomsChecked { get; set; } = string.Empty;
        public string RejectedCandidates { get; set; } = string.Empty;
    }
}

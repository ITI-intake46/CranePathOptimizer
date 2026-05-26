namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    public class LoadChartResolverResult
    {
        public double RequiredRadius { get; set; }
        public double RequiredBoomLength { get; set; }
        public double SelectedChartRadius { get; set; }
        public double SelectedChartBoomLength { get; set; }
        public double ElementWeight { get; set; }
        public double AvailableCapacity { get; set; }
        public ReachStatus ReachStatus { get; set; }
        public ChartStatus ChartStatus { get; set; }
        public LiftStatus LiftStatus { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public string LookupMode { get; set; } = "ConservativeStep";
        public bool IsExactMatch { get; set; }
        public bool IsSteppedRadius { get; set; }
        public bool IsBlankCell { get; set; }
        public bool IsZeroCell { get; set; }
        public double SafetyMargin { get; set; }
        public string CandidateBoomsChecked { get; set; } = string.Empty;
        public string RejectedCandidates { get; set; } = string.Empty;
    }
}

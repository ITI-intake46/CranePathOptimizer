using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using System;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public class ClassificationDetails
    {
        public string FailureReason { get; set; } = string.Empty;
        public string FailureCategory { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;
    }

    public static class LiftClassificationPolicy
    {
        public static ClassificationDetails Classify(
            LiftStatus status,
            ChartStatus chartStatus,
            double? weight,
            double boomLength,
            double maxBoomLength,
            bool hasValidPositions)
        {
            var details = new ClassificationDetails();

            if (!hasValidPositions)
            {
                details.FailureReason = "NoValidSetupPosition";
                details.FailureCategory = "NoValidSetupPosition";
                details.SuggestedAction = "suggest adjusting green/red zones.";
                return details;
            }

            if (!weight.HasValue || weight.Value <= 0)
            {
                details.FailureReason = "MissingWeightOrInvalidData";
                details.FailureCategory = "MissingWeightOrInvalidData";
                details.SuggestedAction = "suggest validating IFC quantities or fallback density rules.";
                return details;
            }

            if (boomLength > maxBoomLength)
            {
                details.FailureReason = "BoomLengthExceeded";
                details.FailureCategory = "BoomLengthExceeded";
                details.SuggestedAction = "suggest longer boom or different crane configuration.";
                return details;
            }

            switch (status)
            {
                case LiftStatus.Liftable:
                    details.FailureReason = "Liftable";
                    details.FailureCategory = "Liftable";
                    details.SuggestedAction = "";
                    break;

                case LiftStatus.OutOfRadius:
                    details.FailureReason = "OutOfRadius";
                    details.FailureCategory = "OutOfRadius";
                    details.SuggestedAction = "suggest increasing allowed green area, adding setup locations, or selecting a longer-radius crane.";
                    break;

                case LiftStatus.Overweight:
                    details.FailureReason = "Overweight";
                    details.FailureCategory = "Overweight";
                    details.SuggestedAction = "suggest larger crane capacity or reviewing element weight.";
                    break;

                case LiftStatus.HeightFail:
                    details.FailureReason = "HeightFail";
                    details.FailureCategory = "HeightFail";
                    details.SuggestedAction = "suggest longer boom or different crane configuration.";
                    break;

                case LiftStatus.NotCharted:
                    switch (chartStatus)
                    {
                        case ChartStatus.OutsideChartEnvelope:
                            details.FailureReason = "OutsideChartEnvelope";
                            details.FailureCategory = "OutsideChartEnvelope";
                            details.SuggestedAction = "suggest elements are outside the crane load chart envelope.";
                            break;
                        case ChartStatus.InvalidBlankCell:
                            details.FailureReason = "InvalidBlankCell";
                            details.FailureCategory = "InvalidBlankCell";
                            details.SuggestedAction = "suggest review crane load chart blank cell rating.";
                            break;
                        case ChartStatus.InvalidZeroCell:
                            details.FailureReason = "InvalidZeroCell";
                            details.FailureCategory = "InvalidZeroCell";
                            details.SuggestedAction = "suggest review crane load chart zero cell rating.";
                            break;
                        case ChartStatus.NoListedCapacity:
                            details.FailureReason = "NoListedCapacityForBoomRadius";
                            details.FailureCategory = "NoListedCapacityForBoomRadius";
                            details.SuggestedAction = "suggest review crane load chart capacity or select a larger crane.";
                            break;
                        default:
                            details.FailureReason = "NoListedCapacityForBoomRadius";
                            details.FailureCategory = "NoListedCapacityForBoomRadius";
                            details.SuggestedAction = "suggest review crane load chart capacity or select a larger crane.";
                            break;
                    }
                    break;

                case LiftStatus.SpecialCase:
                default:
                    details.FailureReason = "SpecialCase";
                    details.FailureCategory = "SpecialCase";
                    details.SuggestedAction = "suggest reviewing element geometry and extracted data.";
                    break;
            }

            return details;
        }

        public static int GetFailureRank(LiftStatus status)
        {
            return status switch
            {
                LiftStatus.Liftable => 5,
                LiftStatus.Overweight => 4,
                LiftStatus.NotCharted => 3,
                LiftStatus.HeightFail => 2,
                LiftStatus.OutOfRadius => 1,
                LiftStatus.SpecialCase => 0,
                _ => 0
            };
        }

        public static void UpdateStatistics(LiftStatistics statistics, LiftStatus status)
        {
            switch (status)
            {
                case LiftStatus.Liftable:
                    statistics.Liftable++;
                    break;
                case LiftStatus.OutOfRadius:
                    statistics.OutOfRadius++;
                    break;
                case LiftStatus.Overweight:
                    statistics.Overweight++;
                    break;
                case LiftStatus.HeightFail:
                    statistics.HeightFail++;
                    break;
                case LiftStatus.SpecialCase:
                    statistics.SpecialCase++;
                    break;
                case LiftStatus.NotCharted:
                    statistics.NotCharted++;
                    break;
            }
        }
    }
}

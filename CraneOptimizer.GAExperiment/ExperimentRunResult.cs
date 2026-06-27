using System;

namespace CraneOptimizer.GAExperiment
{
    public class ExperimentRunResult
    {
        public string RunId { get; set; } = string.Empty;
        public string BenchmarkVersion { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string ProjectFile { get; set; } = string.Empty;
        public string ProjectScale { get; set; } = string.Empty;
        public int ElementCount { get; set; }
        public string CraneModel { get; set; } = string.Empty;
        
        // GA Settings
        public int PopulationSize { get; set; }
        public int MaxGenerations { get; set; }
        public double MutationRate { get; set; }
        public double MutationSigma { get; set; }
        public double CrossoverRate { get; set; }
        public int MaxCranePositions { get; set; }
        public double MinimumRelocationDistance { get; set; }
        public double GridStep { get; set; }
        public int RandomSeed { get; set; }

        // Core GA Outputs
        public double Coverage { get; set; } // 0.0 to 1.0
        public int CoveredElements { get; set; }
        public int TotalElements { get; set; }
        public double Fitness { get; set; }
        public long ExecutionTimeMs { get; set; }
        public int GenerationReached { get; set; }

        // Engineering KPIs
        public int UniqueCranePositions { get; set; }
        public double AverageRadius { get; set; }
        public double MaximumRadius { get; set; }
        public double RelocationDistance { get; set; }
        public int LiftFailures { get; set; }

        // Failure Details Breakdown
        public int OutOfRadius { get; set; }
        public int Overweight { get; set; }
        public int HeightFail { get; set; }
        public int SpecialCase { get; set; }
        public int NotCharted { get; set; }
        public string FailureDominantReason { get; set; } = "None";
        public double FailureDominantPercent { get; set; }

        public void RefreshDerivedFields()
        {
            ElementCount = ElementCount > 0 ? ElementCount : TotalElements;
            ProjectScale = string.IsNullOrWhiteSpace(ProjectScale)
                ? ClassifyProjectScale(ElementCount)
                : ProjectScale;

            var dominance = CalculateFailureDominance(
                OutOfRadius,
                Overweight,
                HeightFail,
                SpecialCase,
                NotCharted);

            FailureDominantReason = dominance.Reason;
            FailureDominantPercent = dominance.Percent;
        }

        public static string ClassifyProjectScale(int elementCount)
        {
            return elementCount switch
            {
                <= 50 => "Small",
                <= 300 => "Medium",
                <= 1000 => "Large",
                _ => "VeryLarge"
            };
        }

        public static (string Reason, double Percent) CalculateFailureDominance(
            int outOfRadius,
            int overweight,
            int heightFail,
            int specialCase,
            int notCharted)
        {
            int notChartedTotal = specialCase + notCharted;
            int totalFailures = outOfRadius + overweight + heightFail + notChartedTotal;
            if (totalFailures <= 0)
            {
                return ("None", 0.0);
            }

            var buckets = new[]
            {
                ("OutOfRadius", outOfRadius),
                ("Overweight", overweight),
                ("HeightFail", heightFail),
                ("NotCharted", notChartedTotal)
            };

            int maxCount = buckets.Max(bucket => bucket.Item2);
            double maxPercent = maxCount * 100.0 / totalFailures;
            int tiedBuckets = buckets.Count(bucket => bucket.Item2 == maxCount);

            if (tiedBuckets > 1 || maxPercent <= 50.0)
            {
                return ("Mixed", Math.Round(maxPercent, 1));
            }

            string reason = buckets.First(bucket => bucket.Item2 == maxCount).Item1;
            return (reason, Math.Round(maxPercent, 1));
        }
    }
}

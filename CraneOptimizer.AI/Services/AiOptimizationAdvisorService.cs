using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Interfaces;

namespace CraneOptimizer.AI.Services
{
    /// <summary>
    /// Deterministic advisor for crane optimization results. This service is intentionally
    /// rule-based: it is testable, explainable, and safe to use without a cloud model.
    /// The optional Ollama layer can summarize this output, but the recommendations here
    /// remain the authoritative application-level assessment.
    /// </summary>
    public sealed class AiOptimizationAdvisorService : IAiOptimizationAdvisorService
    {
        public AiAdvisorResponseDto Analyze(AiOptimizationContextDto context)
        {
            if (!context.HasOptimizationResult || context.TotalElementCount <= 0)
            {
                return new AiAdvisorResponseDto
                {
                    HealthLabel = "No run available",
                    Summary = "No optimization result is available yet. Run the GA optimizer first, then ask the AI Advisor to analyze the result.",
                    Recommendations = new[]
                    {
                        new AiRecommendationDto
                        {
                            Severity = AiRecommendationSeverity.Info,
                            Title = "Run an optimization first",
                            Detail = "The advisor needs a completed optimization result before it can reason about coverage, failures, and crane setup positions.",
                            SuggestedAction = "Open ANALYZE → Run Optimizer, then use AI ASSIST → STUDIO."
                        }
                    },
                    Evidence = Array.Empty<string>()
                };
            }

            var recommendations = new List<AiRecommendationDto>();
            var evidence = BuildEvidence(context);
            var failed = Math.Max(0, context.TotalElementCount - context.LiftedElementCount);
            var coveragePercent = context.CoverageRatio * 100.0;
            var health = GetHealthLabel(context.CoverageRatio);

            AddCoverageRecommendation(context, recommendations);
            AddDominantFailureRecommendation(context, recommendations);
            AddReachabilityRecommendations(context, recommendations);
            AddLoadChartRecommendation(context, recommendations);
            AddSafetyInputRecommendation(context, recommendations);

            if (recommendations.Count == 0)
            {
                recommendations.Add(new AiRecommendationDto
                {
                    Severity = AiRecommendationSeverity.Success,
                    Title = "Optimization result is healthy",
                    Detail = "The current result has high coverage and no dominant failure pattern requiring immediate attention.",
                    SuggestedAction = "Generate the lifting summary report and keep the diagnostics CSV with the project records."
                });
            }

            var craneLabel = string.IsNullOrWhiteSpace(context.CraneName) ? "the selected crane" : context.CraneName;
            var summary = $"{health}: {context.LiftedElementCount} of {context.TotalElementCount} target elements are liftable " +
                          $"({coveragePercent:0.##}% coverage) using {context.PositionCount} setup position(s) with {craneLabel}. " +
                          $"{failed} element(s) remain unresolved.";

            return new AiAdvisorResponseDto
            {
                HealthLabel = health,
                Summary = summary,
                Recommendations = recommendations,
                Evidence = evidence
            };
        }

        private static string GetHealthLabel(double coverageRatio)
        {
            if (coverageRatio >= 0.95) return "Excellent";
            if (coverageRatio >= 0.85) return "Good";
            if (coverageRatio >= 0.65) return "Needs attention";
            return "Critical";
        }

        private static List<string> BuildEvidence(AiOptimizationContextDto context)
        {
            var evidence = new List<string>
            {
                $"Coverage: {context.CoverageRatio:P2}",
                $"Lifted elements: {context.LiftedElementCount}/{context.TotalElementCount}",
                $"Setup positions: {context.PositionCount}",
                $"Failure counts: OutOfRadius={context.FailureCounts.OutOfRadius}, Overweight={context.FailureCounts.Overweight}, HeightFail={context.FailureCounts.HeightFail}, NotCharted={context.FailureCounts.NotCharted}, SpecialCase={context.FailureCounts.SpecialCase}"
            };

            if (context.Reachability != null)
            {
                evidence.Add($"Candidate points: {context.Reachability.ValidCandidatePoints}/{context.Reachability.TotalCandidatePoints} valid");
                evidence.Add($"Best single hot spot coverage: {context.Reachability.MaxHotSpotCoverage:P2}");
            }

            if (!string.IsNullOrWhiteSpace(context.DiagnosticsPath))
                evidence.Add($"Diagnostics CSV: {context.DiagnosticsPath}");

            return evidence;
        }

        private static void AddCoverageRecommendation(AiOptimizationContextDto context, List<AiRecommendationDto> recommendations)
        {
            if (context.CoverageRatio >= 0.95)
                return;

            var severity = context.CoverageRatio < 0.65 ? AiRecommendationSeverity.Critical : AiRecommendationSeverity.Warning;
            var detail = context.PositionCount <= 1
                ? "Coverage is below target and the solution uses one setup position. This often means the current layout is spatially constrained or the run is optimized for minimum relocation rather than coverage."
                : "Coverage is below target even with multiple setup positions, so the remaining failures should be inspected by category before increasing GA runtime.";

            var action = context.PositionCount <= 1
                ? "Try the Crane-Friendly or Balanced Coverage preset, or allow additional setup positions before increasing generations."
                : "Review the dominant failure category first; then consider a larger crane, revised zones, or corrected load chart metadata.";

            recommendations.Add(new AiRecommendationDto
            {
                Severity = severity,
                Title = "Coverage is below a healthy threshold",
                Detail = detail,
                SuggestedAction = action
            });
        }

        private static void AddDominantFailureRecommendation(AiOptimizationContextDto context, List<AiRecommendationDto> recommendations)
        {
            var failureCounts = new Dictionary<string, int>
            {
                ["Out of radius"] = context.FailureCounts.OutOfRadius,
                ["Overweight"] = context.FailureCounts.Overweight,
                ["Height failure"] = context.FailureCounts.HeightFail,
                ["Not charted"] = context.FailureCounts.NotCharted,
                ["Special case"] = context.FailureCounts.SpecialCase
            };

            var dominant = failureCounts
                .OrderByDescending(pair => pair.Value)
                .FirstOrDefault();

            if (dominant.Value <= 0)
                return;

            var failed = Math.Max(1, context.FailureCounts.TotalFailed);
            var share = dominant.Value / (double)failed;
            if (share < 0.35)
                return;

            recommendations.Add(dominant.Key switch
            {
                "Out of radius" => new AiRecommendationDto
                {
                    Severity = AiRecommendationSeverity.Warning,
                    Title = "Main blocker: elements are outside working radius",
                    Detail = $"{dominant.Value} failed element(s) are out of radius, which is {share:P0} of unresolved failures. This points to coverage geometry more than GA convergence.",
                    SuggestedAction = "Move or expand allowed zones, allow more setup positions, reduce grid step for finer candidate points, or select a crane with a larger working radius."
                },
                "Overweight" => new AiRecommendationDto
                {
                    Severity = AiRecommendationSeverity.Critical,
                    Title = "Main blocker: lifting capacity is insufficient",
                    Detail = $"{dominant.Value} failed element(s) exceed available crane capacity after derating and safety factors.",
                    SuggestedAction = "Verify imported element weights, hook/rigging additional weight, derating ratio, and safety factor. If values are correct, use a higher-capacity crane or split the lift scope."
                },
                "Height failure" => new AiRecommendationDto
                {
                    Severity = AiRecommendationSeverity.Warning,
                    Title = "Main blocker: boom or lift height is insufficient",
                    Detail = $"{dominant.Value} failed element(s) require lift geometry outside the current crane setup envelope.",
                    SuggestedAction = "Check boom length limits, wire length, load point elevation, and crane model geometry. Consider a crane/boom configuration with more vertical reach."
                },
                "Not charted" => new AiRecommendationDto
                {
                    Severity = AiRecommendationSeverity.Warning,
                    Title = "Main blocker: load chart gaps",
                    Detail = $"{dominant.Value} failed element(s) could not resolve a valid load chart capacity cell.",
                    SuggestedAction = "Review load chart import metadata, boom headers, radius rows, number-of-lines constraint, and operating condition selection."
                },
                _ => new AiRecommendationDto
                {
                    Severity = AiRecommendationSeverity.Info,
                    Title = "Main blocker: special-case classifications",
                    Detail = $"{dominant.Value} unresolved element(s) are classified as special cases.",
                    SuggestedAction = "Inspect element diagnostics and confirm whether these elements should be included in the GA lifting scope."
                }
            });
        }

        private static void AddReachabilityRecommendations(AiOptimizationContextDto context, List<AiRecommendationDto> recommendations)
        {
            var reachability = context.Reachability;
            if (reachability == null)
                return;

            if (reachability.TotalCandidatePoints > 0 && reachability.ValidCandidatePoints == 0)
            {
                recommendations.Add(new AiRecommendationDto
                {
                    Severity = AiRecommendationSeverity.Critical,
                    Title = "No valid crane candidate points",
                    Detail = "The reachability pre-check found candidate points, but all were rejected by constraints.",
                    SuggestedAction = "Inspect green/allowed zones, forbidden zones, crane footprint radius, and project coordinate alignment before running more GA generations."
                });
                return;
            }

            if (reachability.ValidCandidatePoints > 0 && reachability.ZeroCoveragePoints > reachability.ValidCandidatePoints * 0.5)
            {
                recommendations.Add(new AiRecommendationDto
                {
                    Severity = AiRecommendationSeverity.Warning,
                    Title = "Many valid setup points cover no elements",
                    Detail = $"{reachability.ZeroCoveragePoints} of {reachability.ValidCandidatePoints} valid candidate points have zero reachability.",
                    SuggestedAction = "Check whether allowed zones are too far from target elements or whether the crane radius/boom limits are too restrictive."
                });
            }

            if (reachability.MaxHotSpotCoverage > 0 && reachability.MaxHotSpotCoverage < 0.25 && context.CoverageRatio < 0.85)
            {
                recommendations.Add(new AiRecommendationDto
                {
                    Severity = AiRecommendationSeverity.Info,
                    Title = "Target elements appear spatially dispersed",
                    Detail = $"The best single candidate point reaches only {reachability.MaxHotSpotCoverage:P0} of elements.",
                    SuggestedAction = "Prioritize multi-position presets and evaluate whether the site should be divided into lifting zones."
                });
            }
        }

        private static void AddLoadChartRecommendation(AiOptimizationContextDto context, List<AiRecommendationDto> recommendations)
        {
            if (context.FailureCounts.NotCharted <= 0)
                return;

            var failed = Math.Max(1, context.FailureCounts.TotalFailed);
            var share = context.FailureCounts.NotCharted / (double)failed;
            if (share < 0.2)
                return;

            recommendations.Add(new AiRecommendationDto
            {
                Severity = AiRecommendationSeverity.Warning,
                Title = "Review load chart provenance",
                Detail = "A meaningful share of failures are load-chart lookup failures rather than clear capacity or reach failures.",
                SuggestedAction = "Open the load chart preview and confirm radius units, boom columns, blank cells, duplicate radius handling, number of lines, and operating condition metadata."
            });
        }

        private static void AddSafetyInputRecommendation(AiOptimizationContextDto context, List<AiRecommendationDto> recommendations)
        {
            if (context.SafetyFactor <= 0 || context.DeratingRatio <= 0)
            {
                recommendations.Add(new AiRecommendationDto
                {
                    Severity = AiRecommendationSeverity.Critical,
                    Title = "Invalid safety input detected",
                    Detail = "Safety factor and derating ratio must be positive values for meaningful capacity evaluation.",
                    SuggestedAction = "Correct the GA/crane setup safety inputs before accepting optimization results."
                });
                return;
            }

            if (context.AdditionalWeight < 0 || context.WireLength < 0)
            {
                recommendations.Add(new AiRecommendationDto
                {
                    Severity = AiRecommendationSeverity.Warning,
                    Title = "Suspicious lifting accessory input",
                    Detail = "Additional hook/rigging weight and wire length should not be negative.",
                    SuggestedAction = "Check the crane setup inputs used for this optimization run."
                });
            }
        }
    }
}

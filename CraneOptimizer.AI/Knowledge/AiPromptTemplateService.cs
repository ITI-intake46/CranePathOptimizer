using CraneOptimizer.AI.DTOs;

namespace CraneOptimizer.AI.Knowledge
{
    public sealed class AiPromptTemplateService : IAiPromptTemplateService
    {
        private static readonly IReadOnlyList<AiPromptTemplate> Templates = new[]
        {
            new AiPromptTemplate
            {
                Key = "no-result",
                Title = "No optimization result",
                AppliesWhen = "No completed optimizer result is available.",
                Prompt = "Use Status, Evidence, Recommendation, Next Click, Risk, and Confidence. Explain what is missing before optimization can be analyzed and guide the user to the exact next UI step."
            },
            new AiPromptTemplate
            {
                Key = "low-coverage",
                Title = "Low coverage",
                AppliesWhen = "Coverage is below 80% or many elements remain unlifted.",
                Prompt = "Diagnose low coverage. Separate geometry, candidate zone, crane reach, load chart, and convergence causes. Produce a prioritized next-action list and do not suggest unsafe shortcuts."
            },
            new AiPromptTemplate
            {
                Key = "out-of-radius",
                Title = "Out-of-radius dominant",
                AppliesWhen = "Out-of-radius failures dominate unresolved elements.",
                Prompt = "Explain the out-of-radius pattern. Recommend allowed-zone expansion, more candidate setup locations, grid-step review, or crane radius changes. Include exact UI tabs and buttons."
            },
            new AiPromptTemplate
            {
                Key = "overweight",
                Title = "Overweight dominant",
                AppliesWhen = "Overweight failures dominate unresolved elements.",
                Prompt = "Explain likely capacity or element weight causes. Recommend checking element weight metadata, load chart capacity, safety factor, derating, and crane model selection."
            },
            new AiPromptTemplate
            {
                Key = "not-charted",
                Title = "Not-charted dominant",
                AppliesWhen = "Failures are primarily missing from the selected load chart.",
                Prompt = "Explain that the selected boom/radius/load combination is missing from chart coverage. Recommend load chart import validation and crane chart review."
            },
            new AiPromptTemplate
            {
                Key = "healthy",
                Title = "Healthy run review",
                AppliesWhen = "Coverage is high and there are no dominant blocker failures.",
                Prompt = "Summarize the result for report review. Include remaining limitations, assumptions, and the next report/export step."
            }
        };

        public IReadOnlyList<AiPromptTemplate> GetAllTemplates() => Templates;

        public AiPromptTemplate SelectTemplate(AiOptimizationContextDto context, AiAdvisorResponseDto advisorResponse)
        {
            if (!context.HasOptimizationResult)
                return Templates.First(t => t.Key == "no-result");

            if (context.CoverageRatio >= 0.9)
                return Templates.First(t => t.Key == "healthy");

            var failures = context.FailureCounts;

            // Failure-mode templates should be selected from explicit optimizer failure diagnostics first.
            // Low coverage is a broad symptom; otherwise it can mask a clear engineering blocker such as
            // out-of-radius, overweight, or not-charted failures when many elements remain unlifted.
            var dominantFailure = new[]
            {
                (Key: "out-of-radius", Count: failures.OutOfRadius),
                (Key: "overweight", Count: failures.Overweight),
                (Key: "not-charted", Count: failures.NotCharted)
            }
            .OrderByDescending(x => x.Count)
            .First();

            if (dominantFailure.Count > 0)
            {
                return Templates.First(t => t.Key == dominantFailure.Key);
            }

            return Templates.First(t => t.Key == "low-coverage");
        }
    }
}

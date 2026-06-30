using CraneOptimizer.AI.DTOs;
using CraneOptimizer.AI.Services;
using Xunit;

namespace CraneOptimizer.Application.Tests.AI
{
    public class AiOptimizationAdvisorServiceTests
    {
        [Fact]
        public void Analyze_NoResult_ReturnsRunOptimizerRecommendation()
        {
            var service = new AiOptimizationAdvisorService();

            var response = service.Analyze(new AiOptimizationContextDto
            {
                HasOptimizationResult = false
            });

            Assert.Equal("No run available", response.HealthLabel);
            Assert.Contains(response.Recommendations, r => r.Title.Contains("Run an optimization"));
        }

        [Fact]
        public void Analyze_OutOfRadiusDominant_RecommendsGeometryActions()
        {
            var service = new AiOptimizationAdvisorService();

            var response = service.Analyze(new AiOptimizationContextDto
            {
                HasOptimizationResult = true,
                TotalElementCount = 100,
                LiftedElementCount = 62,
                CoverageRatio = 0.62,
                PositionCount = 1,
                CraneName = "Test Crane",
                FailureCounts = new FailureCountsDto
                {
                    Liftable = 62,
                    OutOfRadius = 30,
                    Overweight = 3,
                    HeightFail = 2,
                    NotCharted = 3
                }
            });

            Assert.Equal("Critical", response.HealthLabel);
            Assert.Contains(response.Recommendations, r => r.Title.Contains("outside working radius"));
            Assert.Contains(response.Recommendations, r => r.SuggestedAction.Contains("additional setup positions"));
        }

        [Fact]
        public void Analyze_NotChartedDominant_RecommendsLoadChartReview()
        {
            var service = new AiOptimizationAdvisorService();

            var response = service.Analyze(new AiOptimizationContextDto
            {
                HasOptimizationResult = true,
                TotalElementCount = 50,
                LiftedElementCount = 40,
                CoverageRatio = 0.80,
                PositionCount = 2,
                FailureCounts = new FailureCountsDto
                {
                    Liftable = 40,
                    NotCharted = 8,
                    OutOfRadius = 2
                }
            });

            Assert.Contains(response.Recommendations, r => r.Title.Contains("load chart", StringComparison.OrdinalIgnoreCase));
        }
    }
}

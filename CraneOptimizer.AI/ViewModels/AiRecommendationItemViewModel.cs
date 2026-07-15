using CraneOptimizer.AI.DTOs;

namespace CraneOptimizer.AI.ViewModels
{
    public sealed class AiRecommendationItemViewModel
    {
        public AiRecommendationItemViewModel(AiRecommendationDto dto)
        {
            Severity = dto.Severity.ToString();
            Title = dto.Title;
            Detail = dto.Detail;
            SuggestedAction = dto.SuggestedAction;
        }

        public string Severity { get; }
        public string Title { get; }
        public string Detail { get; }
        public string SuggestedAction { get; }
    }
}

using System.Windows.Input;

namespace CraneOptimizer.UI.Features.F13_AiAdvisor.ViewModels
{
    /// <summary>
    /// Confirmable, non-destructive UI action proposal. These cards may navigate the UI,
    /// copy/export text, or open panels, but they must not alter crane-planning data.
    /// </summary>
    public sealed class AiActionProposalViewModel
    {
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string ActionType { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public string Risk { get; init; } = "Low — navigation or documentation action only.";
        public string ConfirmLabel { get; init; } = "Confirm";
        public ICommand? ConfirmCommand { get; init; }
    }
}

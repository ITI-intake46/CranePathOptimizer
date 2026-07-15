using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Services
{
    public sealed class UiInteractionContextService : IUiInteractionContextService
    {
        private string _activeTabName = "HOME";
        private string _activePanelName = "Home";
        private DateTimeOffset _lastUpdatedAt = DateTimeOffset.Now;

        public event EventHandler? ContextChanged;
        public event EventHandler<AiUiActionRequestedEventArgs>? ActionRequested;

        public string ActiveTabName => _activeTabName;
        public string ActivePanelName => _activePanelName;
        public DateTimeOffset LastUpdatedAt => _lastUpdatedAt;

        public void SetActiveTab(string tabName, string panelName)
        {
            tabName = string.IsNullOrWhiteSpace(tabName) ? "HOME" : tabName.Trim();
            panelName = string.IsNullOrWhiteSpace(panelName) ? tabName : panelName.Trim();

            if (string.Equals(_activeTabName, tabName, StringComparison.Ordinal)
                && string.Equals(_activePanelName, panelName, StringComparison.Ordinal))
                return;

            _activeTabName = tabName;
            _activePanelName = panelName;
            _lastUpdatedAt = DateTimeOffset.Now;
            ContextChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RequestTabNavigation(string tabName, string reason)
        {
            if (string.IsNullOrWhiteSpace(tabName))
                return;

            ActionRequested?.Invoke(this, new AiUiActionRequestedEventArgs("NavigateTab", tabName.Trim(), reason));
        }
    }
}

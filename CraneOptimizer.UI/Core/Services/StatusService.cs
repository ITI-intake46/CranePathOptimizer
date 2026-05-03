using CraneOptimizer.UI.Core.Interfaces;

namespace CraneOptimizer.UI.Core.Services
{
    public sealed class StatusService : IStatusService
    {
        private string _statusText = "Ready";
        private string _historyBreadcrumb = "No recent action";

        public string StatusText => _statusText;
        public string HistoryBreadcrumb => _historyBreadcrumb;

        public event Action? StatusChanged;
        public event Action? HistoryChanged;

        public void SetStatus(string statusText)
        {
            if (_statusText == statusText)
                return;

            _statusText = statusText;
            StatusChanged?.Invoke();
        }

        public void SetHistory(string historyBreadcrumb)
        {
            if (_historyBreadcrumb == historyBreadcrumb)
                return;

            _historyBreadcrumb = historyBreadcrumb;
            HistoryChanged?.Invoke();
        }

        public void Set(string statusText, string? historyBreadcrumb = null)
        {
            SetStatus(statusText);

            if (historyBreadcrumb != null)
                SetHistory(historyBreadcrumb);
        }
    }
}

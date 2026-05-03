namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IStatusService
    {
        string StatusText { get; }
        string HistoryBreadcrumb { get; }

        event Action? StatusChanged;
        event Action? HistoryChanged;

        void SetStatus(string statusText);
        void SetHistory(string historyBreadcrumb);
        void Set(string statusText, string? historyBreadcrumb = null);
    }
}

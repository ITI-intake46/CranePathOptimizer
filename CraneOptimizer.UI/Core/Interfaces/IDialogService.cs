namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IDialogService
    {
        string? OpenFile(string filter);
        void ShowMessage(string message);
        bool Confirm(string message, string title);
        void ShowNonBlockingError(string message, string title);
    }
}

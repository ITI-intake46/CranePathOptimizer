namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IDialogService
    {
        string? OpenFile(string filter);
        void ShowMessage(string message);
    }
}
